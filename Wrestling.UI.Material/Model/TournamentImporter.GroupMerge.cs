using System;
using System.Collections.Generic;
using System.Linq;
using Wrestling.Entities;
using Wrestling.Entities.Bracket;

namespace Wrestling.UI.Material.Model
{
    public partial class TournamentImporter
    {
        private sealed class CategoryConflictException : Exception
        {
            public CategoryConflictException(ImportConflict conflict) => Conflict = conflict;
            public ImportConflict Conflict { get; }
        }

        private static void Conflict(AgeWeightGroup group, WrestlingMatch match, ImportConflictReason reason) =>
            throw new CategoryConflictException(new ImportConflict(group.ID, group.Name, match?.MatchNumber, match?.BracketFullNumber, reason));

        private static Entities.Tournament CloneCategory(Entities.Tournament source, AgeWeightGroup group)
        {
            // Persistence adapters intentionally migrate/normalize legacy data.
            // A transaction snapshot must preserve the exact live state instead.
            var clone = new Entities.Tournament(source.Settings) { ID = source.ID, Name = source.Name, StartDate = source.StartDate };
            foreach (var w in source.Wrestlers) clone.Wrestlers.Add((Wrestler)w.Clone());
            var copied = CopyGroupForImport(group, clone);
            clone.Groups.Add(copied);
            foreach (var mat in source.Mats)
            {
                var copy = new Mat { ID = mat.ID, Name = mat.Name, FieldsVersion = mat.FieldsVersion, ActivePartID = mat.ActivePartID };
                if (mat.Groups.Any(g => g.ID == group.ID)) copy.Groups.Add(copied);
                clone.Mats.Add(copy);
            }
            return clone;
        }

        // Preview uses the exact live graph, without persistence migrations or
        // normalization that could hide exclusion repairs or other real changes.
        internal static Entities.Tournament CloneForImportPreview(Entities.Tournament source)
        {
            var clone = new Entities.Tournament(source.Settings)
            {
                ID = source.ID, Name = source.Name, StartDate = source.StartDate, MetaVersion = source.MetaVersion
            };
            foreach (var w in source.Wrestlers) clone.Wrestlers.Add((Wrestler)w.Clone());
            foreach (var team in source.TeamApplications) clone.TeamApplications.Add((TeamApplication)team.Clone());
            foreach (var part in source.Parts) clone.Parts.Add(new TournamentPart { ID = part.ID, Name = part.Name });
            foreach (var group in source.Groups) clone.Groups.Add(CopyGroupForImport(group, clone));
            foreach (var mat in source.Mats)
            {
                var copy = new Mat { ID = mat.ID, Name = mat.Name, FieldsVersion = mat.FieldsVersion, ActivePartID = mat.ActivePartID };
                foreach (var group in mat.Groups) copy.Groups.Add(clone.Groups.Single(g => g.ID == group.ID));
                clone.Mats.Add(copy);
            }
            return clone;
        }

        private static AgeWeightGroup CopyGroupForImport(AgeWeightGroup group, Entities.Tournament clone)
        {
            var copied = (AgeWeightGroup)group.Clone();
            copied.FieldsVersion = group.FieldsVersion;
            copied.BracketVersion = group.BracketVersion;
            copied.MatID = group.MatID;
            copied.PartID = group.PartID;
            copied.Wrestlers = ResolveAgainstTargetWrestlers(group.Wrestlers, clone);
            WrestlingMatch CopyMatch(WrestlingMatch m)
            {
                var copy = m.CaptureResult();
                copy.PreviousResults = m.PreviousResults.Select(CopyMatch).ToList();
                ResolveMatchWrestlerRefs(copy, clone);
                return copy;
            }
            copied.Bracket = group.Bracket == null ? null : new GroupBracket
            {
                BracketTypeCode = group.Bracket.BracketTypeCode,
                BracketTypeLabel = group.Bracket.BracketTypeLabel,
                MatchesCount = group.Bracket.MatchesCount,
                CompletedMatchesCount = group.Bracket.CompletedMatchesCount,
                WrestlersCount = group.Bracket.WrestlersCount,
                Rounds = group.Bracket.Rounds.Select(r => new GroupRound
                {
                    RoundNumber = r.RoundNumber, RoundName = r.RoundName, RoundType = r.RoundType,
                    RoundMatches = r.RoundMatches.Select(CopyMatch).ToList()
                }).ToList()
            };
            return copied;
        }

        private (int Count, bool Structural) MergeCategory(Entities.Tournament target, AgeWeightGroup local, AgeWeightGroup remote)
        {
            int count = 0;
            bool structural = false;
            if (remote.BracketVersion >= local.BracketVersion) ValidateCompletedMatches(remote);
            if (remote.FieldsVersion > local.FieldsVersion)
            {
                ApplyGroupFieldChanges(target, local, remote);
                count++;
                structural = true;
            }
            if (remote.BracketVersion > local.BracketVersion)
            {
                ReplaceGroupBracket(target, local, remote);
                count++;
                structural = true;
            }
            if (remote.BracketVersion < local.BracketVersion) return (count, structural);
            if (local.Bracket == null && remote.Bracket == null) return (count, structural);
            if (local.Bracket == null || remote.Bracket == null || local.Bracket.BracketTypeCode != remote.Bracket.BracketTypeCode)
                Conflict(local, null, ImportConflictReason.DifferentStructure);

            var matches = local.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToDictionary(m => m.BracketFullNumber);
            var incoming = remote.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToList();
            if (matches.Count != incoming.Count || incoming.Any(m => !matches.ContainsKey(m.BracketFullNumber)))
                Conflict(local, null, ImportConflictReason.DifferentStructure);

            var processor = GetProcessorForGroup(local.Bracket.BracketTypeCode);
            if (processor == null) Conflict(local, null, ImportConflictReason.DifferentStructure);
            processor.Load(target, local);
            foreach (var imported in incoming)
            {
                var match = matches[imported.BracketFullNumber];
                if (imported.SyncVersion < match.SyncVersion) continue;
                if (imported.SyncVersion == match.SyncVersion)
                {
                    // Pending slots can legitimately be filled by an earlier
                    // completion in this very packet. Completed equal-version
                    // results must agree; never silently pick an author.
                    if ((match.IsMatchCompleted || imported.IsMatchCompleted) && !SameResult(match, imported))
                        Conflict(local, match, ImportConflictReason.DifferentResult);
                    continue;
                }
                ApplyMatchUpdate(local, processor, match, imported);
                count++;
            }
            return (count, structural);
        }

        private static void ValidateCompletedMatches(AgeWeightGroup group)
        {
            if (group.Bracket == null) return;
            foreach (var match in group.Bracket.Rounds.SelectMany(r => r.RoundMatches).Where(m => m.IsMatchCompleted))
            {
                bool mutual = match.WinType is MatchWinTypeEnum.MutualDisqualify or MatchWinTypeEnum.MutualNoShow or MatchWinTypeEnum.MutualInjury;
                if (!match.WinType.HasValue || (!mutual && match.WinType != MatchWinTypeEnum.FreeWin && !match.IsRedWon.HasValue))
                    Conflict(group, match, ImportConflictReason.InvalidResult);
            }
        }

        private static bool SameSlots(WrestlingMatch a, WrestlingMatch b) =>
            a.WrestlerInRed?.ID == b.WrestlerInRed?.ID && a.WrestlerInBlue?.ID == b.WrestlerInBlue?.ID;

        private static bool SameResult(WrestlingMatch a, WrestlingMatch b) => SameSlots(a, b)
            && a.Status == b.Status && a.WinType == b.WinType && a.IsRedWon == b.IsRedWon
            && a.PointsRed == b.PointsRed && a.PointsBlue == b.PointsBlue
            && a.WarningsNumberRed == b.WarningsNumberRed && a.WarningsNumberBlue == b.WarningsNumberBlue
            && a.LastSecondInMatch == b.LastSecondInMatch && (a.Note ?? "") == (b.Note ?? "")
            && a.MatchActions.Count == b.MatchActions.Count
            && a.MatchActions.Zip(b.MatchActions).All(pair => pair.First.Type == pair.Second.Type
                && pair.First.Points == pair.Second.Points && pair.First.IsForRed == pair.Second.IsForRed
                && pair.First.WarningDelta == pair.Second.WarningDelta
                && pair.First.RoundNumber == pair.Second.RoundNumber && pair.First.SecondInRound == pair.Second.SecondInRound
                && pair.First.DateTime.ToUniversalTime() == pair.Second.DateTime.ToUniversalTime())
            && a.PreviousResults.Count == b.PreviousResults.Count
            && a.PreviousResults.Zip(b.PreviousResults).All(pair => SameResult(pair.First, pair.Second));

        private static bool SameAdvancement(WrestlingMatch a, WrestlingMatch b)
        {
            // Ordinary wins preserve both winner and loser feeds. Exclusion,
            // injury and mutual outcomes can change other matches or rebuild
            // medal rounds, so those transitions still go through processors.
            bool Ordinary(MatchWinTypeEnum? type) => type is MatchWinTypeEnum.Tushe
                or MatchWinTypeEnum.PointsWin or MatchWinTypeEnum.PointsWinWithPoints
                or MatchWinTypeEnum.DominationWin or MatchWinTypeEnum.DominationWinWithPoints
                or MatchWinTypeEnum.ActionWin;
            return a.IsRedWon == b.IsRedWon && (a.WinType == b.WinType || Ordinary(a.WinType) && Ordinary(b.WinType));
        }

        private static void ApplyMatchUpdate(AgeWeightGroup group, IGroupBracketProcessor processor, WrestlingMatch match, WrestlingMatch imported)
        {
            if (imported.IsMatchCompleted && !SameSlots(match, imported))
                Conflict(group, match, ImportConflictReason.DifferentParticipants);
            if (match.IsMatchCompleted && imported.IsMatchCompleted && SameAdvancement(match, imported))
            {
                ApplyResultFields(match, imported);
            }
            else
            {
                if (match.IsMatchCompleted)
                {
                    if (!processor.CanMatchBeReverted(match)) Conflict(group, match, ImportConflictReason.CompletedDependentMatch);
                    processor.RevertMatch(match);
                }
                if (imported.IsMatchCompleted)
                {
                    ApplyResultFields(match, imported);
                    processor.CompleteMatch(match, match.IsRedWon, match.WinType.Value);
                }
            }
            match.Version = imported.Version;
        }

        private void CommitCategory(Entities.Tournament target, AgeWeightGroup local, AgeWeightGroup staged)
        {
            if (staged.FieldsVersion > local.FieldsVersion) ApplyGroupFieldChanges(target, local, staged);
            if (staged.BracketVersion > local.BracketVersion)
            {
                local.Wrestlers = ResolveAgainstTargetWrestlers(staged.Wrestlers, target);
                local.Bracket = staged.Bracket;
                ResolveBracketWrestlerRefs(local.Bracket, target);
                local.BracketVersion = staged.BracketVersion;
            }
            else if (local.Bracket != null)
            {
                var stagedMatches = staged.Bracket.Rounds.SelectMany(r => r.RoundMatches).ToDictionary(m => m.BracketFullNumber);
                foreach (var match in local.Bracket.Rounds.SelectMany(r => r.RoundMatches))
                {
                    var source = stagedMatches[match.BracketFullNumber];
                    ApplyResultFields(match, source);
                    match.Status = source.Status;
                    match.Version = source.Version;
                    match.StartDateTime = source.StartDateTime;
                    match.BestActionRed = source.BestActionRed;
                    match.BestActionBlue = source.BestActionBlue;
                    match.BestActionRedCount = source.BestActionRedCount;
                    match.BestActionBlueCount = source.BestActionBlueCount;
                    match.IsLastActionRed = source.IsLastActionRed;
                    match.WrestlerInRed = source.WrestlerInRed;
                    match.WrestlerInBlue = source.WrestlerInBlue;
                    match.PreviousResults = source.PreviousResults;
                    ResolveMatchWrestlerRefs(match, target);
                }
                local.Bracket.CompletedMatchesCount = staged.Bracket.CompletedMatchesCount;
            }
            foreach (var source in staged.Wrestlers)
            {
                var w = target.Wrestlers.Single(w => w.ID == source.ID);
                w.IsNoShow = source.IsNoShow;
                w.IsDisqualified = source.IsDisqualified;
                w.FinalPlace = source.FinalPlace;
            }
        }
    }
}
