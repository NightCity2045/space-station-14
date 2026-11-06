using System;
using System.Linq;
using System.Text.Json;
using Content.Shared.Spatial.Prototypes;
using Content.Shared.Maps;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Shared.Spatial;

public sealed record SpatialAssetState(string Name, int Directions, float[][]? Delays, bool ImageExists);
public sealed record SpatialAssetResource(string Path, int? Width, int? Height, SpatialAssetState[] States, string[] Errors);
public sealed record SpatialAssetEntry(string Prototype, string[] Parents, string[] Components,
    string? Profile, string? Kind, string[] SupportedStates, string[] Resources, string[] Issues,
    string ReviewStatus, string Notes);
public sealed record SpatialTileAssetEntry(string Prototype, string[] Parents, bool LegacySubfloor,
    string? BaseTurf, string[] Resources, string[] Issues, string ReviewStatus);
public sealed record SpatialAssetCatalogReport(int SchemaVersion, string Scope, SpatialAssetEntry[] Entities,
    SpatialTileAssetEntry[] Tiles, SpatialAssetResource[] Resources, int UnboundEntities, int InvalidBindings);

/// <summary>
/// B01 review inventory over the engine's resolved prototype mappings. No custom
/// YAML inheritance, no entity spawning and no guessed profile or collider. An
/// explicit binding is metadata, not proof that dynamic gameplay states are covered.
/// </summary>
public static class SpatialAssetCatalog
{
    public static SpatialAssetCatalogReport Build(IPrototypeManager prototypes, IResourceManager resources)
    {
        var entries = new List<SpatialAssetEntry>();
        var assets = new SortedDictionary<string, SpatialAssetResource>(StringComparer.Ordinal);
        var unbound = 0;
        var invalid = 0;
        foreach (var prototype in prototypes.EnumeratePrototypes<EntityPrototype>().OrderBy(p => p.ID, StringComparer.Ordinal))
        {
            var issues = new List<string>();
            var componentMappings = new Dictionary<string, MappingDataNode>(StringComparer.Ordinal);
            if (!prototypes.TryGetMapping<EntityPrototype>(prototype.ID, out var mapping))
                issues.Add("resolved_mapping_missing");
            else if (mapping.TryGet<SequenceDataNode>("components", out var components))
            {
                foreach (var node in components.Sequence)
                    if (node is MappingDataNode component && Text(component, "type") is { } type)
                        componentMappings[type] = component;
            }
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var component in componentMappings.Values)
                CollectResources(component, paths);
            foreach (var path in paths)
                if (!assets.ContainsKey(path)) assets.Add(path, InspectResource(path, resources));

            var hasVisual = componentMappings.ContainsKey("Sprite") || componentMappings.ContainsKey("Icon") || paths.Count != 0;
            var mechanical = componentMappings.Keys.Any(IsMechanical);
            if (mechanical && !hasVisual) issues.Add("mechanical_visual_source_requires_review");
            var profileId = componentMappings.TryGetValue("SpatialPresentation", out var binding) ? Text(binding, "profile") : null;
            SpatialPresentationPrototype? profile = null;
            if (profileId == null)
            {
                if (hasVisual || mechanical) { unbound++; issues.Add("presentation_profile_missing"); }
            }
            else if (!prototypes.TryIndex<SpatialPresentationPrototype>(profileId, out profile))
            {
                invalid++; issues.Add("presentation_profile_unknown");
            }
            else
            {
                try { profile.Validate(); }
                catch (ArgumentException) { invalid++; issues.Add("presentation_profile_invalid"); }
                // A fallback is only a proposed decoration classification. Actual
                // mechanical coverage must be reviewed, never inferred from alpha.
                if (profile.DecorativeFallback && mechanical)
                {
                    invalid++; issues.Add("decorative_fallback_has_mechanical_components");
                }
            }
            foreach (var path in paths)
                if (assets[path].Errors.Length != 0) issues.Add("resource_invalid:" + path);
            entries.Add(new SpatialAssetEntry(prototype.ID, prototype.Parents ?? Array.Empty<string>(),
                componentMappings.Keys.Order(StringComparer.Ordinal).ToArray(), profileId, profile?.Kind.ToString(),
                profile?.SupportedStates.ToArray() ?? Array.Empty<string>(), paths.ToArray(), issues.ToArray(),
                "unreviewed", profile?.Notes ?? ""));
        }
        var tiles = new List<SpatialTileAssetEntry>();
        foreach (var tile in prototypes.EnumeratePrototypes<ContentTileDefinition>().OrderBy(p => p.ID, StringComparer.Ordinal))
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            var issues = new List<string>();
            if (prototypes.TryGetMapping<ContentTileDefinition>(tile.ID, out var mapping)) CollectResources(mapping, paths);
            else issues.Add("resolved_mapping_missing");
            foreach (var path in paths)
            {
                if (!assets.ContainsKey(path)) assets.Add(path, InspectResource(path, resources));
                if (assets[path].Errors.Length != 0) issues.Add("resource_invalid:" + path);
            }
            tiles.Add(new SpatialTileAssetEntry(tile.ID, tile.Parents ?? Array.Empty<string>(), tile.IsSubFloor,
                tile.BaseTurf?.ToString(), paths.ToArray(), issues.ToArray(), "unreviewed"));
        }
        return new SpatialAssetCatalogReport(1,
            "Concrete entity/tile prototypes resolved by the engine; explicit asset/profile inventory, not completed B01 acceptance or dynamic-state coverage. Legacy subfloor flags are not structural geometry or airtightness.",
            entries.ToArray(), tiles.ToArray(), assets.Values.ToArray(), unbound, invalid);
    }

    private static bool IsMechanical(string component) => component is
        "Physics" or "Fixtures" or "SpatialBody" or "SpatialMotor" or "SpatialGeometry" or "Door" or "Wall"
        or "Airtight" or "ContainerContainer" or "Item" or "Inventory" or "Hands" or "Damageable" or "PowerConsumer"
        or "ApcPowerReceiver" or "AtmosDevice" or "Pipe" or "Construction";

    private static string? Text(MappingDataNode mapping, string key)
        => mapping.TryGet<ValueDataNode>(key, out var value) ? value.Value : null;

    private static void CollectResources(DataNode node, ISet<string> resources)
    {
        switch (node)
        {
            case MappingDataNode mapping:
                foreach (var child in mapping.Children.Values) CollectResources(child, resources);
                break;
            case SequenceDataNode sequence:
                foreach (var child in sequence.Sequence) CollectResources(child, resources);
                break;
            case ValueDataNode value when value.Value.EndsWith(".rsi", StringComparison.OrdinalIgnoreCase)
                || value.Value.EndsWith(".png", StringComparison.OrdinalIgnoreCase):
                var path = value.Value.Replace('\\', '/');
                if (!path.StartsWith('/')) path = "/Textures/" + path;
                resources.Add(path);
                break;
        }
    }

    private static SpatialAssetResource InspectResource(string path, IResourceManager resources)
    {
        var errors = new List<string>();
        var states = new List<SpatialAssetState>();
        int? width = null, height = null;
        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            if (!resources.ContentFileExists(new ResPath(path))) errors.Add("image_missing");
        }
        else
        {
            if (!resources.TryContentFileRead(new ResPath(path + "/meta.json"), out var stream)) errors.Add("rsi_metadata_missing");
            else
            {
                using (stream)
                {
                    try
                    {
                        using var document = JsonDocument.Parse(stream);
                        var root = document.RootElement;
                        width = root.GetProperty("size").GetProperty("x").GetInt32();
                        height = root.GetProperty("size").GetProperty("y").GetInt32();
                        if (width <= 0 || height <= 0) errors.Add("rsi_invalid_dimensions");
                        foreach (var state in root.GetProperty("states").EnumerateArray())
                        {
                            var name = state.GetProperty("name").GetString() ?? "";
                            var directions = state.TryGetProperty("directions", out var direction) ? direction.GetInt32() : 1;
                            float[][]? delays = null;
                            if (state.TryGetProperty("delays", out var delay))
                                delays = delay.EnumerateArray().Select(row => row.EnumerateArray().Select(v => v.GetSingle()).ToArray()).ToArray();
                            var exists = resources.ContentFileExists(new ResPath(path + "/" + name + ".png"));
                            states.Add(new SpatialAssetState(name, directions, delays, exists));
                            if (string.IsNullOrWhiteSpace(name) || directions is not (1 or 4 or 8) || !exists)
                                errors.Add("rsi_invalid_state:" + name);
                            if (delays != null && (delays.Length != directions || delays.Any(row => row.Length == 0
                                || row.Any(seconds => !float.IsFinite(seconds) || seconds <= 0))))
                                errors.Add("rsi_invalid_delays:" + name);
                        }
                        if (states.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count() != states.Count)
                            errors.Add("rsi_duplicate_states");
                    }
                    catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
                    {
                        errors.Add("rsi_metadata_invalid:" + e.GetType().Name);
                    }
                }
            }
        }
        return new SpatialAssetResource(path, width, height, states.OrderBy(s => s.Name, StringComparer.Ordinal).ToArray(), errors.ToArray());
    }
}
