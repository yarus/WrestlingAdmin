using System;
using System.Linq;
using FluentAssertions;
using Wrestling.Entities;
using Wrestling.UI.Material.Tournament.Standing.Details;
using Xunit;

namespace Wrestling.UI.Material.Tests;

public class GroupGeneratorTests
{
    private readonly GroupGenerator _sut = new();

    [Fact]
    public void Single_age_weight_produces_one_group_with_defaults()
    {
        var result = _sut.Generate("2005-2006:60");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var g = result.Value[0];
        g.BirthYearMin.Should().Be(2005);
        g.BirthYearMax.Should().Be(2006);
        g.WeightMax.Should().Be(60);
        g.MaxActionSecond.Should().Be(30);
        g.MaxRoundSecond.Should().Be(90);
        g.MaxTimeoutSecond.Should().Be(30);
        g.IsFemale.Should().BeFalse();
        g.ID.Should().NotBeEmpty();
    }

    [Fact]
    public void Multiple_weights_expand_into_separate_groups()
    {
        var result = _sut.Generate("2005-2006:40,50,60");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Select(g => g.WeightMax).Should().Equal(40, 50, 60);
    }

    [Fact]
    public void Single_year_without_range_leaves_max_null()
    {
        var result = _sut.Generate("2005:60");

        result.IsSuccess.Should().BeTrue();
        result.Value[0].BirthYearMin.Should().Be(2005);
        result.Value[0].BirthYearMax.Should().BeNull();
    }

    [Fact]
    public void Uses_provided_settings_for_time_caps()
    {
        var settings = new GlobalSettings
        {
            MaxActionSecond = 10,
            MaxRoundSecond = 60,
            MaxTimeoutSecond = 5
        };
        var result = _sut.Generate("2010-2011:45", settings);

        result.IsSuccess.Should().BeTrue();
        var g = result.Value[0];
        g.MaxActionSecond.Should().Be(10);
        g.MaxRoundSecond.Should().Be(60);
        g.MaxTimeoutSecond.Should().Be(5);
    }

    [Fact]
    public void Missing_colon_returns_failure()
    {
        var result = _sut.Generate("2005-2006 60");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Non_numeric_weight_returns_failure()
    {
        var result = _sut.Generate("2005-2006:abc");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Inverted_birth_year_range_returns_failure()
    {
        var result = _sut.Generate("2010-2005:60");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Multi_line_statement_is_parsed_across_newlines()
    {
        var input = "2005-2006:60" + Environment.NewLine + "2007-2008:50,55";

        var result = _sut.Generate(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }
}
