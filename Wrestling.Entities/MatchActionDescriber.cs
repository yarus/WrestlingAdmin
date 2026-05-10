using System;

namespace Wrestling.Entities
{
    // Single source of truth for the Russian display text of a MatchAction.
    // Used by:
    //   - EntityToInfoAdapter (entity→info) to populate MatchActionInfo.Text
    //     so old-version clients reading the .wrt still see meaningful entries.
    //   - The UI MatchActionDescriptionConverter to render the protocol column.
    //
    // The strings produced here are intentionally close to the legacy text so
    // LegacyMatchActionTypeInferrer can round-trip an action through a save
    // cycle done by an old client without losing its type.
    public static class MatchActionDescriber
    {
        public static string Describe(MatchActionType type, bool? isForRed, int points)
        {
            string color = SideName(isForRed);

            switch (type)
            {
                case MatchActionType.SetPoints:
                    return $"{ColorLabel(isForRed)} +{points} {PluralizePoints(points)}";

                case MatchActionType.SetWarning:
                    return $"Предупреждение {SideNameDative(isForRed)}";

                case MatchActionType.RevertPoints:
                    return $"Коррекция -{points} {SideNameDative(isForRed)}";

                case MatchActionType.RevertWarning:
                    return $"Отмена предупреждения {SideNameDative(isForRed)}";

                case MatchActionType.ShowActionTimer:
                    return points > 0
                        ? $"{ColorLabel(isForRed)} - активность {points} сек"
                        : $"{ColorLabel(isForRed)} - активность";

                case MatchActionType.HideActionTimer:
                    return $"{ColorLabel(isForRed)} - активность остановлена";

                case MatchActionType.ActionTimerExpired:
                    return isForRed.HasValue
                        ? $"Таймер {SideNameGenitive(isForRed)} завершен"
                        : "Завершен таймер активности";

                case MatchActionType.StartMatchTimer:
                    return "Таймер запущен";

                case MatchActionType.StopMatchTimer:
                    return "Таймер остановлен";

                case MatchActionType.StartTimeout:
                    return "Начался таймаут";

                case MatchActionType.StopTimeout:
                    return "Таймаут завершен";

                case MatchActionType.RoundFinished:
                    return $"Раунд {points} завершен";

                case MatchActionType.TimerAdjusted:
                    return $"Коррекция таймера на {points:+#;-#;0}с";

                case MatchActionType.MatchCompleted:
                    return "Матч завершен";

                case MatchActionType.Unknown:
                default:
                    return string.Empty;
            }
        }

        // "красном" / "синем" — prepositional case ("в красном трико").
        private static string SideName(bool? isForRed)
        {
            if (!isForRed.HasValue) return string.Empty;
            return isForRed.Value ? "красном" : "синем";
        }

        // "красному" / "синему" — dative case ("Предупреждение красному").
        private static string SideNameDative(bool? isForRed)
        {
            if (!isForRed.HasValue) return string.Empty;
            return isForRed.Value ? "красному" : "синему";
        }

        // "красного" / "синего" — genitive case ("Таймер красного завершен").
        private static string SideNameGenitive(bool? isForRed)
        {
            if (!isForRed.HasValue) return string.Empty;
            return isForRed.Value ? "красного" : "синего";
        }

        // "Красный" / "Синий" — nominative, standalone subject ("Красный +2 балла").
        private static string ColorLabel(bool? isForRed)
        {
            if (!isForRed.HasValue) return string.Empty;
            return isForRed.Value ? "Красный" : "Синий";
        }

        // Russian plural for "балл": 1 балл, 2-4 балла, 5+ / 11-14 баллов.
        private static string PluralizePoints(int value)
        {
            int abs = Math.Abs(value) % 100;
            if (abs >= 11 && abs <= 14) return "баллов";
            int rem = abs % 10;
            if (rem == 1) return "балл";
            if (rem >= 2 && rem <= 4) return "балла";
            return "баллов";
        }
    }
}
