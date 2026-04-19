using System;
using System.Globalization;
using System.Windows;
using FluentAssertions;
using Wrestling.UI.Utils.Converters;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public class ConverterTests
{
    private static readonly CultureInfo _ru = CultureInfo.GetCultureInfo("ru-RU");

    [Fact]
    public void FullPathToFileName_returns_empty_for_null_or_empty()
    {
        var c = new FullPathToFileNameConverter();
        c.Convert(null, typeof(string), null, _ru).Should().Be(string.Empty);
        c.Convert("", typeof(string), null, _ru).Should().Be(string.Empty);
    }

    [Fact]
    public void FullPathToFileName_strips_directory()
    {
        var c = new FullPathToFileNameConverter();
        c.Convert(@"C:\a\b\foo.txt", typeof(string), null, _ru).Should().Be("foo.txt");
    }

    [Fact]
    public void UpperCase_converts_string()
    {
        var c = new UpperCaseConverter();
        c.Convert("иван", typeof(string), null, _ru).Should().Be("ИВАН");
        c.Convert(null, typeof(string), null, _ru).Should().Be(string.Empty);
    }

    [Fact]
    public void SecondsToTimeSpan_zero_returns_zero_span()
    {
        var c = new SecondsToTimeSpanConverter();
        c.Convert(0, typeof(TimeSpan), null, _ru).Should().Be(new TimeSpan(0, 0, 0, 0));
    }

    [Fact]
    public void SecondsToTimeSpan_65_returns_1m5s()
    {
        var c = new SecondsToTimeSpanConverter();
        c.Convert(65, typeof(TimeSpan), null, _ru).Should().Be(new TimeSpan(0, 0, 1, 5));
    }

    [Fact]
    public void SecondsToTimeString_under_a_day_returns_hhmm()
    {
        var c = new SecondsToTimeStringConverter();
        c.Convert(3665, typeof(string), null, _ru).Should().Be("01:01");
    }

    [Fact]
    public void BoolInverter_flips_booleans()
    {
        var c = new BoolInverterConverter();
        c.Convert(true, typeof(bool), null, _ru).Should().Be(false);
        c.Convert(false, typeof(bool), null, _ru).Should().Be(true);
    }

    [Fact]
    public void GroupedBoolToVis_maps_true_visible_false_hidden()
    {
        var c = new GroupedBoolToVisConverter();
        c.Convert(true, typeof(Visibility), null, _ru).Should().Be(Visibility.Visible);
        c.Convert(false, typeof(Visibility), null, _ru).Should().Be(Visibility.Hidden);
        c.Convert("not bool", typeof(Visibility), null, _ru).Should().Be(Visibility.Hidden);
    }
}
