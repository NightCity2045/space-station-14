using System.IO;
using System.Linq;
using System.Text.Json;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Spatial;
using Content.Shared.Maps;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class SpatialAssetCatalogTest : GameTest
{
    [Test]
    public async Task CatalogUsesResolvedInheritanceAndDoesNotHideUncoveredObjects()
    {
        SpatialAssetCatalogReport report = null!;
        await Client.WaitAssertion(() =>
        {
            var prototypes = Client.ResolveDependency<IPrototypeManager>();
            report = SpatialAssetCatalog.Build(prototypes, Client.ResolveDependency<IResourceManager>());
            Assert.That(report.Entities.Length, Is.EqualTo(prototypes.EnumeratePrototypes<EntityPrototype>().Count()));
            Assert.That(report.Tiles.Length, Is.EqualTo(prototypes.EnumeratePrototypes<ContentTileDefinition>().Count()));
            Assert.That(report.Entities.Select(e => e.Prototype).Distinct().Count(), Is.EqualTo(report.Entities.Length));
            var wall = report.Entities.Single(e => e.Prototype == "WallSolid");
            Assert.That(wall.Components, Does.Contain("Wall"));
            Assert.That(wall.Components, Does.Contain("Sprite"));
            Assert.That(wall.Resources, Does.Contain("/Textures/Structures/Walls/solid.rsi"));
            if (wall.Profile == null) Assert.That(wall.Issues, Does.Contain("presentation_profile_missing"));
            var art = report.Resources.Single(r => r.Path == "/Textures/Structures/Walls/solid.rsi");
            Assert.That(art.States, Is.Not.Empty);
            Assert.That(art.States.All(s => s.ImageExists), Is.True);
            Assert.That(art.Width, Is.GreaterThan(0));
            Assert.That(report.Entities.All(e => e.ReviewStatus == "unreviewed"), Is.True,
                "A resource inventory must not assert gameplay/physical coverage from a binding alone.");
            Assert.That(report.UnboundEntities,
                Is.EqualTo(report.Entities.Count(e => e.Issues.Contains("presentation_profile_missing"))));
        });
        var output = Path.Combine(TestContext.CurrentContext.WorkDirectory, "spatial-asset-catalog.json");
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddTestAttachment(output, "Resolved B01 asset and explicit-profile review inventory");
    }
}
