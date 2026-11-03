using System;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Wrestling.DataAccess;
using Wrestling.Providers;

namespace Wrestling.UI.Material.Model
{
    public enum ManualImportStatus { Completed, SaveRequired, TargetChanged, BackupFailed, SaveFailed }

    public sealed record ManualImportResult(ManualImportStatus Status, ImportResult Import, string BackupFileName = null, bool Saved = false);

    // The same prepare/apply engine as peer sync, with a recovery copy of the
    // in-memory tournament before the operator's explicit file import.
    public sealed class ManualTournamentImportService
    {
        private readonly IDataContext _context;
        private readonly ITournamentImporter _importer;
        private readonly ITournamentsManager _manager;
        private readonly IResultsService _results;
        private readonly IEntityToInfoAdapter _adapter;
        private readonly ITournamentDataAccess _dataAccess;
        private readonly string _backupRoot;
        private Entities.Tournament _pendingSaveTarget;

        public ManualTournamentImportService(IDataContext context, ITournamentImporter importer,
            ITournamentsManager manager, IResultsService results, IEntityToInfoAdapter adapter,
            ITournamentDataAccess dataAccess, string backupRoot = null)
        {
            _context = context;
            _importer = importer;
            _manager = manager;
            _results = results;
            _adapter = adapter;
            _dataAccess = dataAccess;
            _backupRoot = backupRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WrestlingAdmin", "Backups", "ManualImports");
        }

        // Call on the UI thread. Only loading and writing the detached backup
        // run in the background; Apply and the save snapshot stay on the UI.
        public async Task<ManualImportResult> ImportAsync(string source, CancellationToken cancellationToken = default)
        {
            var target = _context.Tournament;
            if (target == null) return new ManualImportResult(ManualImportStatus.TargetChanged, ImportResult.Error());
            if (string.IsNullOrEmpty(target.FileName))
                return new ManualImportResult(ManualImportStatus.SaveRequired, ImportResult.NoNewData());

            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            EventHandler<Entities.Tournament> changed = (_, current) =>
            {
                if (!ReferenceEquals(current, target)) cancellation.Cancel();
            };
            _context.TournamentChanged += changed;
            try
            {
                var token = cancellation.Token;
                var plan = await Task.Run(() => _importer.PrepareAsync(target, source, token), token);
                if (!ReferenceEquals(_context.Tournament, target))
                    return new ManualImportResult(ManualImportStatus.TargetChanged, ImportResult.Error());
                if (plan == null) return new ManualImportResult(ManualImportStatus.Completed, ImportResult.Error());
                if (!plan.NeedsApply)
                    return new ManualImportResult(ManualImportStatus.Completed, new ImportResult(plan.ShortCircuit ?? ImportOutcome.Error, 0));

                var preview = _importer.Apply(TournamentImporter.CloneForImportPreview(target), plan);
                if (!HasChanges(preview))
                    return await FinishAsync(target, preview, null, token);

                var backup = Path.Combine(_backupRoot, target.ID?.ToString("N") ?? "legacy",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N") + ".wrt");
                // Snapshot before awaiting I/O; saving a copy must never change
                // the live tournament's FileName or serve the backup to peers.
                var snapshot = _adapter.GetInfoFromEntity(target);
                var backedUp = await Task.Run(async () =>
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        return await _dataAccess.SaveToFileAsync(snapshot, backup, token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException
                        or ArgumentException or NotSupportedException)
                    {
                        FileLogger.Log("ManualImport.backup", backup, ex);
                        return false;
                    }
                }, token);
                if (!ReferenceEquals(_context.Tournament, target))
                    return new ManualImportResult(ManualImportStatus.TargetChanged, ImportResult.Error(), backup);
                if (!backedUp) return new ManualImportResult(ManualImportStatus.BackupFailed, ImportResult.Error());

                token.ThrowIfCancellationRequested();
                var result = _importer.Apply(target, plan);
                FileLogger.Log("ManualImport.apply", source, result.Outcome + ":" + result.ImportedCount);
                // Auto sync may have accepted the same changes while the copy
                // was being written. Do not retain a copy for a no-op import.
                if (!HasChanges(result))
                {
                    try { File.Delete(backup); backup = null; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
                    {
                        FileLogger.Log("ManualImport.backupCleanup", backup, ex);
                    }
                }
                return await FinishAsync(target, result, backup, token);
            }
            catch (OperationCanceledException) when (!ReferenceEquals(_context.Tournament, target))
            {
                return new ManualImportResult(ManualImportStatus.TargetChanged, ImportResult.Error());
            }
            finally
            {
                _context.TournamentChanged -= changed;
            }
        }

        private static bool HasChanges(ImportResult result) => result.ImportedCount > 0 || result.Outcome == ImportOutcome.Imported;

        private async Task<ManualImportResult> FinishAsync(Entities.Tournament target, ImportResult result, string backup, CancellationToken token)
        {
            if (HasChanges(result)) _results?.Recalculate(target);
            if (!HasChanges(result) && !ReferenceEquals(_pendingSaveTarget, target))
                return new ManualImportResult(ManualImportStatus.Completed, result, backup);

            _pendingSaveTarget = target;
            bool saved;
            try { saved = await _manager.SaveToFileAsync(target, target.FileName, token); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException
                or ArgumentException or NotSupportedException)
            {
                FileLogger.Log("ManualImport.save", target.FileName, ex);
                saved = false;
            }
            if (!ReferenceEquals(_context.Tournament, target))
                return new ManualImportResult(ManualImportStatus.TargetChanged, result, backup);
            if (!saved)
            {
                FileLogger.Log("ManualImport.save", target.FileName, "save returned false");
                return new ManualImportResult(ManualImportStatus.SaveFailed, result, backup);
            }
            _pendingSaveTarget = null;
            return new ManualImportResult(ManualImportStatus.Completed, result, backup, Saved: true);
        }
    }
}
