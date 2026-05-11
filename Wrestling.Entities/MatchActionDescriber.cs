using System;
using Wrestling.Entities.Localization;

namespace Wrestling.Entities
{
    // Single source of truth for the human-readable display text of a
    // MatchAction.
    // Used by:
    //   - EntityToInfoAdapter (entity→info) to populate MatchActionInfo.Text
    //     so old-version clients reading the .wrt still see meaningful entries.
    //   - The UI MatchActionDescriptionConverter to render the protocol column.
    //
    // The Russian fallback strings are intentionally close to the legacy text
    // so LegacyMatchActionTypeInferrer can round-trip an action through a
    // save cycle done by an old client without losing its type.
    //
    // Russian-language nuance: most phrases need case-agreement on the side
    // word ("Предупреждение красному" — dative). The helpers below return the
    // appropriate case form, looked up through EntityLocalization so English
    // (and any other language) can collapse all cases to a single invariant
    // word like "red" / "blue".
    public static class MatchActionDescriber
    {
        public static string Describe(MatchActionType type, bool? isForRed, int points)
        {
            switch (type)
            {
                case MatchActionType.SetPoints:
                    return string.Format(
                        EntityLocalization.T("Action_SetPoints", "{0} +{1} {2}"),
                        ColorLabel(isForRed), points, PluralizePoints(points));

                case MatchActionType.SetWarning:
                    return string.Format(
                        EntityLocalization.T("Action_SetWarning", "Предупреждение {0}"),
                        SideNameDative(isForRed));

                case MatchActionType.RevertPoints:
                    return string.Format(
                        EntityLocalization.T("Action_RevertPoints", "Коррекция -{0} {1}"),
                        points, SideNameDative(isForRed));

                case MatchActionType.RevertWarning:
                    return string.Format(
                        EntityLocalization.T("Action_RevertWarning", "Отмена предупреждения {0}"),
                        SideNameDative(isForRed));

                case MatchActionType.ShowActionTimer:
                    return points > 0
                        ? string.Format(
                            EntityLocalization.T("Action_ShowActionTimer_Sec", "{0} - активность {1} сек"),
                            ColorLabel(isForRed), points)
                        : string.Format(
                            EntityLocalization.T("Action_ShowActionTimer", "{0} - активность"),
                            ColorLabel(isForRed));

                case MatchActionType.HideActionTimer:
                    return string.Format(
                        EntityLocalization.T("Action_HideActionTimer", "{0} - активность остановлена"),
                        ColorLabel(isForRed));

                case MatchActionType.ActionTimerExpired:
                    return isForRed.HasValue
                        ? string.Format(
                            EntityLocalization.T("Action_ActionTimerExpiredFor", "Таймер {0} завершен"),
                            SideNameGenitive(isForRed))
                        : EntityLocalization.T("Action_ActionTimerExpired", "Завершен таймер активности");

                case MatchActionType.StartMatchTimer:
                    return EntityLocalization.T("Action_StartMatchTimer", "Таймер запущен");

                case MatchActionType.StopMatchTimer:
                    return EntityLocalization.T("Action_StopMatchTimer", "Таймер остановлен");

                case MatchActionType.StartTimeout:
                    return EntityLocalization.T("Action_StartTimeout", "Начался таймаут");

                case MatchActionType.StopTimeout:
                    return EntityLocalization.T("Action_StopTimeout", "Таймаут завершен");

                case MatchActionType.RoundFinished:
                    return string.Format(
                        EntityLocalization.T("Action_RoundFinished", "Раунд {0} завершен"), points);

                case MatchActionType.TimerAdjusted:
                    return string.Format(
                        EntityLocalization.T("Action_TimerAdjusted", "Коррекция таймера на {0:+#;-#;0}с"),
                        points);

                case MatchActionType.MatchCompleted:
                    return EntityLocalization.T("Action_MatchCompleted", "Матч завершен");

                case MatchActionType.Unknown:
                default:
                    return string.Empty;
            }
        }

        // "красному" / "синему" — dative case ("Предупреждение красному").
        // For English: just "red" / "blue".
        private static string SideNameDative(bool? isForRed)
        {
            if (!isForRed.HasValue) return string.Empty;
            return isForRed.Value
                ? EntityLocalization.T("Side_Dative_Red", "красному")
                : EntityLocalization.T("Side_Dative_Blue", "синему");
        }

        // "красного" / "синего" — genitive case ("Таймер красного завершен").
        // For English: just "red" / "blue".
        private static string SideNameGenitive(bool? isForRed)
        {
            if (!isForRed.HasValue) return string.Empty;
            return isForRed.Value
                ? EntityLocalization.T("Side_Genitive_Red", "красного")
                : EntityLocalization.T("Side_Genitive_Blue", "синего");
        }

        // "Красный" / "Синий" — nominative, standalone subject
        // ("Красный +2 балла"). For English: capitalized "Red" / "Blue".
        private static string ColorLabel(bool? isForRed)
        {
            if (!isForRed.HasValue) return string.Empty;
            return isForRed.Value
                ? EntityLocalization.T("Side_Nominative_Red", "Красный")
                : EntityLocalization.T("Side_Nominative_Blue", "Синий");
        }

        // Russian plural for "балл": 1 балл, 2-4 балла, 5+ / 11-14 баллов.
        // English collapses 1=singular, anything else=plural. The selection
        // logic is Russian-grammar-specific (mod 10 / 11..14 exception);
        // for languages with a simpler plural rule the keys
        // Points_Plural_Few / Points_Plural_Many can be set to the same
        // value so the output is consistent.
        private static string PluralizePoints(int value)
        {
            int abs = Math.Abs(value) % 100;
            if (abs >= 11 && abs <= 14) return EntityLocalization.T("Points_Plural_Many", "баллов");
            int rem = abs % 10;
            if (rem == 1) return EntityLocalization.T("Points_Plural_One", "балл");
            if (rem >= 2 && rem <= 4) return EntityLocalization.T("Points_Plural_Few", "балла");
            return EntityLocalization.T("Points_Plural_Many", "баллов");
        }
    }
}
