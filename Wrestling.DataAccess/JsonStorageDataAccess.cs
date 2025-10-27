using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Wrestling.DataAccess
{
    public class JsonStorageDataAccess : IStorageDataAccess
    {
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

        public async Task<bool> SaveToFileAsync<T>(T item, string fileName)
        {
            try
            {
                using (var file = File.Open(fileName, FileMode.Create))
                {
                    using (var stream = new MemoryStream())
                    {
                        using (var writer = new StreamWriter(stream))
                        {
                            var serizlizer = JsonSerializer.CreateDefault();
                            serizlizer.Serialize(writer, item);

                            await writer.FlushAsync().ConfigureAwait(false);

                            stream.Seek(0, SeekOrigin.Begin);

                            await stream.CopyToAsync(file).ConfigureAwait(false);
                        }
                    }

                    await file.FlushAsync().ConfigureAwait(false);
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool SaveToFile<T>(T item, string fileName)
        {
            try
            {
                using (var writeFileStream = new StreamWriter(fileName))
                {
                    var jsonWriter = new JsonTextWriter(writeFileStream);
                    var ser = new JsonSerializer();
                    ser.Serialize(jsonWriter, item);
                    jsonWriter.Flush();
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public T ReadFromFile<T>(string path)
        {
            T result;

            if (!File.Exists(path))
            {
                return default(T);
            }

            try
            {
                using (var reader = new StreamReader(path))
                {
                    var jsonReader = new JsonTextReader(reader);
                    var ser = new JsonSerializer();
                    result = ser.Deserialize<T>(jsonReader);
                }
            }
            catch
            {
                return default(T);
            }

            return result;
        }

        public async Task<T> ReadFromFileAsync<T>(string path)
        {
            const int maxRetries = 3;
            const int initialDelayMs = 200;
            int retryCount = 0;
            Exception lastException = null;

            while (retryCount < maxRetries)
            {
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
                        string jsonContent = await reader.ReadToEndAsync().ConfigureAwait(false);
                        return JsonConvert.DeserializeObject<T>(jsonContent);
                    }
                }
                catch (Exception ex) when (IsRetryableException(ex))
                {
                    lastException = ex;
                    retryCount++;

                    if (retryCount < maxRetries)
                    {
                        int delayMs = initialDelayMs * (int)Math.Pow(2, retryCount - 1);
                        await Task.Delay(delayMs).ConfigureAwait(false);
                    }
                }
            }

            // Log the last exception if needed
            Debug.WriteLine($"Failed after {maxRetries} attempts. Last error: {lastException?.Message}");
            return default(T);
        }

        private bool IsRetryableException(Exception ex)
        {
            return ex is IOException
                   || ex is UnauthorizedAccessException
                   || ex is JsonException
                   || ex is TimeoutException;
        }

        public IEnumerable<string> GetFileNamesInDirectory(string path, string mask)
        {
            return Directory.GetFiles(path, string.IsNullOrEmpty(mask) ? "*.*" : mask);
        }
    }
}
