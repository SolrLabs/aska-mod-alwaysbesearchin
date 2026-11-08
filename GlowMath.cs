using System;

namespace AlwaysBeSearchin;

/// <summary>The glow's decisions on plain numbers and strings, testable without the game.</summary>
public static class GlowMath
{
    public static float Clamp01(float v)
    {
        // Math.Clamp passes NaN through; treat it as 0.
        if (float.IsNaN(v)) return 0f;
        return Math.Clamp(v, 0f, 1f);
    }

    /// <summary>Blends night strength to day strength by daylight (0 night, 1 day); NaN daylight counts as day.</summary>
    public static float DayNightStrength(float dayStrength, float nightStrength, float daylight)
    {
        float day = Clamp01(dayStrength);
        float night = Clamp01(nightStrength);
        float t = float.IsNaN(daylight) ? 1f : Clamp01(daylight);
        return night + (day - night) * t;
    }

    // A NaN game value counts as the game's turn.
    public static bool GameHasTurn(bool routineActive, float gameValue, float baseline) =>
        routineActive && !(gameValue < Clamp01(baseline));

    public static bool ExcludeKindNow(float baseline, bool kindSetting) =>
        Clamp01(baseline) > 0f && kindSetting;

    public static bool ExcludeCreature(bool excludeNow, bool isDead) =>
        excludeNow && !isDead;

    /// <summary>How many renderers to write now: the most the object has left, within what remains of the frame's budget.</summary>
    public static int TakeBudget(int remainingInObject, int budgetLeft) =>
        Math.Min(Math.Max(0, remainingInObject), Math.Max(0, budgetLeft));

    /// <summary>True when the name contains, ignoring case, any non-blank trimmed entry of the comma-separated list.</summary>
    public static bool IsPlantType(string configName, string typeList) =>
        ContainsAny(configName, typeList);

    private static bool ContainsAny(string configName, string list)
    {
        if (string.IsNullOrEmpty(configName) || string.IsNullOrEmpty(list)) return false;
        foreach (string entry in list.Split(','))
        {
            string t = entry.Trim();
            if (t.Length > 0 && configName.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    /// <summary>The highlight multiplier for a vegetation type: boosted types get original times boostStrength (1..10),
    /// else plant types get original times plantStrength (0..1), else the original. Boost wins.</summary>
    public static float TypeMultiplier(string configName, float original, string plantTypes, float plantStrength, string boostTypes, float boostStrength)
    {
        if (ContainsAny(configName, boostTypes))
            return original * (float.IsNaN(boostStrength) ? 1f : Math.Clamp(boostStrength, 1f, 10f));
        if (IsPlantType(configName, plantTypes)) return original * Clamp01(plantStrength);
        return original;
    }

    /// <summary>Parses #RRGGBB or RRGGBB (any case) into channels 0..1; false, with zeros, for anything else.</summary>
    public static bool TryParseHexColor(string s, out float r, out float g, out float b)
    {
        r = g = b = 0f;
        if (string.IsNullOrEmpty(s)) return false;
        if (s[0] == '#') s = s.Substring(1);
        if (s.Length != 6) return false;
        int v = 0;
        foreach (char c in s)
        {
            int d;
            if (c >= '0' && c <= '9') d = c - '0';
            else if (c >= 'a' && c <= 'f') d = c - 'a' + 10;
            else if (c >= 'A' && c <= 'F') d = c - 'A' + 10;
            else return false;
            v = v * 16 + d;
        }
        r = ((v >> 16) & 0xFF) / 255f;
        g = ((v >> 8) & 0xFF) / 255f;
        b = (v & 0xFF) / 255f;
        return true;
    }
}
