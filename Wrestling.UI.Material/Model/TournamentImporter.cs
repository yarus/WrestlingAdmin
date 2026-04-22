using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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
                if (!fetch.Ok) continue;

                try
                {
                    var tournament = await _tournService.LoadFromFileAsync(fetch.LocalPath).ConfigureAwait(false);
                    if (tournament == null) continue;

                    if (tournament.Name != target.Name ||
                        tournament.Groups.Count != target.Groups.Count ||
                        tournament.StartDate != target.StartDate)
                    {
                        // Remember we saw at least one candidate that loaded
                        // but pointed at the wrong tournament — preserve that
                        // outcome for the final result if no sibling succeeds.
                        mismatchSeen = true;
                        continue;
                    }

                    return ImportPlan.Proceed(tournament);
                }
                finally
                {
                    if (fetch.IsTemp) SafeDeleteTempFile(fetch.LocalPath);
                }
            }

            return ImportPlan.Skip(mismatchSeen ? ImportOutcome.TournamentMismatch : ImportOutcome.FileUnavailable);
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
            catch (HttpRequestException) { SafeDeleteTempFile(tempPath); return FetchOutcome.Failed; }
            catch (TaskCanceledException) { SafeDeleteTempFile(tempPath); return FetchOutcome.Failed; }
            catch (IOException) { SafeDeleteTempFile(tempPath); return FetchOutcome.Failed; }
            catch (UnauthorizedAccessException) { SafeDeleteTempFile(tempPath); return FetchOutcome.Failed; }
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
                    if (baseMatch != null && baseMatch.Status == MatchStatusEnum.Pending && importedMatch.Status == MatchStatusEnum.Completed)
                    {
                        baseMatch.WinType = importedMatch.WinType;
                        baseMatch.LastSecondInMatch = importedMatch.LastSecondInMatch;
                        baseMatch.PointsBlue = importedMatch.PointsBlue;
                        baseMatch.PointsRed = importedMatch.PointsRed;
                        baseMatch.WarningsNumberBlue = importedMatch.WarningsNumberBlue;
                        baseMatch.WarningsNumberRed = importedMatch.WarningsNumberRed;
                        baseMatch.IsRedWon = importedMatch.IsRedWon;
                        baseMatch.Note = importedMatch.Note;
                        baseMatch.MatchActions = new List<MatchAction>(importedMatch.MatchActions);

                        var processor = GetProcessorForGroup(sameGroup.Bracket.BracketTypeCode);
                        if (processor == null) throw new ApplicationException("Can't find processor!");

                        processor.Load(target, sameGroup);
                        processor.CompleteMatch(baseMatch, baseMatch.IsRedWon.Value, baseMatch.WinType.Value);

                        result++;
                    }
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
    }
}
