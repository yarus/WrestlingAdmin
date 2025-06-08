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
                AutosaveMaxSecond = info.AutosaveMaxSecond,
                IsTournamentScoreInternational = info.IsTournamentScoreInternational,
                IsOverlayOlympic = info.IsOverlayOlympic,
                IsVideoRecordingEnabled = info.IsVideoRecordingEnabled,
                VideoStoragePath = info.VideStoragePath
            };

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
                AutosaveMaxSecond = entity.AutosaveMaxSecond,
                IsTournamentScoreInternational = entity.IsTournamentScoreInternational,
                IsOverlayOlympic = entity.IsOverlayOlympic,
                IsVideoRecordingEnabled = entity.IsVideoRecordingEnabled,
                VideStoragePath = entity.VideoStoragePath
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

            var slides = info.Slides?.Select(GetEntityFromInfo).ToList() ?? new List<ScreenSlide>();

            var importSources = info.ImportSources?.ToList() ?? new List<string>();

            var wrestlersToDelete = new List<Wrestler>();

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
                        wrestlersToDelete.Add(wrestler);
                    }
                }
            }

            wrestlers.RemoveAll(p => wrestlersToDelete.Contains(p));

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
                ContactEmail = info.ContactEmail,
                ContactPhone = info.ContactPhone,
                ContactPerson = info.ContactPerson,
                ContactTitle = info.ContactTitle,
                ContactAddress = info.ContactAddress,
                StartDate = info.StartDate,
                Name = info.Name,
                Status = !string.IsNullOrEmpty(info.Status) ? (TournamentStatus)Enum.Parse(typeof(TournamentStatus), info.Status) : TournamentStatus.Fake,
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
                Slides = new ObservableCollection<ScreenSlide>(slides),
                HashTag = info.HashTag,
                MainJudgeEmail = info.MainJudgeEmail,
                MainJudgePhone = info.MainJudgePhone,
                MainSecretaryEmail = info.MainSecretaryEmail,
                MainSecretaryPhone = info.MainSecretaryPhone,
                EntryFee = info.EntryFee,
                ImportSources = new ObservableCollection<string>(importSources)
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
                Wrestlers = groupWrestlers
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
                    RoundType = !string.IsNullOrEmpty(p.RoundType) ? (GroupRoundTypeEnum)Enum.Parse(typeof(GroupRoundTypeEnum), p.RoundType) : GroupRoundTypeEnum.Main,
                    RoundMatches = p.RoundMatches.Select(m => GetEntityFromInfo(m, group, wrestlers)).ToList()
                }).ToList()
            };

            return bracket;
        }

        private WrestlingMatch GetEntityFromInfo(TournamentMatchInfo info, AgeWeightGroup group, List<Wrestler> wrestlers)
        {
            if (info == null) return null;

            return new WrestlingMatch
            {
                Status = (MatchStatusEnum)Enum.Parse(typeof(MatchStatusEnum), info.Status),
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
                StartDateTime = info.StartDateTime,
                RoundNumber = info.RoundNumber,
                MaxRoundSecond = info.MaxRoundSecond,
                MaxTimeoutSecond = info.MaxTimeoutSecond,
                MaxActionSecond = info.MaxActionSecond,
                GroupName = group.Name,
                WinType = !string.IsNullOrEmpty(info.WinType) ? (MatchWinTypeEnum)Enum.Parse(typeof(MatchWinTypeEnum), info.WinType) : (MatchWinTypeEnum?)null,
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
                ContactEmail = item.ContactEmail,
                ContactPhone = item.ContactPhone,
                ContactPerson = item.ContactPerson,
                ContactAddress = item.ContactAddress,
                ContactTitle = item.ContactTitle,
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
                Slides = item.Slides.Select(GetInfoFromEntity),
                HashTag = item.HashTag,
                MainJudgeEmail = item.MainJudgeEmail,
                MainJudgePhone = item.MainJudgePhone,
                MainSecretaryPhone = item.MainSecretaryPhone,
                MainSecretaryEmail = item.MainSecretaryEmail,
                EntryFee = item.EntryFee,
                ImportSources = item.ImportSources.ToList()
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
                IsFemale = group.IsFemale
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
