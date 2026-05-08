using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
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

        private readonly IMatchNumbersGenerator _matchNumbersGenerator;

        // Shared instance — HttpClient is designed for reuse across many calls
        // and would exhaust sockets if re-created per import tick.
        private readonly HttpClient _httpClient = new HttpClient { Timeout = HttpTimeout };

        public TournamentImporter(ITournamentsManager tournService, List<IGroupBracketProcessor> processors, IMatchNumbersGenerator matchNumbersGenerator)
        {
            _tournService = tournService;
            _drawTypes = processors;
            _matchNumbersGenerator = matchNumbersGenerator;
        }

        public async Task<ImportPlan> PrepareAsync(Entities.Tournament target, string fileName, CancellationToken cancellationToken = default)
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
                cancellationToken.ThrowIfCancellationRequested();

                var candidate = rawCandidate?.Trim();
                if (string.IsNullOrEmpty(candidate)) continue;

                var fetch = await FetchLocalCopyAsync(candidate, cancellationToken).ConfigureAwait(false);
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
        //
        // **W2.1**: 3 attempts with backoff 200ms→500ms→1.2s on transient
        // exceptions. Mirrors the retry policy already shipped for SMB reads
        // in JsonStorageDataAccess.ReadFromFileAsync — a peer mid-File.Replace
        // or a wifi micro-pause on a 5 sec timeout otherwise surfaces as
        // FileUnavailable and forces the operator to wait for the next
        // hash-divergence cycle. 4xx responses are NOT retried (404 = peer
        // serves a different tournament; retry is futile).
        private async Task<FetchOutcome> FetchLocalCopyAsync(string source, CancellationToken cancellationToken = default)
        {
            if (!IsHttpUri(source))
            {
                return new FetchOutcome(ok: true, localPath: source, isTemp: false);
            }

            const int maxAttempts = 3;
            int[] backoffMs = { 200, 500, 1200 };

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tempPath = Path.Combine(Path.GetTempPath(), "wrt-import-" + Guid.NewGuid().ToString("N") + ".wrt");
                try
                {
                    using (var response = await _httpClient.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            FileLogger.Log("Import.http", source,
                                "HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase + " (attempt " + attempt + "/" + maxAttempts + ", no retry on 4xx/5xx)");
                            return FetchOutcome.Failed;
                        }
                        using (var netStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var fileStream = File.Create(tempPath))
                        {
                            await netStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    return new FetchOutcome(ok: true, localPath: tempPath, isTemp: true);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Caller-driven cancellation — do not retry, do not swallow.
                    SafeDeleteTempFile(tempPath);
                    throw;
                }
                catch (Exception ex) when (IsTransientHttp(ex))
                {
                    SafeDeleteTempFile(tempPath);
                    FileLogger.Log("Import.http", source, "attempt " + attempt + "/" + maxAttempts + ": " + ex.GetType().Name + " " + ex.Message);
                    if (attempt < maxAttempts)
                    {
                        try
                        {
                            await Task.Delay(backoffMs[attempt - 1], cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        continue;
                    }
                    return FetchOutcome.Failed;
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Permission errors don't get better with retry.
                    SafeDeleteTempFile(tempPath);
                    FileLogger.Log("Import.http", source, ex);
                    return FetchOutcome.Failed;
                }
            }

            return FetchOutcome.Failed;
        }

        private static bool IsTransientHttp(Exception ex)
        {
            return ex is HttpRequestException
                || ex is TaskCanceledException
                || ex is IOException;
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
            bool structuralChange = false;

            // Wrestler sync runs FIRST so any new registrations (late entries
            // or duplicates created by a cross-group transfer) land in
            // target.Wrestlers before the per-group merge tries to resolve
            // bracket WrestlerInRed/WrestlerInBlue references against them.
            result += SyncWrestlers(target, tournament);

            // New carpets next — bare add (no version on Carpet). A group
            // arriving in this same Apply pass with CarpetID pointing at one
            // of these new carpets needs to find it in target.Carpets when
            // ApplyGroupFieldChanges / AddNewGroup wires up membership.
            foreach (var carpet in tournament.Carpets)
            {
                if (target.Carpets.Any(c => c.ID == carpet.ID)) continue;
                target.Carpets.Add(new Carpet { ID = carpet.ID, Name = carpet.Name });
                result++;
                structuralChange = true;
            }

            foreach (var group in tournament.Groups)
            {
                var localGroup = target.Groups.FirstOrDefault(g => g.ID == group.ID);
                if (localGroup == null)
                {
                    // New group from remote (secretary added a weight category
                    // mid-tournament, or this peer joined late and is hearing
                    // about a group it didn't know existed).
                    AddNewGroup(target, group);
                    result++;
                    structuralChange = true;
                    continue;
                }

                // Per-group structural merge — independent of match-completion
                // merge below. FieldsVersion covers timing/CarpetID/name/age/
                // weight changes that don't touch bracket shape; BracketVersion
                // fires only on Generate() and replaces bracket+wrestlers
                // wholesale. They're separate so a peer that completed matches
                // doesn't lose its work just because secretary tweaked timing.
                if (group.FieldsVersion > localGroup.FieldsVersion)
                {
                    ApplyGroupFieldChanges(target, localGroup, group);
                    result++;
                    structuralChange = true;
                }

                if (group.BracketVersion > localGroup.BracketVersion)
                {
                    ReplaceGroupBracket(target, localGroup, group);
                    result++;
                    structuralChange = true;
                }

                // Existing per-match merge — handles ordinary match completions
                // and reverts. Skips silently if structure inconsistent (a
                // first-tick from a peer with a wholly different bracket).
                if (localGroup.Bracket == null || group.Bracket == null) continue;
                if (localGroup.Bracket.BracketTypeCode != group.Bracket.BracketTypeCode) continue;

                var matches = localGroup.Bracket.Rounds.SelectMany(p => p.RoundMatches).ToList();
                var importedMatches = group.Bracket.Rounds.SelectMany(p => p.RoundMatches).ToList();

                if (matches.Count != importedMatches.Count) continue;

                foreach (var importedMatch in importedMatches)
                {
                    // Match identity by BracketFullNumber (stable per-group
                    // RoundNumber.BracketNumber pair). MatchNumber is per-
                    // carpet and gets renumbered when a group moves between
                    // carpets or a new group is added — using it as the merge
                    // key would silently match wrong matches across peers.
                    var baseMatch = matches.FirstOrDefault(p => p.BracketFullNumber == importedMatch.BracketFullNumber);
                    if (baseMatch == null) continue;

                    // Strict ">" — equal versions keep the local copy. This is
                    // the cheap escape hatch when two peers concurrently approve
                    // the same match: each keeps its own state, operators notice
                    // the divergence on the dashboard rather than one peer
                    // silently winning by import-tick race.
                    if (importedMatch.Version <= baseMatch.Version) continue;

                    var processor = GetProcessorForGroup(localGroup.Bracket.BracketTypeCode);
                    if (processor == null) throw new InvalidOperationException($"No processor registered for bracket type '{localGroup.Bracket.BracketTypeCode}'. Check DI registration in App.xaml.cs.");
                    processor.Load(target, localGroup);

                    // Three observable transitions when remote is strictly newer.
                    // The fourth (both Pending) bumps version only — bracket is
                    // already in the right state. See docs/TodoList.md #14.
                    if (baseMatch.Status == MatchStatusEnum.Pending && importedMatch.Status == MatchStatusEnum.Completed)
                    {
                        // Case 1: applied completion (Pending → Completed).
                        ApplyResultFields(baseMatch, importedMatch);
                        processor.CompleteMatch(baseMatch, baseMatch.IsRedWon, baseMatch.WinType.Value);
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
                        processor.CompleteMatch(baseMatch, baseMatch.IsRedWon, baseMatch.WinType.Value);
                        result++;
                    }
                    // else: both Pending — bracket already correct, no-op.

                    baseMatch.Version = importedMatch.Version;
                }
            }

            // Renumber per-carpet match numbers locally so peer's UI shows the
            // same MatchNumber sequence as secretary's after any structural
            // change (new group, carpet membership shift, bracket regen). The
            // generator is deterministic for identical Tournament state, so
            // peers converge to the same numbering without explicitly copying
            // MatchNumber across the wire.
            if (structuralChange && _matchNumbersGenerator != null)
            {
                _matchNumbersGenerator.Generate(target, _drawTypes);
            }

            return result > 0 ? ImportResult.Imported(result) : ImportResult.NoNewData();
        }

        // Adds wrestlers from remote that aren't yet in target (late
        // registrations, duplicates created during cross-group transfers) and
        // syncs name/team edits on existing ones via Timestamp. Only NEW
        // wrestler additions count toward the returned change count — name
        // edits via Sync are best-effort and don't trigger autosave on their
        // own (they piggyback on the next autosave-eligible event). This keeps
        // the no-op tick (peer re-announces an unchanged tournament) from
        // looping forever via Imported→autosave→re-import.
        private static int SyncWrestlers(Entities.Tournament target, Entities.Tournament remote)
        {
            int added = 0;
            foreach (var wrestler in remote.Wrestlers)
            {
                var local = target.Wrestlers.FirstOrDefault(x => x.ID == wrestler.ID);
                if (local == null)
                {
                    target.Wrestlers.Add(wrestler);
                    added++;
                    continue;
                }
                if (local.Timestamp >= wrestler.Timestamp) continue;
                local.Sync(wrestler);
            }
            return added;
        }

        // Adds a brand-new group from remote into target. Wrestler refs inside
        // the group's Wrestlers list and bracket matches are re-resolved
        // against target.Wrestlers so the group hooks into the live object
        // graph instead of dangling on remote-only Wrestler instances.
        private static void AddNewGroup(Entities.Tournament target, AgeWeightGroup remoteGroup)
        {
            remoteGroup.Wrestlers = ResolveAgainstTargetWrestlers(remoteGroup.Wrestlers, target);
            ResolveBracketWrestlerRefs(remoteGroup.Bracket, target);

            target.Groups.Add(remoteGroup);

            if (remoteGroup.CarpetID.HasValue)
            {
                var carpet = target.Carpets.FirstOrDefault(c => c.ID == remoteGroup.CarpetID.Value);
                if (carpet != null && !carpet.Groups.Contains(remoteGroup))
                {
                    carpet.Groups.Add(remoteGroup);
                }
            }
        }

        // Returns a list of Wrestler instances drawn from target.Wrestlers
        // for the IDs in `source`. Falls back to the source instance when no
        // local match exists (shouldn't happen in normal flow because
        // SyncWrestlers runs first, but keeps the graph populated under any
        // ordering).
        private static List<Wrestler> ResolveAgainstTargetWrestlers(IEnumerable<Wrestler> source, Entities.Tournament target)
        {
            return source
                .Select(rw => target.Wrestlers.FirstOrDefault(tw => tw.ID == rw.ID) ?? rw)
                .ToList();
        }

        // Re-points WrestlerInRed/WrestlerInBlue on every match in `bracket`
        // to the corresponding target.Wrestlers instances. Without this, after
        // we adopt remote's bracket the matches would reference remote-only
        // Wrestler objects while the rest of the live graph (target.Wrestlers,
        // group.Wrestlers) holds the local instances — bindings would diverge.
        private static void ResolveBracketWrestlerRefs(GroupBracket bracket, Entities.Tournament target)
        {
            if (bracket?.Rounds == null) return;
            foreach (var round in bracket.Rounds)
            {
                foreach (var m in round.RoundMatches)
                {
                    if (m.WrestlerInRed != null)
                    {
                        m.WrestlerInRed = target.Wrestlers.FirstOrDefault(w => w.ID == m.WrestlerInRed.ID) ?? m.WrestlerInRed;
                    }
                    if (m.WrestlerInBlue != null)
                    {
                        m.WrestlerInBlue = target.Wrestlers.FirstOrDefault(w => w.ID == m.WrestlerInBlue.ID) ?? m.WrestlerInBlue;
                    }
                }
            }
        }

        // Copies non-bracket fields from remote group to local. Cascades the
        // (possibly new) timing into local pending matches — same operation
        // DetailsViewModel.EditGroup does locally on the secretary's laptop —
        // so peers see the same end state. Bracket shape and Wrestlers list
        // are intentionally untouched: those travel through BracketVersion.
        private static void ApplyGroupFieldChanges(Entities.Tournament target, AgeWeightGroup local, AgeWeightGroup remote)
        {
            var oldCarpetId = local.CarpetID;

            local.MaxRoundSecond = remote.MaxRoundSecond;
            local.MaxTimeoutSecond = remote.MaxTimeoutSecond;
            local.MaxActionSecond = remote.MaxActionSecond;
            local.BirthYearMin = remote.BirthYearMin;
            local.BirthYearMax = remote.BirthYearMax;
            local.WeightMax = remote.WeightMax;
            local.IsFemale = remote.IsFemale;
            local.CarpetID = remote.CarpetID;
            local.CarpetLabel = remote.CarpetLabel;

            if (local.Bracket?.Rounds != null)
            {
                foreach (var round in local.Bracket.Rounds)
                {
                    if (round.RoundMatches == null) continue;
                    foreach (var match in round.RoundMatches)
                    {
                        if (match.Status == MatchStatusEnum.Completed) continue;
                        match.MaxRoundSecond = local.MaxRoundSecond;
                        match.MaxTimeoutSecond = local.MaxTimeoutSecond;
                        match.MaxActionSecond = local.MaxActionSecond;
                    }
                }
            }

            if (oldCarpetId != local.CarpetID)
            {
                if (oldCarpetId.HasValue)
                {
                    var oldCarpet = target.Carpets.FirstOrDefault(c => c.ID == oldCarpetId.Value);
                    oldCarpet?.Groups.Remove(local);
                }
                if (local.CarpetID.HasValue)
                {
                    var newCarpet = target.Carpets.FirstOrDefault(c => c.ID == local.CarpetID.Value);
                    if (newCarpet != null && !newCarpet.Groups.Contains(local))
                    {
                        newCarpet.Groups.Add(local);
                    }
                }
            }

            local.FieldsVersion = remote.FieldsVersion;
        }

        // Replaces local group's bracket and wrestlers list from remote, but
        // preserves any locally-newer match completions (other carpets that
        // had completed matches before the secretary's bracket regeneration
        // landed do not lose their work — match-Version > local takes precedence).
        // Wrestler instances are re-resolved against target.Wrestlers so the
        // bracket references the same Wrestler objects the rest of the live
        // tournament graph uses; otherwise WrestlerInRed/WrestlerInBlue would
        // dangle as remote-only instances.
        private void ReplaceGroupBracket(Entities.Tournament target, AgeWeightGroup local, AgeWeightGroup remote)
        {
            // Snapshot local match state by stable identity before we drop
            // the bracket. MatchNumber is per-carpet (renumbered on group move),
            // BracketFullNumber is stable as long as the bracket shape itself
            // didn't change for that match — exactly the matches we want to
            // preserve.
            var snapshot = new Dictionary<string, WrestlingMatch>();
            if (local.Bracket?.Rounds != null)
            {
                foreach (var round in local.Bracket.Rounds)
                {
                    foreach (var m in round.RoundMatches)
                    {
                        snapshot[m.BracketFullNumber] = m;
                    }
                }
            }

            local.Wrestlers = ResolveAgainstTargetWrestlers(remote.Wrestlers, target);
            local.Bracket = remote.Bracket;
            ResolveBracketWrestlerRefs(local.Bracket, target);

            // Re-apply any locally-newer match completions onto the new bracket.
            // A match identity that no longer exists in the new structure (e.g.
            // wrestler reassignment shifted positions) is silently dropped — the
            // secretary explicitly chose to regenerate, accepting the loss.
            if (local.Bracket?.Rounds != null && snapshot.Count > 0)
            {
                var processor = GetProcessorForGroup(local.Bracket.BracketTypeCode);
                bool processorLoaded = false;

                foreach (var round in local.Bracket.Rounds)
                {
                    foreach (var match in round.RoundMatches)
                    {
                        if (!snapshot.TryGetValue(match.BracketFullNumber, out var localOld)) continue;
                        if (localOld.Version <= match.Version) continue;

                        // Wrestler-pair safety: same BracketFullNumber after a
                        // bracket regen does NOT mean the position holds the
                        // same opponents. If admin shuffled wrestlers (new
                        // seeding, transfer in, etc.), re-applying the old
                        // result would credit A's prior win against B to the
                        // new match A-vs-X. Drop the old completion in that
                        // case — the secretary regenerated knowing the impact.
                        if (!SameWrestlerPair(localOld, match)) continue;

                        if (processor == null) break;
                        if (!processorLoaded)
                        {
                            processor.Load(target, local);
                            processorLoaded = true;
                        }

                        if (match.Status == MatchStatusEnum.Pending && localOld.Status == MatchStatusEnum.Completed)
                        {
                            ApplyResultFields(match, localOld);
                            processor.CompleteMatch(match, match.IsRedWon, match.WinType.Value);
                        }
                        else if (match.Status == MatchStatusEnum.Completed && localOld.Status == MatchStatusEnum.Pending)
                        {
                            processor.RevertMatch(match);
                        }
                        else if (match.Status == MatchStatusEnum.Completed && localOld.Status == MatchStatusEnum.Completed)
                        {
                            processor.RevertMatch(match);
                            ApplyResultFields(match, localOld);
                            processor.CompleteMatch(match, match.IsRedWon, match.WinType.Value);
                        }

                        match.Version = localOld.Version;
                    }
                }
            }

            local.BracketVersion = remote.BracketVersion;
        }

        private IGroupBracketProcessor GetProcessorForGroup(string processorType)
        {
            return _drawTypes.FirstOrDefault(p => p.Code == processorType);
        }

        // True iff both matches have the same WrestlerInRed/WrestlerInBlue
        // pair by ID (order-independent — a match that was A-red vs B-blue
        // is "the same" as A-blue vs B-red for purposes of preserving a
        // result like "A won by points 5-2"). Either side missing a wrestler
        // is treated as a non-match.
        private static bool SameWrestlerPair(WrestlingMatch a, WrestlingMatch b)
        {
            if (a.WrestlerInRed == null || a.WrestlerInBlue == null) return false;
            if (b.WrestlerInRed == null || b.WrestlerInBlue == null) return false;
            var aPair = new[] { a.WrestlerInRed.ID, a.WrestlerInBlue.ID };
            var bPair = new[] { b.WrestlerInRed.ID, b.WrestlerInBlue.ID };
            return (aPair[0] == bPair[0] && aPair[1] == bPair[1])
                || (aPair[0] == bPair[1] && aPair[1] == bPair[0]);
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
