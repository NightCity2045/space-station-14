using System.Globalization;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace Content.Shared.Spatial;

/// <summary>Explicit review export using the client's existing sandboxed YAML API.</summary>
public static class SpatialAssetCatalogYaml
{
    public static string Write(SpatialAssetCatalogReport report)
    {
        var root = new YamlMappingNode
        {
            { "SchemaVersion", Number(report.SchemaVersion) },
            { "Scope", report.Scope },
            { "UnboundEntities", Number(report.UnboundEntities) },
            { "InvalidBindings", Number(report.InvalidBindings) },
            { "Entities", new YamlSequenceNode(report.Entities.Select(e => new YamlMappingNode
            {
                { "Prototype", e.Prototype }, { "Parents", Strings(e.Parents) },
                { "Components", Strings(e.Components) }, { "Profile", Scalar(e.Profile) },
                { "Kind", Scalar(e.Kind) }, { "SupportedStates", Strings(e.SupportedStates) },
                { "Resources", Strings(e.Resources) }, { "Issues", Strings(e.Issues) },
                { "ReviewStatus", e.ReviewStatus }, { "Notes", e.Notes },
            })) },
            { "Tiles", new YamlSequenceNode(report.Tiles.Select(t => new YamlMappingNode
            {
                { "Prototype", t.Prototype }, { "Parents", Strings(t.Parents) },
                { "LegacySubfloor", t.LegacySubfloor ? "true" : "false" },
                { "BaseTurf", Scalar(t.BaseTurf) }, { "Resources", Strings(t.Resources) },
                { "Issues", Strings(t.Issues) }, { "ReviewStatus", t.ReviewStatus },
            })) },
            { "Resources", new YamlSequenceNode(report.Resources.Select(r => new YamlMappingNode
            {
                { "Path", r.Path }, { "Width", Scalar(r.Width?.ToString(CultureInfo.InvariantCulture)) },
                { "Height", Scalar(r.Height?.ToString(CultureInfo.InvariantCulture)) },
                { "Errors", Strings(r.Errors) },
                { "States", new YamlSequenceNode(r.States.Select(s => new YamlMappingNode
                {
                    { "Name", s.Name }, { "Directions", Number(s.Directions) },
                    { "ImageExists", s.ImageExists ? "true" : "false" },
                    { "Delays", s.Delays == null ? Scalar(null) : new YamlSequenceNode(s.Delays.Select(row =>
                        new YamlSequenceNode(row.Select(v => Scalar(v.ToString("R", CultureInfo.InvariantCulture)))))) },
                })) },
            })) },
        };
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new YamlStream(new YamlDocument(root)).Save(writer, false);
        return writer.ToString();
    }

    private static YamlScalarNode Number(int value) => Scalar(value.ToString(CultureInfo.InvariantCulture));
    private static YamlScalarNode Scalar(string? value) => new(value);
    private static YamlSequenceNode Strings(string[] values) => new(values.Select(Scalar));
}
