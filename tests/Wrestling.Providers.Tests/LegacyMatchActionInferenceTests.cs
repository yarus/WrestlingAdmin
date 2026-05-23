using FluentAssertions;
using Wrestling.Entities;
using Wrestling.Providers;
using Xunit;

namespace Wrestling.Providers.Tests;

// Unit tests for the legacy inferrer that translates pre-typing MatchAction
// text strings into MatchActionType variants. Each rule from
// LegacyMatchActionTypeInferrer must round-trip a representative legacy text.
public class LegacyMatchActionInferenceTests
{
    [Theory]
    [InlineData("Отмена предупреждения борцу в красном трико", MatchActionType.RevertWarning)]
    [InlineData("Отмена предупреждения борцу в синем трико", MatchActionType.RevertWarning)]
    [InlineData("Борец в красном трико получил 1 предупреждение", MatchActionType.SetWarning)]
    [InlineData("Борец в синем трико получил 2 предупреждение", MatchActionType.SetWarning)]
    [InlineData("Коррекция баллов для борца в красном на 2", MatchActionType.RevertPoints)]
    [InlineData("Коррекция баллов борцу в синем на -1", MatchActionType.RevertPoints)]
    [InlineData("Коррекция таймера на +5с", MatchActionType.TimerAdjusted)]
    [InlineData("Коррекция таймера на -3с", MatchActionType.TimerAdjusted)]
    [InlineData("Красный - активность остановлена", MatchActionType.HideActionTimer)]
    [InlineData("Синий - активность остановлена", MatchActionType.HideActionTimer)]
    [InlineData("Красный - активность 30 сек", MatchActionType.ShowActionTimer)]
    [InlineData("Синий - активность 60 сек", MatchActionType.ShowActionTimer)]
    [InlineData("Завершен таймер активности", MatchActionType.ActionTimerExpired)]
    [InlineData("Таймер запущен", MatchActionType.StartMatchTimer)]
    [InlineData("Таймер остановлен", MatchActionType.StopMatchTimer)]
    [InlineData("Начался таймаут", MatchActionType.StartTimeout)]
    [InlineData("Таймаут завершен", MatchActionType.StopTimeout)]
    [InlineData("Раунд 1 завершен", MatchActionType.RoundFinished)]
    [InlineData("Раунд 2 завершен", MatchActionType.RoundFinished)]
    [InlineData("Матч завершен", MatchActionType.MatchCompleted)]
    [InlineData("Красный +1 балл", MatchActionType.SetPoints)]
    [InlineData("Синий +2 балла", MatchActionType.SetPoints)]
    [InlineData("Красный +5 баллов", MatchActionType.SetPoints)]
    [InlineData("Действие борца в красном трико оценено в 2", MatchActionType.SetPoints)]
    [InlineData("Действие борца в синем трико оценено в 4", MatchActionType.SetPoints)]
    // New short-form strings (downgrade-then-upgrade safety net):
    [InlineData("Предупреждение красному", MatchActionType.SetWarning)]
    [InlineData("Предупреждение синему", MatchActionType.SetWarning)]
    [InlineData("Отмена предупреждения красному", MatchActionType.RevertWarning)]
    [InlineData("Отмена предупреждения синему", MatchActionType.RevertWarning)]
    [InlineData("Коррекция -2 красному", MatchActionType.RevertPoints)]
    [InlineData("Коррекция -1 синему", MatchActionType.RevertPoints)]
    [InlineData("Таймер красного завершен", MatchActionType.ActionTimerExpired)]
    [InlineData("Таймер синего завершен", MatchActionType.ActionTimerExpired)]
    public void Infer_maps_legacy_text_to_correct_type(string legacyText, MatchActionType expected)
    {
        LegacyMatchActionTypeInferrer.Infer(legacyText, points: 0, isForRed: null)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Какая-то непонятная строка из будущей версии")]
    public void Infer_returns_Unknown_for_unrecognized_input(string text)
    {
        LegacyMatchActionTypeInferrer.Infer(text, points: 0, isForRed: null)
            .Should().Be(MatchActionType.Unknown);
    }

    [Fact]
    public void Cancellation_phrasing_takes_priority_over_warning_phrasing()
    {
        // Both texts contain «предупреждение». The cancellation rule must
        // fire first or revert-of-warning would be misclassified as a new
        // warning on load.
        LegacyMatchActionTypeInferrer.Infer("Отмена предупреждения борцу в красном трико", 0, true)
            .Should().Be(MatchActionType.RevertWarning);
    }
}
