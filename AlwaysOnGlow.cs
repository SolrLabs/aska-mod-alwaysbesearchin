using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using SSSGame;
using SSSGame.Vegetation;
using SSSGame.Weather;
using UnityEngine;

namespace AlwaysBeSearchin;

/// <summary>Keeps gatherables near the player softly glowing by writing the Resource Finder's shader globals,
/// stepping aside while the game's own finder (Z) is at or above the baseline, which follows the game's day and night.</summary>
public class AlwaysOnGlow : MonoBehaviour
{
    public AlwaysOnGlow(IntPtr ptr) : base(ptr) { }

    private static readonly int ValueId = Shader.PropertyToID("Global_ResFinderValue");
    private static readonly int RadiusId = Shader.PropertyToID("Global_ResFinderRadius");
    private static readonly int ColorId = Shader.PropertyToID("Global_ResFinderColor");

    // Set by GlowPatches while the game's finder routine runs.
    internal static bool RoutineActive;
    internal static float RoutineDeadline;

    private PlayerInteractionAgent _agent;
    private float _nextAgentLookup;
    private SettingsManager _settings;

    private bool _wasGameTurn;
    private bool _zeroWritten;

    private string _parsedFrom;
    private bool _hasOurColor;
    private Vector4 _ourColor;

    private MaterialPropertyBlock _mpb;
    private bool _wasExcluding;
    private int _cursor = -1;
    private float _nextCharLog;
    private double _worstFrameMs, _worstLookupMs, _worstWriteMs;

    private WeatherSystem _weather;
    private float _nextWeatherLookup;
    private float _nextStrengthLog;
    private bool _weatherFailed;

    private readonly Dictionary<int, (bool overrideOn, float strength)> _plantOriginals = new Dictionary<int, (bool, float)>();
    private float _nextPlantPass;
    private float _lastPlantStrength = float.NaN;
    private string _lastPlantTypes;
    private string _lastBoostTypes;
    private float _lastBoostStrength = float.NaN;
    private bool _plantsLogged;

    private bool _updateFailed, _lateFailed, _colorFailed, _villagerFailed, _plantFailed;

    private void Update()
    {
        try
        {
            if (RoutineActive && Time.time > RoutineDeadline) RoutineActive = false;
        }
        catch (Exception e)
        {
            if (_updateFailed) return;
            _updateFailed = true;
            Plugin.Logger.LogWarning("Update failed: " + e);
        }

        try
        {
            TickPlants();
        }
        catch (Exception e)
        {
            if (_plantFailed) return;
            _plantFailed = true;
            Plugin.Logger.LogWarning("Plant strength failed: " + e);
        }
    }

    /// <summary>Every 5 s, and at once when the settings change, sets the highlight multiplier of each vegetation type:
    /// dimmed by PlantStrength, boosted by BoostStrength, or restored. Each type's original values are kept on first sight.</summary>
    private void TickPlants()
    {
        float now = Time.time;
        float strength = Plugin.PlantStrength.Value;
        string types = Plugin.PlantTypes.Value;
        string boostTypes = Plugin.BoostTypes.Value;
        float boost = Plugin.BoostStrength.Value;
        bool changed = strength != _lastPlantStrength || !string.Equals(types, _lastPlantTypes, StringComparison.Ordinal)
            || !string.Equals(boostTypes, _lastBoostTypes, StringComparison.Ordinal) || boost != _lastBoostStrength;
        if (!changed && now < _nextPlantPass) return;
        _nextPlantPass = now + 5f;
        _lastPlantStrength = strength;
        _lastPlantTypes = types;
        _lastBoostTypes = boostTypes;
        _lastBoostStrength = boost;

        var addon = VegetationVFXAddon.Instance;
        if (addon == null) return;
        var renderers = addon._vfxRenderers;
        if (renderers == null) return;

        var seen = new HashSet<int>();
        var boosted = new List<string>();
        int dimmed = 0;
        var updated = new HashSet<int>();
        int count = renderers.Count;
        for (int i = 0; i < count; i++)
        {
            var renderer = renderers[i];
            var profile = renderer?.Profile;
            if (profile == null) continue;
            int id = profile.GetInstanceID();
            if (!_plantOriginals.TryGetValue(id, out var original))
            {
                original = (profile.overrideHighlightStrength, profile.highlightStrength);
                _plantOriginals[id] = original;
            }

            float wantStrength = GlowMath.TypeMultiplier(profile.name, original.strength, types, strength, boostTypes, boost);
            bool changedType = wantStrength != original.strength;
            bool wantOverride = changedType ? true : original.overrideOn;
            if (seen.Add(id) && changedType)
            {
                if (wantStrength > original.strength) boosted.Add(profile.name);
                else dimmed++;
            }
            if (profile.overrideHighlightStrength != wantOverride || profile.highlightStrength != wantStrength)
            {
                profile.overrideHighlightStrength = wantOverride;
                profile.highlightStrength = wantStrength;
                updated.Add(id);
            }
            if (updated.Contains(id)) renderer._UpdateHighlightStrength();
        }

        if (!_plantsLogged && count > 0)
        {
            _plantsLogged = true;
            Plugin.Logger.LogInfo($"vegetation types scaled: {dimmed} dimmed, {boosted.Count} boosted of {seen.Count} ({string.Join(", ", boosted.GetRange(0, Math.Min(20, boosted.Count)))})");
        }
    }

    private void LateUpdate()
    {
        try
        {
            if (_agent == null)
            {
                float now = Time.time;
                if (now < _nextAgentLookup) return;
                _nextAgentLookup = now + 2f;
                _agent = FindObjectOfType<PlayerInteractionAgent>();
                if (_agent == null) return;
                _seedStep = 0;
            }
            if (_seedStep >= 0) TickSeed();

            float baseline = CurrentBaseline();
            float gameValue = Shader.GetGlobalFloat(ValueId);

            if (GlowMath.GameHasTurn(RoutineActive, gameValue, baseline))
            {
                if (!_wasGameTurn) Shader.SetGlobalVector(ColorId, PlayerColor());
                _wasGameTurn = true;
                _zeroWritten = false;
            }
            else if (baseline <= 0f)
            {
                if (_wasGameTurn || !_zeroWritten) Shader.SetGlobalFloat(ValueId, 0f);
                _wasGameTurn = false;
                _zeroWritten = true;
            }
            else
            {
                Shader.SetGlobalFloat(ValueId, baseline);
                Shader.SetGlobalFloat(RadiusId, Plugin.GlowRadius.Value);
                Shader.SetGlobalVector(ColorId, CurrentColor());
                _wasGameTurn = false;
                _zeroWritten = false;
            }

            TickCharacters(GlowMath.ExcludeKindNow(baseline, Plugin.ExcludeVillagers.Value),
                GlowMath.ExcludeKindNow(baseline, Plugin.ExcludeStructures.Value));
        }
        catch (Exception e)
        {
            if (_lateFailed) return;
            _lateFailed = true;
            Plugin.Logger.LogWarning("LateUpdate failed: " + e);
        }
    }

    /// <summary>The always-on strength: the blend for the game's current daylight (full day while the weather system is unavailable).</summary>
    private float CurrentBaseline()
    {
        float daylight = 1f;
        float time = float.NaN;
        bool found = false;
        try
        {
            float now = Time.time;
            if (_weather == null && now >= _nextWeatherLookup)
            {
                _nextWeatherLookup = now + 2f;
                _weather = FindObjectOfType<WeatherSystem>();
                found = _weather != null;
            }
            if (_weather != null)
            {
                daylight = _weather.DayNightValue;
                time = _weather.narmalizedDayTime;
            }
        }
        catch (Exception e)
        {
            if (!_weatherFailed)
            {
                _weatherFailed = true;
                Plugin.Logger.LogWarning("Reading the day and night failed: " + e);
            }
        }

        float strength = GlowMath.DayNightStrength(Plugin.DayStrength.Value, Plugin.NightStrength.Value, daylight);
        if (_weather != null && (found || Time.time >= _nextStrengthLog))
        {
            _nextStrengthLog = Time.time + 60f;
            Plugin.Logger.LogInfo($"daylight {daylight:0.###} time {time:0.###} strength {strength:0.###}");
        }
        return strength;
    }

    /// <summary>The configured color, or the player's own Resource Finder color when it is empty or invalid.</summary>
    private Vector4 CurrentColor()
    {
        string cfg = Plugin.GlowColor.Value;
        if (!string.Equals(cfg, _parsedFrom, StringComparison.Ordinal))
        {
            _parsedFrom = cfg;
            if (GlowMath.TryParseHexColor(cfg, out float r, out float g, out float b))
            {
                _ourColor = new Vector4(r, g, b, 1f);
                _hasOurColor = true;
            }
            else
            {
                _hasOurColor = false;
                if (!string.IsNullOrEmpty(cfg))
                {
                    Plugin.Logger.LogWarning($"GlowColor '{cfg}' is not #RRGGBB; using the game's Resource Finder color.");
                }
            }
        }
        return _hasOurColor ? _ourColor : PlayerColor();
    }

    private Vector4 PlayerColor()
    {
        try
        {
            if (_settings == null) _settings = FindObjectOfType<SettingsManager>();
            if (_settings != null)
            {
                Color c = _settings.ResourceFinderColor.Value;
                return new Vector4(c.r, c.g, c.b, 1f);
            }
        }
        catch (Exception e)
        {
            if (!_colorFailed)
            {
                _colorFailed = true;
                Plugin.Logger.LogWarning("Reading the Resource Finder color failed: " + e);
            }
        }
        return new Vector4(1f, 1f, 1f, 1f);
    }

    private const int KindCount = 4;
    private const int RendererBudget = 150;
    private const int MaxVisits = 200;
    private const float CacheSeconds = 10f;

    private readonly bool[] _kindOn = new bool[KindCount];
    private readonly Queue<Known> _restore = new Queue<Known>();
    private Known _cur;
    private int _seedStep = -1;
    private long _frameSeedTicks;

    private static bool KindExcluded(int kind, bool characters, bool structures) =>
        kind == 3 ? structures : characters;

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    /// <summary>One kind per frame over four frames, run when the player agent is found: adds every object already in the world to the registry.</summary>
    private void TickSeed()
    {
        long t0 = Stopwatch.GetTimestamp();
        int kind = _seedStep;
        _seedStep = kind + 1 >= KindCount ? -1 : kind + 1;
        if (kind == 0) foreach (var v in FindObjectsOfType<Villager>()) GlowRegistry.Seed(v, 0);
        else if (kind == 1) foreach (var p in FindObjectsOfType<PlayerCharacter>()) GlowRegistry.Seed(p, 1);
        else if (kind == 2) foreach (var c in FindObjectsOfType<Creature>()) GlowRegistry.Seed(c, 2);
        else foreach (var st in FindObjectsOfType<Structure>()) GlowRegistry.Seed(st, 3);
        _frameSeedTicks += Stopwatch.GetTimestamp() - t0;
    }

    /// <summary>While excluding, writes a value of 0 into each renderer's property block under every registered villager,
    /// player, live creature and structure (which carries the items stored in or on it), round-robin within one renderer
    /// budget per frame. Writes the current global value back, under the same budget, once a kind's exclusion stops.
    /// A creature that died while excluded has its block cleared so the carcass glows.</summary>
    private void TickCharacters(bool characters, bool structures)
    {
        bool anyOn = characters || structures;
        if (!anyOn && !_wasExcluding && _restore.Count == 0 && _cur == null)
        {
            _frameSeedTicks = 0;
            return;
        }
        try
        {
            float now = Time.time;
            var sw = Stopwatch.StartNew();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            bool started = !_wasExcluding;
            for (int kind = 0; kind < KindCount; kind++)
            {
                bool on = KindExcluded(kind, characters, structures);
                if (_kindOn[kind] && !on)
                {
                    foreach (var k in GlowRegistry.List)
                    {
                        if (k.Kind != kind || k.Removed || !(k.Touched || k.Excluded)) continue;
                        k.Restoring = true;
                        _restore.Enqueue(k);
                    }
                }
                else if (!_kindOn[kind] && on)
                {
                    foreach (var k in GlowRegistry.List)
                    {
                        if (k.Kind == kind && !k.Removed) GlowRegistry.Fresh.Enqueue(k);
                    }
                }
                _kindOn[kind] = on;
            }
            _wasExcluding = anyOn;
            if (started && anyOn) _nextCharLog = now + 60f;

            long baseTicks = _frameSeedTicks + GlowRegistry.TakeTicks();
            long lookupTicks = baseTicks;
            long writeTicks = 0;
            _frameSeedTicks = 0;
            RunBudget(now, anyOn, ref lookupTicks, ref writeTicks);

            sw.Stop();
            double lookupMs = Ms(lookupTicks);
            double writeMs = Ms(writeTicks);
            _worstFrameMs = Math.Max(_worstFrameMs, sw.Elapsed.TotalMilliseconds + Ms(baseTicks));
            _worstLookupMs = Math.Max(_worstLookupMs, lookupMs);
            _worstWriteMs = Math.Max(_worstWriteMs, writeMs);
            if (anyOn && now >= _nextCharLog)
            {
                _nextCharLog = now + 60f;
                int[] counts = new int[KindCount];
                foreach (var k in GlowRegistry.List)
                {
                    if (!k.Removed && _kindOn[k.Kind]) counts[k.Kind]++;
                }
                Plugin.Logger.LogInfo($"excluded from glow: {counts[0]} villagers, {counts[1]} players, {counts[2]} creatures, {counts[3]} structures; worst frame {_worstFrameMs:0.00} ms (lookup {_worstLookupMs:0.00}, write {_worstWriteMs:0.00}), seeded {GlowRegistry.Seeded}, spawned {GlowRegistry.SpawnedCount}, despawned {GlowRegistry.DespawnedCount}");
                _worstFrameMs = 0;
                _worstLookupMs = 0;
                _worstWriteMs = 0;
            }
        }
        catch (Exception e)
        {
            _wasExcluding = anyOn;
            _cur = null;
            for (int kind = 0; kind < KindCount; kind++) _kindOn[kind] = KindExcluded(kind, characters, structures);
            if (_villagerFailed) return;
            _villagerFailed = true;
            Plugin.Logger.LogWarning("Glow exclusion failed: " + e);
        }
    }

    /// <summary>Writes blocks to at most RendererBudget renderers: restores first, then new entries, then the round-robin.
    /// Calls GetComponentsInChildren for at most one entry per frame.</summary>
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private void RunBudget(float now, bool anyOn, ref long lookupTicks, ref long writeTicks)
    {
        int budget = RendererBudget;
        int visits = 0;
        bool refreshed = false;
        float global = Shader.GetGlobalFloat(ValueId);
        while (budget > 0 && visits < MaxVisits)
        {
            if (_cur == null)
            {
                long t = Stopwatch.GetTimestamp();
                _cur = NextKnown(anyOn, ref visits);
                lookupTicks += Stopwatch.GetTimestamp() - t;
                if (_cur == null) break;
                _cur.Index = 0;
            }
            var k = _cur;
            if (k.Removed || k.Comp == null)
            {
                GlowRegistry.Drop(k);
                _cur = null;
                continue;
            }
            if (k.Index == 0)
            {
                if (k.Rends == null || now - k.RendTime > CacheSeconds)
                {
                    if (refreshed) break;
                    refreshed = true;
                    long t = Stopwatch.GetTimestamp();
                    k.Rends = k.Comp.GetComponentsInChildren<Renderer>(true);
                    k.RendTime = now;
                    lookupTicks += Stopwatch.GetTimestamp() - t;
                }
                k.Mode = ModeFor(k, global);
            }

            long w = Stopwatch.GetTimestamp();
            var rends = k.Rends;
            int take = k.Mode == ModeNone ? 0 : GlowMath.TakeBudget(rends.Length - k.Index, budget);
            for (int i = 0; i < take; i++)
            {
                var r = rends[k.Index + i];
                if (r == null) continue;
                if (k.Mode == ModeClear)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(ValueId, k.Mode == ModeZero ? 0f : global);
                r.SetPropertyBlock(_mpb);
            }
            writeTicks += Stopwatch.GetTimestamp() - w;
            k.Index += take;
            budget -= take;
            if (k.Mode == ModeNone || k.Index >= rends.Length)
            {
                k.Restoring = false;
                k.Index = 0;
                _cur = null;
            }
        }
    }

    private const int ModeNone = 0, ModeZero = 1, ModeValue = 2, ModeClear = 3;

    /// <summary>What to write for this entry now: the exclusion value 0, the restored global value, a cleared block, or nothing.</summary>
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private static int ModeFor(Known k, float global)
    {
        bool exclude = !k.Restoring;
        if (k.Kind != 2)
        {
            if (exclude)
            {
                k.Touched = true;
                return ModeZero;
            }
            if (!k.Touched) return ModeNone;
            k.Touched = false;
            return ModeValue;
        }
        var c = k.Comp.TryCast<Creature>();
        if (c == null) return ModeNone;
        if (GlowMath.ExcludeCreature(exclude, c.IsDead))
        {
            k.Excluded = true;
            return ModeZero;
        }
        if (!k.Excluded) return ModeNone;
        k.Excluded = false;
        return c.IsDead ? ModeClear : ModeValue;
    }

    /// <summary>The next entry to work on, or null: restores, then newly registered, then round-robin over the registry.
    /// Drops entries that were despawned or whose component is gone.</summary>
    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private Known NextKnown(bool anyOn, ref int visits)
    {
        while (_restore.Count > 0)
        {
            var k = _restore.Dequeue();
            if (!k.Removed) return k;
        }
        if (!anyOn) return null;
        var fresh = GlowRegistry.Fresh;
        while (fresh.Count > 0 && visits < MaxVisits)
        {
            visits++;
            var k = fresh.Dequeue();
            if (!k.Removed && _kindOn[k.Kind]) return k;
        }
        var list = GlowRegistry.List;
        while (list.Count > 0 && visits < MaxVisits)
        {
            visits++;
            _cursor = (_cursor + 1) % list.Count;
            var k = list[_cursor];
            if (k.Removed || k.Comp == null)
            {
                GlowRegistry.Drop(k);
                list.RemoveAt(_cursor);
                _cursor--;
                continue;
            }
            if (_kindOn[k.Kind]) return k;
        }
        return null;
    }
}

internal sealed class Known
{
    public int Kind;
    public int Id;
    public Component Comp;
    public bool Excluded;
    public bool Touched;
    public bool Removed;
    public bool Restoring;
    public Renderer[] Rends;
    public float RendTime;
    public int Index;
    public int Mode;
}

/// <summary>Every live villager, player, creature and structure, kept by the Spawned and Despawned patches and seeded once per world load.</summary>
internal static class GlowRegistry
{
    internal static readonly List<Known> List = new List<Known>();
    internal static readonly Queue<Known> Fresh = new Queue<Known>();
    private static readonly Dictionary<int, Known> ById = new Dictionary<int, Known>();
    private static long _ticks;
    internal static int Seeded, SpawnedCount, DespawnedCount;

    internal static long TakeTicks()
    {
        long t = _ticks;
        _ticks = 0;
        return t;
    }

    private static bool Add(Component c, int kind)
    {
        if (c == null) return false;
        int id = c.GetInstanceID();
        if (ById.ContainsKey(id)) return false;
        var k = new Known { Kind = kind, Id = id, Comp = c };
        List.Add(k);
        ById[id] = k;
        Fresh.Enqueue(k);
        return true;
    }

    internal static void Seed(Component c, int kind)
    {
        if (Add(c, kind)) Seeded++;
    }

    internal static void Spawned(Component c, int kind)
    {
        long t = Stopwatch.GetTimestamp();
        if (Add(c, kind)) SpawnedCount++;
        _ticks += Stopwatch.GetTimestamp() - t;
    }

    internal static void Despawned(Component c)
    {
        long t = Stopwatch.GetTimestamp();
        if (c != null && ById.TryGetValue(c.GetInstanceID(), out var k))
        {
            Drop(k);
            DespawnedCount++;
        }
        _ticks += Stopwatch.GetTimestamp() - t;
    }

    /// <summary>Marks the entry gone; the round-robin removes it from the list when it next reaches it.</summary>
    internal static void Drop(Known k)
    {
        k.Removed = true;
        if (ById.TryGetValue(k.Id, out var cur) && cur == k) ById.Remove(k.Id);
    }
}

[HarmonyPatch]
internal static class GlowPatches
{
    private static bool _pressFailed, _routineFailed;

    [HarmonyPostfix, HarmonyPatch(typeof(PlayerInteractionAgent), nameof(PlayerInteractionAgent.ActivateResourceFinder))]
    private static void Press(PlayerInteractionAgent __instance)
    {
        try
        {
            AlwaysOnGlow.RoutineActive = true;
            AlwaysOnGlow.RoutineDeadline = Time.time + __instance.finderActiveTime + __instance.finderFadeOutTime + 1f;
        }
        catch (Exception e)
        {
            if (_pressFailed) return;
            _pressFailed = true;
            Plugin.Logger.LogWarning("ActivateResourceFinder patch failed: " + e);
        }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(PlayerInteractionAgent.__ResourceFinderRoutine_d__189), "MoveNext")]
    private static void Routine(bool __result)
    {
        try
        {
            if (!__result) AlwaysOnGlow.RoutineActive = false;
        }
        catch (Exception e)
        {
            if (_routineFailed) return;
            _routineFailed = true;
            Plugin.Logger.LogWarning("Resource Finder routine patch failed: " + e);
        }
    }

    internal static int KindOf(Component c)
    {
        if (c.TryCast<Villager>() != null) return 0;
        if (c.TryCast<PlayerCharacter>() != null) return 1;
        if (c.TryCast<Creature>() != null) return 2;
        return c.TryCast<Structure>() != null ? 3 : -1;
    }

    private static int _spawnedHooks = -1, _despawnedHooks = -1;

    internal static IEnumerable<MethodBase> Targets(string method)
    {
        int found = 0;
        foreach (var t in new[] { typeof(Villager), typeof(PlayerCharacter), typeof(Creature), typeof(Structure) })
        {
            var m = AccessTools.Method(t, method);
            if (m == null)
            {
                Plugin.Logger.LogWarning($"spawn hooks: {t.Name}.{method} not found");
                continue;
            }
            found++;
            yield return m;
        }
        if (method == "Spawned") _spawnedHooks = found; else _despawnedHooks = found;
        if (_spawnedHooks >= 0 && _despawnedHooks >= 0)
            Plugin.Logger.LogInfo($"spawn hooks: {_spawnedHooks} Spawned, {_despawnedHooks} Despawned");
    }
}

[HarmonyPatch]
internal static class SpawnedPatch
{
    private static bool _failed;

    private static IEnumerable<MethodBase> TargetMethods() => GlowPatches.Targets("Spawned");

    private static void Postfix(object __instance)
    {
        try
        {
            if (__instance is not Component c) return;
            int kind = GlowPatches.KindOf(c);
            if (kind >= 0) GlowRegistry.Spawned(c, kind);
        }
        catch (Exception e)
        {
            if (_failed) return;
            _failed = true;
            Plugin.Logger.LogWarning("Spawned patch failed: " + e);
        }
    }
}

[HarmonyPatch]
internal static class DespawnedPatch
{
    private static bool _failed;

    private static IEnumerable<MethodBase> TargetMethods() => GlowPatches.Targets("Despawned");

    private static void Postfix(object __instance)
    {
        try
        {
            if (__instance is Component c) GlowRegistry.Despawned(c);
        }
        catch (Exception e)
        {
            if (_failed) return;
            _failed = true;
            Plugin.Logger.LogWarning("Despawned patch failed: " + e);
        }
    }
}
