using AlwaysBeSearchin;
using Xunit;

public class GlowMathTests
{
    [Fact]
    public void Clamp01_bounds()
    {
        Assert.Equal(0f, GlowMath.Clamp01(-0.5f));
        Assert.Equal(0.3f, GlowMath.Clamp01(0.3f));
        Assert.Equal(1f, GlowMath.Clamp01(1.7f));
    }

    [Fact]
    public void Clamp01_nan_is_zero() =>
        Assert.Equal(0f, GlowMath.Clamp01(float.NaN));

    [Fact]
    public void GameHasTurn_false_when_routine_inactive() =>
        Assert.False(GlowMath.GameHasTurn(false, 1f, 0.3f));

    [Fact]
    public void GameHasTurn_true_at_full_strength() =>
        Assert.True(GlowMath.GameHasTurn(true, 1f, 0.3f));

    [Fact]
    public void GameHasTurn_false_when_fade_below_baseline() =>
        Assert.False(GlowMath.GameHasTurn(true, 0.2f, 0.3f));

    [Fact]
    public void GameHasTurn_true_at_exactly_baseline() =>
        Assert.True(GlowMath.GameHasTurn(true, 0.3f, 0.3f));

    [Fact]
    public void GameHasTurn_true_for_nan() =>
        Assert.True(GlowMath.GameHasTurn(true, float.NaN, 0.3f));

    [Fact]
    public void GameHasTurn_clamps_baseline() =>
        Assert.True(GlowMath.GameHasTurn(true, 1f, 1.7f));

    [Fact]
    public void DayNightStrength_full_day() =>
        Assert.Equal(0.4f, GlowMath.DayNightStrength(0.4f, 0.15f, 1f));

    [Fact]
    public void DayNightStrength_full_night() =>
        Assert.Equal(0.15f, GlowMath.DayNightStrength(0.4f, 0.15f, 0f));

    [Fact]
    public void DayNightStrength_halfway() =>
        Assert.Equal(0.275f, GlowMath.DayNightStrength(0.4f, 0.15f, 0.5f), 1e-5f);

    [Fact]
    public void DayNightStrength_clamps_daylight()
    {
        Assert.Equal(0.4f, GlowMath.DayNightStrength(0.4f, 0.15f, 1.7f));
        Assert.Equal(0.15f, GlowMath.DayNightStrength(0.4f, 0.15f, -0.3f));
    }

    [Fact]
    public void DayNightStrength_nan_daylight_is_day() =>
        Assert.Equal(0.4f, GlowMath.DayNightStrength(0.4f, 0.15f, float.NaN));

    [Fact]
    public void DayNightStrength_both_zero_is_off() =>
        Assert.Equal(0f, GlowMath.DayNightStrength(0f, 0f, 0.5f));

    [Fact]
    public void ExcludeKindNow_off_when_baseline_zero() =>
        Assert.False(GlowMath.ExcludeKindNow(0f, true));

    [Fact]
    public void ExcludeKindNow_on() =>
        Assert.True(GlowMath.ExcludeKindNow(0.17f, true));

    [Fact]
    public void ExcludeKindNow_off_when_setting_off() =>
        Assert.False(GlowMath.ExcludeKindNow(0.17f, false));

    [Fact]
    public void Hex_parses_cream()
    {
        Assert.True(GlowMath.TryParseHexColor("#FFF1D0", out float r, out float g, out float b));
        Assert.Equal(1f, r, 0.002f);
        Assert.Equal(0.945f, g, 0.002f);
        Assert.Equal(0.816f, b, 0.002f);
    }

    [Fact]
    public void Hex_accepts_lowercase_without_hash()
    {
        Assert.True(GlowMath.TryParseHexColor("fff1d0", out float r, out float g, out float b));
        Assert.Equal(1f, r, 0.002f);
        Assert.Equal(0.945f, g, 0.002f);
        Assert.Equal(0.816f, b, 0.002f);
    }

    [Fact]
    public void Hex_rejects_bad_input()
    {
        foreach (var s in new string[] { null, "", "#FFF", "#GGGGGG", "#FFF1D0AA" })
        {
            Assert.False(GlowMath.TryParseHexColor(s, out float r, out float g, out float b));
            Assert.Equal(0f, r + g + b);
        }
    }

    [Fact]
    public void ExcludeCreature_live_excluded() =>
        Assert.True(GlowMath.ExcludeCreature(true, false));

    [Fact]
    public void ExcludeCreature_dead_not_excluded() =>
        Assert.False(GlowMath.ExcludeCreature(true, true));

    [Fact]
    public void ExcludeCreature_off_when_exclusion_off() =>
        Assert.False(GlowMath.ExcludeCreature(false, false));

    [Fact]
    public void IsPlantType_matches_fern() =>
        Assert.True(GlowMath.IsPlantType("VFXVegetation_Ferns", "Fern,Bush"));

    [Fact]
    public void IsPlantType_case_insensitive() =>
        Assert.True(GlowMath.IsPlantType("VFXVegetation_BushFlax", "bush"));

    [Fact]
    public void IsPlantType_rock_not_plant() =>
        Assert.False(GlowMath.IsPlantType("VFXVegetation_RockForest_Large", "Fern,Bush,Grass,Reed,Flower,Lupin"));

    private const string DefaultTypes = "Fern,Bush,Grass,Reed,Flower,Lupin";

    [Fact]
    public void IsPlantType_ignores_blank_entries()
    {
        Assert.True(GlowMath.IsPlantType("VFXVegetation_Ferns", " , ,Fern "));
        Assert.False(GlowMath.IsPlantType("VFXVegetation_Rock4", " , "));
    }

    [Fact]
    public void TypeMultiplier_boosts_flax() =>
        Assert.Equal(6f, GlowMath.TypeMultiplier("VFXVegetation_BushFlax", 2f, DefaultTypes, 0.5f, "Flax", 3f));

    [Fact]
    public void TypeMultiplier_dims_fern() =>
        Assert.Equal(0.5f, GlowMath.TypeMultiplier("VFXVegetation_Ferns", 1f, DefaultTypes, 0.5f, "Flax", 3f));

    [Fact]
    public void TypeMultiplier_leaves_rock() =>
        Assert.Equal(1f, GlowMath.TypeMultiplier("VFXVegetation_Rock4", 1f, DefaultTypes, 0.5f, "Flax", 3f));

    [Fact]
    public void TypeMultiplier_clamps_boost()
    {
        Assert.Equal(20f, GlowMath.TypeMultiplier("VFXVegetation_BushFlax", 2f, DefaultTypes, 0.5f, "Flax", 20f));
        Assert.Equal(2f, GlowMath.TypeMultiplier("VFXVegetation_BushFlax", 2f, DefaultTypes, 0.5f, "Flax", 0.5f));
    }

    [Fact]
    public void TypeMultiplier_empty_boost_list() =>
        Assert.Equal(1f, GlowMath.TypeMultiplier("VFXVegetation_BushFlax", 2f, DefaultTypes, 0.5f, "", 3f));

    [Fact]
    public void TakeBudget_takes_all_when_room() =>
        Assert.Equal(40, GlowMath.TakeBudget(40, 150));

    [Fact]
    public void TakeBudget_caps_at_budget() =>
        Assert.Equal(150, GlowMath.TakeBudget(400, 150));

    [Fact]
    public void TakeBudget_zero_budget() =>
        Assert.Equal(0, GlowMath.TakeBudget(10, 0));

    [Fact]
    public void TakeBudget_negative_inputs()
    {
        Assert.Equal(0, GlowMath.TakeBudget(-1, 150));
        Assert.Equal(0, GlowMath.TakeBudget(10, -5));
    }
}
