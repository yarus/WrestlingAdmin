using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Wrestling.Data;
using Wrestling.Providers;
using Xunit;

namespace Wrestling.Providers.Tests;

// Reflection-based round-trip COMPLETENESS guards. The hand-written
// AdapterRoundTripTests assert a curated subset of fields; these instead
// enumerate EVERY writable property on the *Info DTO, fill each with a
// distinct non-default value, push it through Info → Entity → Info, and assert
// every property survived unchanged.
//
// Why this matters: EntityToInfoAdapter maps each field in BOTH directions by
// hand. A field added to one direction but not the other silently fails to
// round-trip through .wrt save/load — a data-loss bug that no subset test would
// catch. With these guards, adding WrestlerInfo.Foo (wired only one way) fails
// here automatically, without anyone remembering to extend a test.
public class AdapterCompletenessTests
{
    private readonly EntityToInfoAdapter _adapter = new();

    [Fact]
    public void Every_WrestlerInfo_field_survives_round_trip()
    {
        // Level is an enum<->string field whose wire format is the enum NAME
        // ("KMS"); a raw reflection string ("value-7") wouldn't round-trip, so
        // it's overridden with a canonical name here and pinned across labels by
        // the Theories below.
        var info = new WrestlerInfo();
        PopulateScalars(info, overrides: new Dictionary<string, object> { ["Level"] = "KMS" });

        var restored = _adapter.GetInfoFromEntity(_adapter.GetEntityFromInfo(info));

        AssertScalarsEqual(info, restored);
    }

    [Fact]
    public void Every_TeamApplicationInfo_field_survives_round_trip()
    {
        // TeamApplication has no round-trip coverage elsewhere — every persisted
        // field (coach, representative, city, emblem path, contacts…) is checked
        // here in both directions.
        var info = new TeamApplicationInfo();
        PopulateScalars(info, overrides: null);

        var restored = _adapter.GetInfoFromEntity(_adapter.GetEntityFromInfo(info));

        AssertScalarsEqual(info, restored);
    }

    // Current wire format: WrestlerInfo.Level holds the enum NAME (the adapter
    // writes entity.Level.ToString()), so a canonical name must round-trip
    // identically — this is what every modern .wrt actually stores.
    [Theory]
    [InlineData("MSMK")]
    [InlineData("MS")]
    [InlineData("KMS")]
    [InlineData("Adult1")]
    [InlineData("Adult3")]
    [InlineData("Junior1")]
    [InlineData("Junior3")]
    public void Wrestler_Level_enum_name_round_trips_identically(string level)
    {
        var info = new WrestlerInfo { ID = Guid.NewGuid(), LastName = "X", FirstName = "Y", Level = level };

        var restored = _adapter.GetInfoFromEntity(_adapter.GetEntityFromInfo(info));

        restored.Level.Should().Be(level, "the canonical enum-name wire format must round-trip losslessly");
    }

    // Legacy .wrt files stored the rank as a Russian label. Loading normalizes
    // it to the enum name on the next save — lossless at the semantic (enum)
    // level, even though the persisted string changes. This pins that contract
    // so the legacy-acceptance path in FromString can't silently regress.
    [Theory]
    [InlineData("МСМК", "MSMK")]
    [InlineData("КМС", "KMS")]
    [InlineData("III юн", "Junior3")]
    [InlineData("I юн.", "Junior1")] // formal spelling with trailing dot
    [InlineData("б/р", "")]          // "no rank" → None → empty string
    [InlineData("", "")]
    public void Wrestler_legacy_cyrillic_Level_normalizes_to_enum_name(string legacy, string expected)
    {
        var info = new WrestlerInfo { ID = Guid.NewGuid(), LastName = "X", FirstName = "Y", Level = legacy };

        var restored = _adapter.GetInfoFromEntity(_adapter.GetEntityFromInfo(info));

        restored.Level.Should().Be(expected);
    }

    // --- reflection helpers ------------------------------------------------

    private static void PopulateScalars(object info, IDictionary<string, object> overrides)
    {
        int i = 1;
        foreach (var p in ScalarProps(info.GetType()))
        {
            if (overrides != null && overrides.TryGetValue(p.Name, out var ov))
            {
                p.SetValue(info, ov);
                continue;
            }
            p.SetValue(info, MakeValue(p.PropertyType, i));
            i++;
        }
    }

    private static void AssertScalarsEqual(object expected, object actual)
    {
        foreach (var p in ScalarProps(expected.GetType()))
        {
            p.GetValue(actual).Should().Be(p.GetValue(expected),
                $"property '{p.Name}' must survive the EntityToInfoAdapter round-trip in both directions");
        }
    }

    private static IEnumerable<PropertyInfo> ScalarProps(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
         .Where(p => p.CanRead && p.CanWrite);

    // Distinct, type-appropriate non-default values. The per-property increment
    // makes string fields distinct so a swapped assignment (e.g. FirstName <-
    // LastName) is also caught, not just a dropped one.
    private static object MakeValue(Type type, int i)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string)) return $"value-{i}";
        if (t == typeof(int)) return i;
        if (t == typeof(double)) return i + 0.5;
        if (t == typeof(decimal)) return i + 0.25m;
        if (t == typeof(bool)) return true;
        if (t == typeof(Guid)) return Guid.NewGuid();
        if (t == typeof(DateTime)) return new DateTime(2020, 1, 1).AddDays(i);
        if (t.IsEnum) return Enum.GetValues(t).GetValue(0);
        throw new NotSupportedException($"Add a MakeValue case for {t.Name} (property index {i}).");
    }
}
