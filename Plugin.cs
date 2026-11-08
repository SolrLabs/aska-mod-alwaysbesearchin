using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace AlwaysBeSearchin;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "solrlabs.aska.alwaysbesearchin";
    public const string Name = "Always Be Searchin'";
    public const string Version = "0.1.1";

    internal static ManualLogSource Logger;
    internal static ConfigEntry<float> DayStrength;
    internal static ConfigEntry<float> NightStrength;
    internal static ConfigEntry<float> GlowRadius;
    internal static ConfigEntry<string> GlowColor;
    internal static ConfigEntry<bool> ExcludeVillagers;
    internal static ConfigEntry<bool> ExcludeStructures;
    internal static ConfigEntry<float> PlantStrength;
    internal static ConfigEntry<string> PlantTypes;
    internal static ConfigEntry<string> BoostTypes;
    internal static ConfigEntry<float> BoostStrength;

    public override void Load()
    {
        Logger = Log;
        DayStrength = Config.Bind("General", "DayStrength", 0.17f,
            new ConfigDescription("Strength of the always-on glow in full daylight, 0 to 1.",
                new AcceptableValueRange<float>(0f, 1f)));
        NightStrength = Config.Bind("General", "NightStrength", 0.08f,
            new ConfigDescription("Strength of the always-on glow at night, 0 to 1. The glow blends between the two with the game's day and night. Set both to 0 to turn the always-on glow off; Z then works as in the base game.",
                new AcceptableValueRange<float>(0f, 1f)));
        GlowRadius = Config.Bind("General", "GlowRadius", 60f,
            new ConfigDescription("Radius in metres of the always-on glow around the player.",
                new AcceptableValueRange<float>(5f, 200f)));
        GlowColor = Config.Bind("General", "GlowColor", "#FFFF33",
            "Color of the always-on glow as #RRGGBB. Empty uses the game's Resource Finder color from its settings.");
        ExcludeVillagers = Config.Bind("General", "ExcludeVillagers", true,
            "Keep villagers, players and live creatures, and what they carry, out of the glow, including during Z.");
        ExcludeStructures = Config.Bind("General", "ExcludeStructures", true,
            "Keep items stored in or on buildings (storage, cooking pots, racks) out of the glow. Items on the landscape still glow.");
        PlantStrength = Config.Bind("General", "PlantStrength", 0.5f,
            new ConfigDescription("Multiplier on the glow of plants (ferns, bushes, grass and similar), which glow much brighter than rocks. 1 leaves them as the game draws them. Also applies during Z.",
                new AcceptableValueRange<float>(0f, 1f)));
        PlantTypes = Config.Bind("General", "PlantTypes", "Fern,Bush,Grass,Reed,Flower,Lupin",
            "Comma-separated name fragments of the vegetation types that PlantStrength applies to.");
        BoostTypes = Config.Bind("General", "BoostTypes", "Flax",
            "Comma-separated name fragments of vegetation types whose glow is multiplied by BoostStrength (never dimmed by PlantStrength).");
        BoostStrength = Config.Bind("General", "BoostStrength", 3f,
            new ConfigDescription("Multiplier on the glow of BoostTypes, for items that are hard to see (1 = as the game draws them).",
                new AcceptableValueRange<float>(1f, 10f)));

        new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
        AddComponent<AlwaysOnGlow>();
        Log.LogInfo($"Always Be Searchin' {Version} loaded (day {DayStrength.Value}, night {NightStrength.Value}, radius {GlowRadius.Value}, color {GlowColor.Value}, exclude villagers {ExcludeVillagers.Value})");
    }
}
