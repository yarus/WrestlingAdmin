using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Wrestling.DataAccess;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;
using Wrestling.Providers;

namespace Wrestling.UI.Material.Model
{
    public class TournamentImporter : ITournamentImporter
    {
        // Matches the 5 s timeout used for UNC paths in JsonStorageDataAccess —
        // a tournament file is ~50-200 KB so network fetch should be well
        // inside this window on any reasonable LAN.
        private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

        // Peers often advertise two ways to reach the same .wrt (an HTTP URL
        // served by the embedded server, and a UNC path on a manually-set-up
        // SMB share). We pack both into a single ImportSources entry separated
        // by this character so removing a peer stays one action for the
        // operator; the importer tries each candidate in order and uses the
        // first that loads.
        public const char SourceAlternativesSeparator = '|';

        private readonly List<IGroupBracketProcessor> _drawTypes;

        private readonly ITournamentsManager _tournService;

        // Shared instance — HttpClient is designed for reuse across many calls
        // and would exhaust sockets if re-created per import tick.
        private readonly HttpClient _httpClient = new HttpClient { Timeout = HttpTimeout };

        public TournamentImporter(ITournamentsManager tournService, List<IGroupBracketProcessor> processors)
        {
            _tournService = tournService;
            _drawTypes = processors;
        }

        public async Task<ImportPlan> PrepareAsync(Entities.Tournament target, string fileName)
        {
            // Phase 1 — threadpool-safe. Loads + deserializes + runs the entity
            // adapter (the expensive 50-200ms CPU step) without touching the
            // target's live ObservableCollections. The caller is expected to
            // wrap this in Task.Run so the work happens off the UI thread.

            if (string.IsNullOrEmpty(fileName)) return ImportPlan.Skip(ImportOutcome.FileUnavailable);

            // Split the entry into alternative candidates — a discovered peer
            // typically hands us HTTP first and UNC second, so a flaky HTTP
            // server or firewall automatically falls back to the SMB share
            // without the operator noticing.
            var candidates = fileName.Split(SourceAlternativesSeparator);
            bool mismatchSeen = false;

            foreach (var rawCandidate in candidates)
            {
                var candidate = rawCandidate?.Trim();
                if (string.IsNullOrEmpty(candidate)) continue;

                var fetch = await FetchLocalCopyAsync(candidate).ConfigureAwait(false);
                if (!fetch.Ok)
                {
                    // FetchLocalCopyAsync logs the underlying exception itself;
                    // here we only record the candidate-level outcome so the
                    // operator can correlate "import failed for source X" in the
                    // ImportLog with a specific candidate that did not even
                    // produce a local file.
                    FileLogger.Log("Import.fetch", candidate, "fetch failed (see preceding entry for cause)");
                    continue;
                }

                try
                {
                    var tournament = await _tournService.LoadFromFileAsync(fetch.LocalPath).ConfigureAwait(false);
                    if (tournament == null)
                    {
                        // ReadFromFile already classified and logged the cause
                        // (Corrupt / AccessDenied / Transient / etc.); add the
                        // import-side breadcrumb so the data_log shows the
                        // chain "candidate → load returned null".
                        FileLogger.Log("Import.parse", candidate, "load returned null (see ReadFromFile entry for classification)");
                        continue;
                    }

                    // Identity check: prefer Tournament.ID (stable GUID assigned
                    // at creation, survives renames / date adjustments). Fall
                    // back to the legacy Name+Date+Groups.Count heuristic only
                    // when either side is missing an ID (very old .wrt files).
                    if (!IsSameTournament(tournament, target))
                    {
                        // Remember we saw at least one candidate that loaded
                        // but pointed at the wrong tournament — preserve that
                        // outcome for the final result if no sibling succeeds.
                        mismatchSeen = true;
                        FileLogger.Log("Import.match", candidate, FormatMismatchReason(tournament, target));
                        continue;
                    }

                    FileLogger.Log("Import.ok", candidate, "candidate accepted");

                    return ImportPlan.Proceed(tournament);
                }
                finally
                {
                    if (fetch.IsTemp) SafeDeleteTempFile(fetch.LocalPath);
                }
            }

            var finalOutcome = mismatchSeen ? ImportOutcome.TournamentMismatch : ImportOutcome.FileUnavailable;
            FileLogger.Log("Import.skip", fileName, "all candidates exhausted — outcome=" + finalOutcome);
            return ImportPlan.Skip(finalOutcome);
        }

        private static bool IsSameTournament(Entities.Tournament source, Entities.Tournament target)
        {
            if (source.ID.HasValue && target.ID.HasValue)
            {
                return source.ID.Value == target.ID.Value;
            }
            // Fallback for legacy files without an ID — keeps the original
            // heuristic so a brand-new tournament with no ID can still match
            // by Name + Date + GroupsCount during the same session.
            return source.Name == target.Name
                   && source.Groups.Count == target.Groups.Count
                   && source.StartDate == target.StartDate;
        }

        private static string FormatMismatchReason(Entities.Tournament source, Entities.Tournament target)
        {
            string srcId = source.ID?.ToString() ?? "<none>";
            string tgtId = target.ID?.ToString() ?? "<none>";
            if (source.ID.HasValue && target.ID.HasValue)
            {
                return $"id mismatch: source={srcId} target={tgtId}";
            }
            return $"heuristic mismatch (no IDs): srcName='{source.Name}' tgtName='{target.Name}'"
                   + $", srcGroups={source.Groups.Count} tgtGroups={target.Groups.Count}"
                   + $", srcStart={source.StartDate} tgtStart={target.StartDate}";
        }

        // Normalizes a source string (UNC path, absolute path, http/https URL)
        // into a local file path the tournaments manager can read. HTTP sources
        // are streamed into a temp file so the rest of the pipeline doesn't
        // need to know about the network origin.
        private async Task<FetchOutcome> FetchLocalCopyAsync(string source)
        {
            if (!IsHttpUri(source))
            {
                return new FetchOutcome(ok: true, localPath: source, isTemp: false);
            }

            var tempPath = Path.Combine(Path.GetTempPath(), "wrt-import-" + Guid.NewGuid().ToString("N") + ".wrt");
            try
            {
                using (var response = await _httpClient.GetAsync(source, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        FileLogger.Log("Import.http", source, "HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase);
                        return FetchOutcome.Failed;
                    }
                    using (var netStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var fileStream = File.Create(tempPath))
                    {
                        await netStream.CopyToAsync(fileStream).ConfigureAwait(false);
                    }
                }
                return new FetchOutcome(ok: true, localPath: tempPath, isTemp: true);
            }
            catch (HttpRequestException ex) { SafeDeleteTempFile(tempPath); FileLogger.Log("Import.http", source, ex); return FetchOutcome.Failed; }
            catch (TaskCanceledException ex) { SafeDeleteTempFile(tempPath); FileLogger.Log("Import.http", source, ex); return FetchOutcome.Failed; }
            catch (IOException ex) { SafeDeleteTempFile(tempPath); FileLogger.Log("Import.http", source, ex); return FetchOutcome.Failed; }
            catch (UnauthorizedAccessException ex) { SafeDeleteTempFile(tempPath); FileLogger.Log("Import.http", source, ex); return FetchOutcome.Failed; }
        }

        private static bool IsHttpUri(string source)
        {
            return source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static void SafeDeleteTempFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private readonly struct FetchOutcome
        {
            public bool Ok { get; }
            public string LocalPath { get; }
            public bool IsTemp { get; }
            public FetchOutcome(bool ok, string localPath, bool isTemp)
            {
                Ok = ok;
                LocalPath = localPath;
                IsTemp = isTemp;
            }
            public static FetchOutcome Failed => new FetchOutcome(false, null, false);
        }

        public ImportResult Apply(Entities.Tournament target, ImportPlan plan)
        {
            // Phase 2 — UI-thread. Walks the remote tournament loaded by
            // PrepareAsync and merges genuinely-new completions into the live
            // target via the bracket processors. Only the matches that actually
            // flipped from Pending to Completed touch ObservableCollection, so
            // the typical per-tick cost is < 10 ms even under a live round.

            if (plan == null) return new ImportResult(ImportOutcome.Error, 0);
            if (plan.ShortCircuit.HasValue) return new ImportResult(plan.ShortCircuit.Value, 0);
            if (plan.Remote == null) return new ImportResult(ImportOutcome.Error, 0);

            int result = 0;
            var tournament = plan.Remote;

            foreach (var group in tournament.Groups)
            {
                var sameGroup = target.Groups.FirstOrDefault(p => p.ID == group.ID
                                                                  && p.Bracket != null &&
                                                                  group.Bracket != null &&
                                                                  p.CarpetID == group.CarpetID &&
                                                                  p.Bracket.BracketTypeCode == group.Bracket.BracketTypeCode);
                if (sameGroup == null) continue;

                var matches = sameGroup.Bracket.Rounds.SelectMany(p => p.RoundMatches).ToList();
                var importedMatches = group.Bracket.Rounds.SelectMany(p => p.RoundMatches).ToList();

                if (matches.Count != importedMatches.Count) continue;

                foreach (var importedMatch in importedMatches)
                {
                    var baseMatch = matches.FirstOrDefault(p => p.MatchNumber == importedMatch.MatchNumber);
                    if (baseMatch == null) continue;

                    // Strict ">" — equal versions keep the local copy. This is
                    // the cheap escape hatch when two peers concurrently approve
                    // the same match: each keeps its own state, operators notice
                    // the divergence on the dashboard rather than one peer
                    // silently winning by import-tick race.
                    if (importedMatch.Version <= baseMatch.Version) continue;

                    var processor = GetProcessorForGroup(sameGroup.Bracket.BracketTypeCode);
                    if (processor == null) throw new ApplicationException("Can't find processor!");
                    processor.Load(target, sameGroup);

                    // Three observable transitions when remote is strictly newer.
                    // The fourth (both Pending) bumps version only — bracket is
                    // already in the right state. See docs/TodoList.md #14.
                    if (baseMatch.Status == MatchStatusEnum.Pending && importedMatch.Status == MatchStatusEnum.Completed)
                    {
                        // Case 1: applied completion (Pending → Completed).
                        ApplyResultFields(baseMatch, importedMatch);
                        processor.CompleteMatch(baseMatch, baseMatch.IsRedWon.Value, baseMatch.WinType.Value);
                        result++;
                    }
                    else if (baseMatch.Status == MatchStatusEnum.Completed && importedMatch.Status == MatchStatusEnum.Pending)
                    {
                        // Case 2: applied revert (Completed → Pending).
                        processor.RevertMatch(baseMatch);
                        result++;
                    }
                    else if (baseMatch.Status == MatchStatusEnum.Completed && importedMatch.Status == MatchStatusEnum.Completed)
                    {
                        // Case 3: applied edit on the author side (revert + re-
                        // approve between our ticks). Roll the local bracket
                        // back to clean Pending, then apply the new completion.
                        processor.RevertMatch(baseMatch);
                        ApplyResultFields(baseMatch, importedMatch);
                        processor.CompleteMatch(baseMatch, baseMatch.IsRedWon.Value, baseMatch.WinType.Value);
                        result++;
                    }
                    // else: both Pending — bracket already correct, no-op.

                    baseMatch.Version = importedMatch.Version;
                }
            }

            // Sync wrestler info (supporting changing of names)
            foreach (var wrestler in tournament.Wrestlers)
            {
                var changedWrestler = target.Wrestlers.FirstOrDefault(x => x.ID == wrestler.ID);

                if (changedWrestler == null || changedWrestler.Timestamp >= wrestler.Timestamp) continue;

                changedWrestler.Sync(wrestler);
            }

            return result > 0 ? ImportResult.Imported(result) : ImportResult.NoNewData();
        }

        private IGroupBracketProcessor GetProcessorForGroup(string processorType)
        {
            return _drawTypes.FirstOrDefault(p => p.Code == processorType);
        }

        // Copies the result-bearing fields the importer is responsible for
        // propagating. Keep this in sync with the version-bump triggers in
        // MatchResultsViewModel (ApproveAsync / RejectAsync) — adding a new
        // field here without bumping Version on local edits would mean peers
        // never see the change.
        private static void ApplyResultFields(WrestlingMatch dest, WrestlingMatch source)
        {
            dest.WinType = source.WinType;
            dest.LastSecondInMatch = source.LastSecondInMatch;
            dest.PointsBlue = source.PointsBlue;
            dest.PointsRed = source.PointsRed;
            dest.WarningsNumberBlue = source.WarningsNumberBlue;
            dest.WarningsNumberRed = source.WarningsNumberRed;
            dest.IsRedWon = source.IsRedWon;
            dest.Note = source.Note;
            dest.MatchActions = new List<MatchAction>(source.MatchActions);
        }
    }
}
