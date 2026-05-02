using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Wrestling.Data;
using Wrestling.Entities;

namespace Wrestling.Providers
{
    public class EntityToInfoAdapter : IEntityToInfoAdapter
    {
        private static T ParseEnumOrDefault<T>(string value, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            return Enum.TryParse<T>(value, ignoreCase: false, out var parsed) ? parsed : fallback;
        }

        private static T? ParseNullableEnum<T>(string value) where T : struct
        {
            if (string.IsNullOrEmpty(value)) return null;
            return Enum.TryParse<T>(value, ignoreCase: false, out var parsed) ? parsed : (T?)null;
        }


        public GlobalSettings GetEntityFromInfo(GlobalSettingsInfo info)
        {
            if (info == null) return null;

            var entity = new GlobalSettings
            {
                MaxRoundSecond = info.MaxRoundSecond,
                MaxTimeoutSecond = info.MaxTimeoutSecond,
                MaxActionSecond = info.MaxActionSecond,
                IsTimerBackward = info.IsTimerBackward,
                IsSoundEnabled = info.IsSoundEnabled,
                EndGongSoundPath = info.EndGongSoundPath,
                SliderBackgroundImagePath = info.SliderBackgroundImagePath,
                StartGongSoundPath = info.StartGongSoundPath,
                SliderMaxSecond = info.SliderMaxSecond,
                SliderOpacityValue = info.SliderOpacityValue,
                IsAutosaveEnabled = info.IsAutosaveEnabled,
                IsOverlayOlympic = info.IsOverlayOlympic,
                IsBackupEnabled = info.IsBackupEnabled,
                MaxBackupCount = info.MaxBackupCount,
                BackupFolderPath = info.BackupFolderPath,
                DiscoveryPort = info.DiscoveryPort,
                IsHttpServerEnabled = info.IsHttpServerEnabled,
                HttpServerPort = info.HttpServerPort,
                NodeName = info.NodeName ?? string.Empty,
                SelfUncPath = info.SelfUncPath ?? string.Empty,
                AnnounceIpOverride = info.AnnounceIpOverride ?? string.Empty,
                SignatureFooterImagePath = info.SignatureFooterImagePath
            };

            // Legacy .wrt files saved before NodeName was an auto-default field
            // come back with NodeName="". Without a NodeName the laptop is
            // invisible to peers — fall back to MachineName so the user is
            // discoverable out of the box and can rename later in Settings.
            if (string.IsNullOrWhiteSpace(entity.NodeName))
            {
                try { entity.NodeName = Environment.MachineName ?? string.Empty; } catch { entity.NodeName = string.Empty; }
            }

            // Legacy .wrt files saved before the backup feature shipped don't
            // carry MaxBackupCount. The GlobalSettingsInfo constructor seeds
            // 10 for newly-constructed instances, but pre-feature JSON that
            // explicitly serialized MaxBackupCount = 0 is indistinguishable
            // from "missing". Treat 0 as "use default" to be safe.
            if (entity.MaxBackupCount <= 0) entity.MaxBackupCount = 10;

            // Same kind of legacy-zero concern: .wrt files saved before the
            // network feature shipped can deserialize with DiscoveryPort = 0 /
            // HttpServerPort = 0 (field missing → DTO ctor seeds defaults, but
            // a pre-feature file that ever serialized them as zero is possible
            // in theory). Re-seed to defaults so listeners start on usable ports.
            if (entity.DiscoveryPort <= 0) entity.DiscoveryPort = 24565;
            if (entity.HttpServerPort <= 0) entity.HttpServerPort = 24566;

            return entity;
        }

        private GlobalSettingsInfo GetInfoFromEntity(GlobalSettings entity)
        {
            if (entity == null) return null;

            var info = new GlobalSettingsInfo
            {
                MaxRoundSecond = entity.MaxRoundSecond,
                MaxTimeoutSecond = entity.MaxTimeoutSecond,
                MaxActionSecond = entity.MaxActionSecond,
                IsTimerBackward = entity.IsTimerBackward,
                IsSoundEnabled = entity.IsSoundEnabled,
                EndGongSoundPath = entity.EndGongSoundPath,
                SliderBackgroundImagePath = entity.SliderBackgroundImagePath,
                StartGongSoundPath = entity.StartGongSoundPath,
                SliderMaxSecond = entity.SliderMaxSecond,
                SliderOpacityValue = entity.SliderOpacityValue,
                IsAutosaveEnabled = entity.IsAutosaveEnabled,
                IsOverlayOlympic = entity.IsOverlayOlympic,
                IsBackupEnabled = entity.IsBackupEnabled,
                MaxBackupCount = entity.MaxBackupCount,
                BackupFolderPath = entity.BackupFolderPath,
                DiscoveryPort = entity.DiscoveryPort,
                IsHttpServerEnabled = entity.IsHttpServerEnabled,
                HttpServerPort = entity.HttpServerPort,
                NodeName = entity.NodeName ?? string.Empty,
                SelfUncPath = entity.SelfUncPath ?? string.Empty,
                AnnounceIpOverride = entity.AnnounceIpOverride ?? string.Empty,
                SignatureFooterImagePath = entity.SignatureFooterImagePath
            };

            return info;
        }

        public Tournament GetEntityFromInfo(TournamentInfo info)
        {
            if (info == null) return null;

            var settings = GetEntityFromInfo(info.Settings);

            var wrestlers = info.Wrestlers?.Select(GetEntityFromInfo).ToList() ?? new List<Wrestler>();

            var applications = info.TeamApplications?.Select(a => GetEntityFromInfo(a)).ToList() ?? new List<TeamApplication>();

            var groups = info.Groups?.Select(g => GetEntityFromInfo(g, wrestlers)).ToList() ?? new List<AgeWeightGroup>();

            var carpets = info.Carpets?.Select(c => GetEntityFromInfo(c, groups)).ToList() ?? new List<Carpet>();

            var slideChannels = info.SlideChannels?.Select(GetEntityFromInfo).ToList() ?? new List<SlideChannel>();

            // Legacy .wrt files serialize a flat `Slides` list instead of channels.
            // When SlideChannels is empty and Slides is not, migrate into one
            // default channel so old tournaments keep their slides on load.
            if (slideChannels.Count == 0 && info.Slides != null)
            {
                var legacy = info.Slides.Select(GetEntityFromInfo).ToList();
                if (legacy.Count > 0)
                {
                    slideChannels.Add(new SlideChannel
                    {
                        Name = "Основной",
                        Slides = new ObservableCollection<ScreenSlide>(legacy)
                    });
                }
            }

            foreach (var wrestler in wrestlers)
            {
                if (wrestler.GroupID.HasValue)
                {
                    var group = groups.FirstOrDefault(g => g.ID == wrestler.GroupID);
                    if (group != null)
                    {
                        wrestler.GroupName = group.Name;
                    }
                    else
                    {
                        wrestler.GroupID = null;
                    }
                }

                if (wrestler.TeamID.HasValue)
                {
                    var teamApp = applications.FirstOrDefault(a => a.ID == wrestler.TeamID);
                    if (teamApp != null)
                    {
                        wrestler.TeamName = teamApp.ShortName;
                        wrestler.TeamCity = teamApp.City;
                    }
                    else
                    {
                        // Team reference couldn't be resolved (file mismatch, deleted team).
                        // Keep the wrestler and clear the dangling link rather than dropping
                        // registration data.
                        wrestler.TeamID = null;
                        wrestler.TeamName = null;
                        wrestler.TeamCity = null;
                    }
                }
            }

            foreach (var ageWeightGroup in groups)
            {
                if (ageWeightGroup.CarpetID.HasValue)
                {
                    var carpet = carpets.FirstOrDefault(c => c.ID == ageWeightGroup.CarpetID);
                    if (carpet != null)
                    {
                        ageWeightGroup.CarpetLabel = carpet.Name;
                    }
                }
            }

            var tournEntity = new Tournament(settings)
            {
                ID = info.ID,
                StartDate = info.StartDate,
                Name = info.Name,
                Status = ParseEnumOrDefault(info.Status, TournamentStatus.Fake),
                MainSecretary = info.MainSecretary,
                MainJudge = info.MainJudge,
                Country = info.Country,
                City = info.City,
                Address = info.Address,
                TeamApplications = new ObservableCollection<TeamApplication>(applications),
                Groups = new ObservableCollection<AgeWeightGroup>(groups),
                Wrestlers = new ObservableCollection<Wrestler>(wrestlers),
                Settings = settings,
                Carpets = new ObservableCollection<Carpet>(carpets),
                SlideChannels = new ObservableCollection<SlideChannel>(slideChannels),
                HashTag = info.HashTag,
                MainJudgeEmail = info.MainJudgeEmail,
                MainJudgePhone = info.MainJudgePhone,
                MainSecretaryEmail = info.MainSecretaryEmail,
                MainSecretaryPhone = info.MainSecretaryPhone,
                EntryFee = info.EntryFee
            };

            if (!tournEntity.ID.HasValue)
            {
                tournEntity.ID = Guid.NewGuid();
            }

            return tournEntity;
        }

        private ScreenSlide GetEntityFromInfo(ScreenSlideInfo info)
        {
            if (info == null) return null;

            var entity = new ScreenSlide
            {
                Title = info.Title,
                Duration = info.Duration,
                SlideType = info.SlideType,
                NamedValues = info.NamedValues
            };

            return entity;
        }

        private SlideChannel GetEntityFromInfo(SlideChannelInfo info)
        {
            if (info == null) return null;

            var slides = info.Slides?.Select(GetEntityFromInfo).ToList() ?? new List<ScreenSlide>();

            return new SlideChannel
            {
                Name = info.Name,
                SliderMaxSecond = info.SliderMaxSecond,
                Slides = new ObservableCollection<ScreenSlide>(slides)
            };
        }

        private Carpet GetEntityFromInfo(CarpetInfo info, IEnumerable<AgeWeightGroup> groups)
        {
            if (info == null) return null;

            var entity = new Carpet
            {
                ID = info.ID,
                Name = info.Name,
                Groups = new ObservableCollection<AgeWeightGroup>(groups.Where(g => info.Groups.Contains(g.ID)))
            };

            return entity;
        }

        public TeamApplication GetEntityFromInfo(TeamApplicationInfo info)
        {
            if (info == null) return null;

            var entity = new TeamApplication
            {
                City = info.City,
                Country = info.Country,
                Email = info.Email,
                EmblemPath = info.EmblemPath,
                FullAddress = info.FullAddress,
                FullName = info.FullName,
                ID = info.ID,
                MainCoach = info.MainCoach,
                PhoneNumber = info.PhoneNumber,
                Representative = info.Representative,
                ShortName = info.ShortName,
                HashTag = info.HashTag
            };

            return entity;
        }

        private AgeWeightGroup GetEntityFromInfo(AgeWeightGroupInfo info, IEnumerable<Wrestler> wrestlers)
        {
            if (info == null) return null;

            var groupWrestlers = wrestlers.Where(w => info.Wrestlers.Contains(w.ID)).ToList();

            var entity = new AgeWeightGroup
            {
                MaxActionSecond = info.MaxActionSecond,
                MaxRoundSecond = info.MaxRoundSecond,
                MaxTimeoutSecond = info.MaxTimeoutSecond,
                CarpetLabel = info.CarpetLabel,
                CarpetID = info.CarpetID,
                BirthYearMax = info.BirthYearMax,
                BirthYearMin = info.BirthYearMin,
                ID = info.ID,
                IsFemale = info.IsFemale,
                WeightMax = info.WeightMax,
                Wrestlers = groupWrestlers,
                FieldsVersion = info.FieldsVersion,
                BracketVersion = info.BracketVersion
            };

            entity.Bracket = GetEntityFromInfo(info.Bracket, entity, groupWrestlers);

            return entity;
        }

        public Wrestler GetEntityFromInfo(WrestlerInfo info)
        {
            if (info == null) return null;

            var wrestler = new Wrestler
            {
                ID = info.ID,
                FinalPlace = info.FinalPlace,
                LastName = info.LastName,
                BirthDate = info.BirthDate,
                FirstName = info.FirstName,
                MiddleName = info.MiddleName,
                Weight = info.Weight,
                IsFemale = info.IsFemale,
                IsSeedFixed = info.IsSeedFixed,
                SeedNumber = info.SeedNumber,
                GroupID = info.GroupID,
                IsEntryFeePaid = info.IsEntryFeePaid,
                TeamID = info.TeamID,
                HashTag = info.HashTag,
                Level = info.Level,
                PaidAmount = info.PaidAmount,
                IsWeightApproved = info.IsWeightApproved,
                Timestamp = info.Timestamp
            };

            return wrestler;
        }

        private GroupBracket GetEntityFromInfo(GroupBracketInfo bracketInfo, AgeWeightGroup group, List<Wrestler> wrestlers)
        {
            if (bracketInfo == null) return null;

            var bracket = new GroupBracket
            {
                BracketTypeLabel = bracketInfo.BracketTypeLabel,
                BracketTypeCode = bracketInfo.BracketTypeCode,
                WrestlersCount = bracketInfo.WrestlersCount,
                CompletedMatchesCount = bracketInfo.CompletedMatchesCount,
                MatchesCount = bracketInfo.MatchesCount,
                Rounds = bracketInfo.Rounds.Select(p => new GroupRound
                {
                    RoundNumber = p.RoundNumber,
                    RoundName = p.RoundName,
                    RoundType = ParseEnumOrDefault(p.RoundType, GroupRoundTypeEnum.Main),
                    RoundMatches = p.RoundMatches.Select(m => GetEntityFromInfo(m, group, wrestlers)).ToList()
                }).ToList()
            };

            return bracket;
        }

        private WrestlingMatch GetEntityFromInfo(TournamentMatchInfo info, AgeWeightGroup group, List<Wrestler> wrestlers)
        {
            if (info == null) return null;

            var status = ParseEnumOrDefault(info.Status, MatchStatusEnum.Pending);
            // Legacy migration: pre-version .wrt files have info.Version == 0
            // for every match. Bumping legacy Completed matches to V=1 prevents
            // an accidental local Approve (V=0→V=1) on one peer from auto-
            // overwriting an already-completed match on a sibling peer who is
            // still on V=0 — equal versions keep local, so legacy completions
            // are protected by the symmetric ceiling.
            var version = info.Version;
            if (version == 0 && status == MatchStatusEnum.Completed) version = 1;

            return new WrestlingMatch
            {
                Status = status,
                WrestlerInBlue = info.WrestlerInBlue.HasValue ? wrestlers.FirstOrDefault(w => w.ID == info.WrestlerInBlue.Value) : null,
                WrestlerInRed = info.WrestlerInRed.HasValue ? wrestlers.FirstOrDefault(w => w.ID == info.WrestlerInRed.Value) : null,
                MatchNumber = info.MatchNumber,
                Note = info.Note,
                IsRedWon = info.IsRedWon,
                LastSecondInMatch = info.LastSecondInMatch,
                PointsBlue = info.PointsBlue,
                PointsRed = info.PointsRed,
                WarningsNumberBlue = info.WarningsNumberBlue,
                WarningsNumberRed = info.WarningsNumberRed,
                Version = version,
                StartDateTime = info.StartDateTime,
                RoundNumber = info.RoundNumber,
                MaxRoundSecond = info.MaxRoundSecond,
                MaxTimeoutSecond = info.MaxTimeoutSecond,
                MaxActionSecond = info.MaxActionSecond,
                GroupName = group.Name,
                WinType = ParseNullableEnum<MatchWinTypeEnum>(info.WinType),
                RoundName = info.RoundName,
                MatchActions = info.MatchActions == null ? new List<MatchAction>() : info.MatchActions?.Select(m => new MatchAction
                {
                    RoundNumber = m.RoundNumber,
                    DateTime = m.DateTime,
                    Text = m.Text,
                    SecondInRound = m.SecondInRound,
                    IsForRed = m.IsForRed,
                    Points = m.Points
                }).ToList(),
                GroupID = group.ID,
                BracketNumber = info.BracketNumber,
                NextMatchBracketFullNumber = info.NextMatchBracketFullNumber
            };
        }

        public TournamentInfo GetInfoFromEntity(Tournament item)
        {
            if (item == null) return null;

            var settings = GetInfoFromEntity(item.Settings);

            if (!item.ID.HasValue)
            {
                item.ID = Guid.NewGuid();
            }

            var info = new TournamentInfo
            {
                ID = item.ID.Value,
                Name = item.Name,
                StartDate = item.StartDate,
                Status = item.Status.ToString(),
                Groups = item.Groups.Select(GetInfoFromEntity),
                MainJudge = item.MainJudge,
                MainSecretary = item.MainSecretary,
                Address = item.Address,
                Country = item.Country,
                City = item.City,
                Settings = settings,
                TeamApplications = item.TeamApplications.Select(GetInfoFromEntity),
                Wrestlers = item.Wrestlers.Select(GetInfoFromEntity),
                Carpets = item.Carpets.Select(GetInfoFromEntity),
                SlideChannels = item.SlideChannels.Select(GetInfoFromEntity),
                HashTag = item.HashTag,
                MainJudgeEmail = item.MainJudgeEmail,
                MainJudgePhone = item.MainJudgePhone,
                MainSecretaryPhone = item.MainSecretaryPhone,
                MainSecretaryEmail = item.MainSecretaryEmail,
                EntryFee = item.EntryFee
            };

            if (!info.ID.HasValue)
            {
                info.ID = Guid.NewGuid();
            }

            return info;
        }

        private ScreenSlideInfo GetInfoFromEntity(ScreenSlide entity)
        {
            if (entity == null) return null;

            var info = new ScreenSlideInfo
            {
                Title = entity.Title,
                NamedValues = entity.NamedValues,
                SlideType = entity.SlideType,
                Duration = entity.Duration
            };

            return info;
        }

        private SlideChannelInfo GetInfoFromEntity(SlideChannel entity)
        {
            if (entity == null) return null;

            return new SlideChannelInfo
            {
                Name = entity.Name,
                SliderMaxSecond = entity.SliderMaxSecond,
                Slides = entity.Slides?.Select(GetInfoFromEntity).ToList()
            };
        }

        private CarpetInfo GetInfoFromEntity(Carpet entity)
        {
            if (entity?.ID == null) return null;

            var info = new CarpetInfo
            {
                ID = entity.ID.Value,
                Name = entity.Name,
                Groups = entity.Groups.Select(g => g.ID)
            };

            return info;
        }

        public TeamApplicationInfo GetInfoFromEntity(TeamApplication entity)
        {
            if (entity == null) return null;

            var info = new TeamApplicationInfo
            {
                City = entity.City,
                Country = entity.Country,
                Email = entity.Email,
                EmblemPath = entity.EmblemPath,
                FullAddress = entity.FullAddress,
                FullName = entity.FullName,
                HashTag = entity.HashTag,
                ID = entity.ID,
                MainCoach = entity.MainCoach,
                PhoneNumber = entity.PhoneNumber,
                Representative = entity.Representative,
                ShortName = entity.ShortName,
            };

            return info;
        }

        private AgeWeightGroupInfo GetInfoFromEntity(AgeWeightGroup group)
        {
            if (group == null) return null;

            var groupInfo = new AgeWeightGroupInfo
            {
                Bracket = GetInfoFromEntity(group.Bracket),
                MaxActionSecond = group.MaxActionSecond,
                MaxRoundSecond = group.MaxRoundSecond,
                MaxTimeoutSecond = group.MaxTimeoutSecond,
                Wrestlers = new List<Guid>(group.Wrestlers.Select(w => w.ID)),
                CarpetLabel = group.CarpetLabel,
                CarpetID = group.CarpetID,
                ID = group.ID,
                BirthYearMax = group.BirthYearMax,
                BirthYearMin = group.BirthYearMin,
                WeightMax = group.WeightMax,
                IsFemale = group.IsFemale,
                FieldsVersion = group.FieldsVersion,
                BracketVersion = group.BracketVersion
            };

            return groupInfo;
        }

        public WrestlerInfo GetInfoFromEntity(Wrestler entity)
        {
            if (entity == null) return null;

            var info = new WrestlerInfo
            {
                ID = entity.ID,
                BirthDate = entity.BirthDate,
                LastName = entity.LastName,
                FirstName = entity.FirstName,
                MiddleName = entity.MiddleName,
                Weight = entity.Weight,
                FinalPlace = entity.FinalPlace,
                IsSeedFixed = entity.IsSeedFixed,
                SeedNumber = entity.SeedNumber,
                IsFemale = entity.IsFemale,
                GroupID = entity.GroupID,
                IsEntryFeePaid = entity.IsEntryFeePaid,
                TeamID = entity.TeamID,
                PaidAmount = entity.PaidAmount,
                IsWeightApproved = entity.IsWeightApproved,
                HashTag = entity.HashTag,
                Level = entity.Level,
                Timestamp = entity.Timestamp
            };

            return info;
        }

        private GroupBracketInfo GetInfoFromEntity(GroupBracket bracket)
        {
            if (bracket == null) return null;

            var bracketInfo = new GroupBracketInfo
            {
                BracketTypeLabel = bracket.BracketTypeLabel,
                BracketTypeCode = bracket.BracketTypeCode,
                WrestlersCount = bracket.WrestlersCount,
                CompletedMatchesCount = bracket.CompletedMatchesCount,
                MatchesCount = bracket.MatchesCount,
                Rounds = bracket.Rounds.Select(GetInfoFromEntity).ToList()
            };

            return bracketInfo;
        }

        private GroupRoundInfo GetInfoFromEntity(GroupRound entity)
        {
            if (entity == null) return null;

            var info = new GroupRoundInfo
            {
                RoundNumber = entity.RoundNumber,
                RoundName = entity.RoundName,
                RoundType = entity.RoundType.ToString(),
                RoundMatches = entity.RoundMatches.Select(GetInfoFromEntity).ToList()
            };

            return info;
        }

        private TournamentMatchInfo GetInfoFromEntity(WrestlingMatch entity)
        {
            if (entity == null) return null;

            var info = new TournamentMatchInfo
            {
                Status = entity.Status.ToString(),
                WrestlerInBlue = entity.WrestlerInBlue?.ID,
                WrestlerInRed = entity.WrestlerInRed?.ID,
                MatchNumber = entity.MatchNumber,
                Note = entity.Note,
                IsRedWon = entity.IsRedWon,
                LastSecondInMatch = entity.LastSecondInMatch,
                PointsBlue = entity.PointsBlue,
                PointsRed = entity.PointsRed,
                WarningsNumberRed = entity.WarningsNumberRed,
                WarningsNumberBlue = entity.WarningsNumberBlue,
                Version = entity.Version,
                StartDateTime = entity.StartDateTime,
                WinType = entity.WinType.ToString(),
                RoundNumber = entity.RoundNumber,
                MaxRoundSecond = entity.MaxRoundSecond,
                MaxTimeoutSecond = entity.MaxTimeoutSecond,
                MaxActionSecond = entity.MaxActionSecond,
                RoundName = entity.RoundName,
                MatchActions = entity.MatchActions?.Select(GetInfoFromEntity),
                BracketNumber = entity.BracketNumber,
                NextMatchBracketFullNumber = entity.NextMatchBracketFullNumber
            };

            return info;
        }

        private MatchActionInfo GetInfoFromEntity(MatchAction entity)
        {
            if (entity == null) return null;

            var info = new MatchActionInfo
            {
                RoundNumber = entity.RoundNumber,
                DateTime = entity.DateTime,
                Text = entity.Text,
                SecondInRound = entity.SecondInRound,
                IsForRed = entity.IsForRed,
                Points = entity.Points
            };

            return info;
        }
    }
}
