using Content.Shared.Spatial;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Spatial;

/// <summary>Read-only B01 review export from actual resolved content prototypes.</summary>
public sealed partial class SpatialAssetCatalogCommand : IConsoleCommand
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceManager _resources = default!;
    public string Command => "spatial_asset_catalog";
    public string Description => "Export resolved spatial asset/profile coverage to user data.";
    public string Help => "spatial_asset_catalog";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0) { shell.WriteError(Help); return; }
        var report = SpatialAssetCatalog.Build(_prototypes, _resources);
        _resources.UserData.WriteAllText(new ResPath("/spatial-asset-catalog.yml"), SpatialAssetCatalogYaml.Write(report));
        shell.WriteLine($"Saved spatial-asset-catalog.yml: {report.Entities.Length} entities, {report.Tiles.Length} tiles, {report.Resources.Length} resources, "
            + $"{report.UnboundEntities} unbound visual/mechanical entities, {report.InvalidBindings} invalid bindings. Review is still required.");
    }
}
