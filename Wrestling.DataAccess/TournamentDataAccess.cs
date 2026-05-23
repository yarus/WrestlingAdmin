using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Wrestling.Data;

namespace Wrestling.DataAccess
{
    public class TournamentDataAccess : ITournamentDataAccess
    {
        // Rotated pre-save backups. Location and retention come from
        // GlobalSettingsInfo on the tournament being saved:
        //   IsBackupEnabled  - master toggle; if false, skip all backup work
        //   MaxBackupCount   - how many copies to keep (oldest rotated out)
        //   BackupFolderPath - optional override for backup root; empty/null
        //                      resolves to <tournament-dir>/Backups
        //
        // Each tournament gets its own subfolder inside the backup root
        // (named after the .wrt filename without extension) so multiple
        // tournaments sharing a working directory don't mix timestamps.
        //
        // Backup operations are best-effort: any failure (permissions, disk
        // full, path collision) is logged but never blocks the save itself —
        // the atomic write in the storage layer already protects against
        // torn files.
        private const string DefaultBackupFolderName = "Backups";
        private const int FallbackMaxBackupCount = 20;

        private readonly IStorageDataAccess _storageDataAccess;

        public TournamentDataAccess(IStorageDataAccess storageDataAccess)
        {
            _storageDataAccess = storageDataAccess;
        }

        public bool SaveToFile(TournamentInfo item, string fileName)
        {
            TryCreateBackup(fileName, item?.Settings);

            _storageDataAccess.SaveToFile(item, fileName);

            if (!VerifySavedFile(fileName))
            {
                TryRestoreLatestBackup(fileName, item?.Settings);
                FileLogger.Log("TournamentDataAccess.SaveToFile [Corrupt]", fileName,
                    "post-save verification failed; latest backup restored if available");
                return false;
            }

            TryRotateBackups(fileName, item?.Settings);
            return true;
        }

        public async Task<bool> SaveToFileAsync(TournamentInfo item, string fileName, CancellationToken cancellationToken = default)
        {
            TryCreateBackup(fileName, item?.Settings);

            await _storageDataAccess.SaveToFileAsync(item, fileName, cancellationToken).ConfigureAwait(false);

            if (!await VerifySavedFileAsync(fileName, cancellationToken).ConfigureAwait(false))
            {
                TryRestoreLatestBackup(fileName, item?.Settings);
                FileLogger.Log("TournamentDataAccess.SaveToFileAsync [Corrupt]", fileName,
                    "post-save verification failed; latest backup restored if available");
                return false;
            }

            TryRotateBackups(fileName, item?.Settings);
            return true;
        }

        public TournamentInfo LoadFromFile(string fileName)
        {
            return _storageDataAccess.ReadFromFile<TournamentInfo>(fileName);
        }

        public async Task<TournamentInfo> LoadFromFileAsync(string fileName, CancellationToken cancellationToken = default)
        {
            return await _storageDataAccess.ReadFromFileAsync<TournamentInfo>(fileName, cancellationToken).ConfigureAwait(false);
        }

        private static bool IsBackupEnabled(GlobalSettingsInfo settings) => settings == null || settings.IsBackupEnabled;

        private static int GetMaxBackups(GlobalSettingsInfo settings)
        {
            if (settings == null || settings.MaxBackupCount <= 0) return FallbackMaxBackupCount;
            return settings.MaxBackupCount;
        }

        private static string GetBackupFolder(string fileName, GlobalSettingsInfo settings)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            var tournamentDir = Path.GetDirectoryName(fileName) ?? string.Empty;
            var configured = settings?.BackupFolderPath;

            string root;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                // Absolute path wins; relative path resolves against the
                // tournament's working directory so a setting like "Backups"
                // or "..\shared-backups" stays predictable per tournament.
                root = Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(tournamentDir, configured);
            }
            else
            {
                root = Path.Combine(tournamentDir, DefaultBackupFolderName);
            }

            var namespaceName = Path.GetFileNameWithoutExtension(fileName);
            return Path.Combine(root, namespaceName);
        }

        private static void TryCreateBackup(string fileName, GlobalSettingsInfo settings)
        {
            if (!IsBackupEnabled(settings)) return;

            try
            {
                if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName)) return;

                var folder = GetBackupFolder(fileName, settings);
                if (string.IsNullOrEmpty(folder)) return;

                Directory.CreateDirectory(folder);

                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var backupPath = Path.Combine(folder, stamp + ".wrt");

                // Millisecond collisions on rapid saves — append a numeric
                // suffix rather than silently overwriting a just-taken backup.
                var unique = backupPath;
                int suffix = 1;
                while (File.Exists(unique))
                {
                    unique = Path.Combine(folder, $"{stamp}_{suffix}.wrt");
                    suffix++;
                }

                File.Copy(fileName, unique, overwrite: false);
            }
            catch (Exception ex) when (IsExpectedBackupException(ex))
            {
                FileLogger.Log("TournamentDataAccess.TryCreateBackup", fileName, ex);
            }
        }

        private bool VerifySavedFile(string fileName)
        {
            try
            {
                var info = _storageDataAccess.ReadFromFile<TournamentInfo>(fileName);
                return info != null;
            }
            catch (Exception ex) when (IsExpectedBackupException(ex))
            {
                FileLogger.Log("TournamentDataAccess.VerifySavedFile", fileName, ex);
                return false;
            }
        }

        private async Task<bool> VerifySavedFileAsync(string fileName, CancellationToken cancellationToken)
        {
            try
            {
                var info = await _storageDataAccess.ReadFromFileAsync<TournamentInfo>(fileName, cancellationToken).ConfigureAwait(false);
                return info != null;
            }
            catch (Exception ex) when (IsExpectedBackupException(ex))
            {
                FileLogger.Log("TournamentDataAccess.VerifySavedFileAsync", fileName, ex);
                return false;
            }
        }

        private static void TryRestoreLatestBackup(string fileName, GlobalSettingsInfo settings)
        {
            try
            {
                var folder = GetBackupFolder(fileName, settings);
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

                var newest = Directory.GetFiles(folder, "*.wrt")
                    .OrderByDescending(p => p, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (newest == null) return;

                File.Copy(newest, fileName, overwrite: true);
            }
            catch (Exception ex) when (IsExpectedBackupException(ex))
            {
                FileLogger.Log("TournamentDataAccess.TryRestoreLatestBackup", fileName, ex);
            }
        }

        private static void TryRotateBackups(string fileName, GlobalSettingsInfo settings)
        {
            if (!IsBackupEnabled(settings)) return;

            try
            {
                var folder = GetBackupFolder(fileName, settings);
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

                var max = GetMaxBackups(settings);
                var files = Directory.GetFiles(folder, "*.wrt")
                    .OrderByDescending(p => p, StringComparer.Ordinal)
                    .ToList();

                foreach (var stale in files.Skip(max))
                {
                    try { File.Delete(stale); }
                    catch (Exception delEx) when (IsExpectedBackupException(delEx))
                    {
                        FileLogger.Log("TournamentDataAccess.TryRotateBackups [Delete]", stale, delEx);
                    }
                }
            }
            catch (Exception ex) when (IsExpectedBackupException(ex))
            {
                FileLogger.Log("TournamentDataAccess.TryRotateBackups", fileName, ex);
            }
        }

        private static bool IsExpectedBackupException(Exception ex)
        {
            // Everything a filesystem can throw for permission / capacity /
            // path-validation reasons. Anything outside this set (OOM,
            // AccessViolation, NullReference from a bug) propagates so the
            // global crash handler can log it and save a backup of the
            // in-memory tournament.
            return ex is IOException
                   || ex is UnauthorizedAccessException
                   || ex is SecurityException
                   || ex is PathTooLongException
                   || ex is ArgumentException
                   || ex is NotSupportedException;
        }
    }
}
