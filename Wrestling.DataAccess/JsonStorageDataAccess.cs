using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wrestling.DataAccess
{
    public class JsonStorageDataAccess : IStorageDataAccess
    {
        // Single shared converter handles legacy mat-name keys ("Carpets",
        // "CarpetID", "CarpetLabel") in old .wrt files. See LegacyMatNameConverter.
        private static readonly LegacyMatNameConverter _legacyMatConverter = new LegacyMatNameConverter();

        // Orphan tmp files (atomic-write residue from a prior crash) older than
        // this are reaped on the next load. A normal save completes in well
        // under a second, so 5 min is comfortably past any legitimate
        // in-progress write while still keeping disk clutter bounded.
        private static readonly TimeSpan OrphanTempFileMinAge = TimeSpan.FromMinutes(5);

        // Pinned hardening for every serializer instance the storage layer
        // creates. TypeNameHandling.None blocks $type polymorphic gadgets in
        // untrusted .wrt files (we never write $type — all our DTOs are flat),
        // so loading a malicious file cannot trigger a deserialization gadget.
        // Kept centralized so we never accidentally instantiate a bare
        // JsonSerializer somewhere with default settings.
        internal static JsonSerializer CreateSerializer(JsonConverter extraConverter = null)
        {
            var s = new JsonSerializer
            {
                TypeNameHandling = TypeNameHandling.None,
            };
            if (extraConverter != null)
            {
                s.Converters.Add(extraConverter);
            }
            return s;
        }

        private static JsonSerializer CreateLoadSerializer() => CreateSerializer(_legacyMatConverter);

        private static JsonSerializerSettings CreateLoadSettings()
        {
            return new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                Converters = { _legacyMatConverter },
            };
        }


        public void ProcessDirectory<T>(string targetDirectory, ref List<T> list, string mask)
        {
            // Process the list of files found in the directory. 
            string[] fileEntries = Directory.GetFiles(targetDirectory, string.IsNullOrEmpty(mask) ? "*.*" : mask);

            foreach (string fileName in fileEntries)
            {
                list.Add(this.ReadFromFile<T>(fileName));
            }

            // Recurse into subdirectories of this directory. 
            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);

            foreach (string subdirectory in subdirectoryEntries)
            {
                this.ProcessDirectory(subdirectory, ref list, mask);
            }
        }

        public void SaveToStorage<T>(T item, string storageFolder, string fileName)
        {
            SaveToFile(item, storageFolder + fileName);
        }

        public async Task<bool> SaveToFileAsync<T>(T item, string fileName, CancellationToken cancellationToken = default)
        {
            // Atomic write: serialize to a sibling tmp file, then swap into place.
            // Keeps the prior-good .wrt intact if serialization or I/O throws
            // mid-stream, which matters for tournaments synced across mats.
            var tmpPath = GetTempSiblingPath(fileName);

            try
            {
                using (var stream = new MemoryStream())
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
                    {
                        var serializer = CreateSerializer();
                        serializer.Serialize(writer, item);
                        await writer.FlushAsync().ConfigureAwait(false);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Seek(0, SeekOrigin.Begin);

                    using (var file = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                    {
                        await stream.CopyToAsync(file, 81920, cancellationToken).ConfigureAwait(false);
                        await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                AtomicReplace(tmpPath, fileName);
                return true;
            }
            catch (OperationCanceledException)
            {
                // Caller asked to bail out — drop the half-written tmp before
                // propagating. The destination file is untouched (atomic swap
                // hasn't run), so the existing .wrt remains the last good copy.
                TryDelete(tmpPath);
                throw;
            }
        }

        public bool SaveToFile<T>(T item, string fileName)
        {
            // Atomic write: serialize fully to a tmp file first so a failure mid-write
            // cannot truncate the previous save.
            var tmpPath = GetTempSiblingPath(fileName);

            using (var writeFileStream = new StreamWriter(tmpPath, append: false, new UTF8Encoding(false)))
            {
                var jsonWriter = new JsonTextWriter(writeFileStream);
                var ser = CreateSerializer();
                ser.Serialize(jsonWriter, item);
                jsonWriter.Flush();
            }

            AtomicReplace(tmpPath, fileName);
            return true;
        }

        private static string GetTempSiblingPath(string fileName)
        {
            var dir = Path.GetDirectoryName(fileName);
            var name = Path.GetFileName(fileName);
            var tmp = name + ".tmp." + Guid.NewGuid().ToString("N");
            return string.IsNullOrEmpty(dir) ? tmp : Path.Combine(dir, tmp);
        }

        private static void AtomicReplace(string source, string destination)
        {
            try
            {
                if (File.Exists(destination))
                {
                    File.Replace(source, destination, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(source, destination);
                }
            }
            catch
            {
                TryDelete(source);
                throw;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort cleanup */ }
        }

        // Sweep stale atomic-write residue (*.tmp.<guid>) from the directory
        // hosting the file being loaded. A normal save flushes-then-swaps in
        // sub-second time, so anything older than OrphanTempFileMinAge is from
        // a prior crash (process killed mid-save, power loss, OOM). Best-effort
        // — any directory enumeration / deletion failure is swallowed so a
        // permission glitch on cleanup never blocks the actual load.
        private static void TryCleanOrphanTempFiles(string referencePath)
        {
            try
            {
                if (string.IsNullOrEmpty(referencePath)) return;

                var dir = Path.GetDirectoryName(referencePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                // Skip network paths — sweeping a peer's share would delete
                // tmp files mid-write on another machine.
                if (IsNetworkPath(referencePath)) return;

                var cutoff = DateTime.UtcNow - OrphanTempFileMinAge;
                foreach (var tmp in Directory.EnumerateFiles(dir, "*.tmp.*"))
                {
                    try
                    {
                        var info = new FileInfo(tmp);
                        if (info.LastWriteTimeUtc <= cutoff)
                        {
                            info.Delete();
                        }
                    }
                    catch (Exception perFileEx) when (IsExpectedLoadException(perFileEx) || perFileEx is IOException)
                    {
                        // One stuck file shouldn't stop the rest of the sweep.
                        FileLogger.Log("JsonStorageDataAccess.TryCleanOrphanTempFiles [Delete]", tmp, perFileEx);
                    }
                }
            }
            catch (Exception ex) when (IsExpectedLoadException(ex) || ex is IOException)
            {
                FileLogger.Log("JsonStorageDataAccess.TryCleanOrphanTempFiles", referencePath, ex);
            }
        }

        public T ReadFromFile<T>(string path)
        {
            // Load paths must never crash the app — import polls network/UNC paths
            // on a timer during live matches, where WiFi drops and peer-laptop
            // mid-write truncations are expected. Return default(T) for every
            // expected I/O / parse failure, logging the cause to
            // %LocalAppData%/<app>/Logs/ so the secretary can diagnose later.
            // Network paths get the same retry+backoff treatment as the async path.

            if (!File.Exists(path))
            {
                return default(T);
            }

            TryCleanOrphanTempFiles(path);

            int maxRetries = IsNetworkPath(path) ? 3 : 1;
            const int initialDelayMs = 200;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using (var reader = new StreamReader(path))
                    {
                        var jsonReader = new JsonTextReader(reader);
                        var ser = CreateLoadSerializer();
                        return ser.Deserialize<T>(jsonReader);
                    }
                }
                catch (Exception ex) when (IsRetryableException(ex))
                {
                    if (attempt < maxRetries)
                    {
                        var delayMs = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                        try { System.Threading.Thread.Sleep(delayMs); } catch { }
                        continue;
                    }

                    FileLogger.Log(BuildSource("ReadFromFile", ex, attempt, maxRetries), path, ex);
                    return default(T);
                }
                catch (Exception ex) when (IsExpectedLoadException(ex))
                {
                    FileLogger.Log(BuildSource("ReadFromFile", ex, attempt, maxRetries), path, ex);
                    return default(T);
                }
            }

            return default(T);
        }

        public async Task<T> ReadFromFileAsync<T>(string path, CancellationToken cancellationToken = default)
        {
            // Load paths never throw for expected I/O / parse errors — see
            // ReadFromFile above for the full policy. OperationCanceledException
            // is the one exception: cancellation is an explicit caller signal,
            // not an I/O failure, so it propagates.
            if (!File.Exists(path))
            {
                return default(T);
            }

            TryCleanOrphanTempFiles(path);

            int maxRetries = IsNetworkPath(path) ? 5 : 3;
            const int initialDelayMs = 200;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using (var stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 4096,
                        useAsync: true))
                    using (var reader = new StreamReader(stream))
                    {
                        // StreamReader.ReadToEndAsync has no CT overload on
                        // netstandard2.0 — but the FileStream above honors the
                        // OS-level cancellation through async I/O completion
                        // ports, and we re-check the token between retries
                        // and after the read completes.
                        string jsonContent = await reader.ReadToEndAsync().ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        return JsonConvert.DeserializeObject<T>(jsonContent, CreateLoadSettings());
                    }
                }
                catch (Exception ex) when (IsRetryableException(ex))
                {
                    if (attempt < maxRetries)
                    {
                        var delayMs = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    FileLogger.Log(BuildSource("ReadFromFileAsync", ex, attempt, maxRetries), path, ex);
                    return default(T);
                }
                catch (Exception ex) when (IsExpectedLoadException(ex))
                {
                    FileLogger.Log(BuildSource("ReadFromFileAsync", ex, attempt, maxRetries), path, ex);
                    return default(T);
                }
            }

            return default(T);
        }

        private static bool IsRetryableException(Exception ex)
        {
            // Transient I/O that is worth retrying with a short backoff. JsonException
            // is included because a peer mat mid-write can leave the file briefly
            // unparseable before File.Replace completes.
            return ex is IOException
                   || ex is UnauthorizedAccessException
                   || ex is JsonException
                   || ex is SocketException
                   || ex is TimeoutException;
        }

        private static bool IsExpectedLoadException(Exception ex)
        {
            // Non-transient failures we still tolerate: argument/path validation,
            // security denials, encoding issues inside Json. Anything outside this
            // set (OOM, AccessViolation, NullReference from a bug) is allowed to
            // propagate to the global crash handler which saves a backup.
            return ex is ArgumentException
                   || ex is NotSupportedException
                   || ex is SecurityException
                   || ex is PathTooLongException
                   || ex is DirectoryNotFoundException
                   || ex is FileNotFoundException
                   || ex is FormatException;
        }

        private static bool IsNetworkPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return true;
            try
            {
                var root = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(root) || root.Length < 2) return false;
                var driveInfo = new DriveInfo(root);
                return driveInfo.DriveType == DriveType.Network;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildSource(string method, Exception ex, int attempt, int maxRetries)
        {
            var category = Classify(ex);
            return string.Format("{0} [{1}] attempt {2}/{3}", method, category, attempt, maxRetries);
        }

        private static string Classify(Exception ex)
        {
            switch (ex)
            {
                case JsonException _: return "Corrupt";
                case UnauthorizedAccessException _:
                case SecurityException _: return "AccessDenied";
                case SocketException _:
                case TimeoutException _: return "Transient";
                case PathTooLongException _:
                case ArgumentException _:
                case NotSupportedException _:
                case FormatException _: return "InvalidPath";
                case DirectoryNotFoundException _:
                case FileNotFoundException _: return "NotFound";
                case IOException _: return "IO";
                default: return "Other";
            }
        }

        public IEnumerable<string> GetFileNamesInDirectory(string path, string mask)
        {
            return Directory.GetFiles(path, string.IsNullOrEmpty(mask) ? "*.*" : mask);
        }
    }
}
