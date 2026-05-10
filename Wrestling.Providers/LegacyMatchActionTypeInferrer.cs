using System;
using System.Text.RegularExpressions;
using Wrestling.Entities;

namespace Wrestling.Providers
{
    // One-shot migration helper: maps a legacy MatchActionInfo.Text (Russian
    // free-form) to a MatchActionType. Used only by EntityToInfoAdapter when
    // loading a .wrt written by a pre-typing version of the app.
    //
    // Rule order matters — more specific patterns must precede more general
    // ones (e.g. "Отмена предупреждения..." before any plain "...предупреждение").
    // Returns Unknown when no rule matches; loader keeps the action with that
    // type and the original Text untouched (display still works).
    public static class LegacyMatchActionTypeInferrer
    {
        private static readonly Regex ShowActionTimerPattern =
            new Regex(@"^(Красный|Синий) - активность \d+ сек$", RegexOptions.Compiled);

        private static readonly Regex RoundFinishedPattern =
            new Regex(@"^Раунд \d+ завершен$", RegexOptions.Compiled);

        // Old format: «Действие борца в красном/синем трико оценено в N»
        // New format: «Красный/Синий +N балл/балла/баллов»
        private static readonly Regex SetPointsOldPattern =
            new Regex(@"^Действие борца в (красном|синем) трико оценено в \d+", RegexOptions.Compiled);

        private static readonly Regex SetPointsNewPattern =
            new Regex(@"^(Красный|Синий) \+\d+ балл", RegexOptions.Compiled);

        // Short-form new-app strings, recognized so that a downgrade-then-
        // upgrade cycle (new app writes file → old app re-saves it without
        // Type → new app reads it again) still infers the right type.
        private static readonly Regex RevertPointsShortPattern =
            new Regex(@"^Коррекция -?\d+ ", RegexOptions.Compiled);

        private static readonly Regex ActionTimerExpiredShortPattern =
            new Regex(@"^Таймер (красного|синего) завершен$", RegexOptions.Compiled);

        public static MatchActionType Infer(string text, int points, bool? isForRed)
        {
            if (string.IsNullOrEmpty(text)) return MatchActionType.Unknown;

            // Order matters: specific cancellation/correction phrasings first.
            if (text.IndexOf("Отмена предупреждения", StringComparison.Ordinal) >= 0)
                return MatchActionType.RevertWarning;

            // Both "...получил предупреждение" (legacy) and "Предупреждение
            // красному/синему" (new short form) match this rule.
            if (text.IndexOf("предупреждение", StringComparison.OrdinalIgnoreCase) >= 0)
                return MatchActionType.SetWarning;

            if (text.StartsWith("Коррекция баллов", StringComparison.Ordinal)
                || RevertPointsShortPattern.IsMatch(text))
                return MatchActionType.RevertPoints;

            if (text.StartsWith("Коррекция таймера", StringComparison.Ordinal))
                return MatchActionType.TimerAdjusted;

            if (ActionTimerExpiredShortPattern.IsMatch(text))
                return MatchActionType.ActionTimerExpired;

            if (text.EndsWith("активность остановлена", StringComparison.Ordinal))
                return MatchActionType.HideActionTimer;

            if (ShowActionTimerPattern.IsMatch(text))
                return MatchActionType.ShowActionTimer;

            if (text.StartsWith("Завершен таймер активности", StringComparison.Ordinal))
                return MatchActionType.ActionTimerExpired;

            if (text == "Таймер запущен")
                return MatchActionType.StartMatchTimer;

            if (text == "Таймер остановлен")
                return MatchActionType.StopMatchTimer;

            if (text == "Начался таймаут")
                return MatchActionType.StartTimeout;

            if (text == "Таймаут завершен")
                return MatchActionType.StopTimeout;

            if (RoundFinishedPattern.IsMatch(text))
                return MatchActionType.RoundFinished;

            if (text == "Матч завершен")
                return MatchActionType.MatchCompleted;

            if (SetPointsOldPattern.IsMatch(text) || SetPointsNewPattern.IsMatch(text))
                return MatchActionType.SetPoints;

            return MatchActionType.Unknown;
        }
    }
}
