using System;
using System.Linq;
using System.Numerics;
using Content.Shared.Spatial.Prototypes;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Spatial;

namespace Content.Tests.Shared.Spatial;

[TestFixture]
public sealed class SpatialProfileTest : ContentUnitTest
{
    [Test]
    public void AuthoredProfilesDeserializeWithoutInferringMechanicsFromVisuals()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        prototypes.Initialize();
        prototypes.LoadString("""
            - type: spatialPresentation
              id: ProfileFixture
              kind: DirectionalBillboard
              size: 0.8, 1.8, 0.6
              pivot: 0.5, 0
              maximumTilt: 0.4
              supportedStates: [standing, lying]
            - type: spatialBarrier
              id: GlassFixture
              movement: false
              view: true
              projectiles: false
              gasConductance: 0
              soundTransmission: 0.2
            - type: spatialGeometry
              id: WindowFixture
              kind: Boundary
              material: metal
              barrier: GlassFixture
              height: 3
              thickness: 0.15
              boundaryOpenings:
              - start: 0.1
                end: 0.9
                bottom: 0.9
                top: 2.4
            - type: spatialGeometry
              id: FloorFixture
              kind: Slab
              material: floor
              footprint: -2,-2,2,2
              thickness: 0.2
              slabOpenings: ["-0.5,-0.5,0.5,0.5"]
            """);
        prototypes.ResolveResults();
        var presentation = prototypes.Index<SpatialPresentationPrototype>("ProfileFixture");
        Assert.That(presentation.Size, Is.EqualTo(new Vector3(0.8f, 1.8f, 0.6f)));
        Assert.That(presentation.SupportedStates, Is.EqualTo(new[] { "standing", "lying" }));
        var glass = prototypes.Index<SpatialBarrierPrototype>("GlassFixture");
        Assert.That(glass.View, Is.True);
        Assert.That(glass.Movement || glass.Projectiles, Is.False);
        Assert.That(glass.GasConductance, Is.Zero);
        Assert.That(glass.SoundTransmission, Is.EqualTo(0.2f));
        var window = prototypes.Index<SpatialGeometryPrototype>("WindowFixture");
        Assert.That(window.BoundaryOpenings, Has.Length.EqualTo(1));
        Assert.That(window.BoundaryOpenings[0].Top, Is.EqualTo(2.4f));
        var floor = prototypes.Index<SpatialGeometryPrototype>("FloorFixture");
        Assert.That(floor.SlabOpenings, Has.Length.EqualTo(1));
    }

    [Test]
    public void InvalidDimensionsAndStructuralFallbacksAreRejected()
    {
        var profile = new SpatialPresentationPrototype { Kind = SpatialPresentationKind.Box, Size = Vector3.One, Pivot = new Vector2(0.5f) };
        profile.Validate();
        profile.DecorativeFallback = true;
        Assert.Throws<ArgumentException>(profile.Validate);
        profile.DecorativeFallback = false; profile.MaximumTilt = 0.2f;
        Assert.Throws<ArgumentException>(profile.Validate);
        profile.MaximumTilt = 0; profile.Size = new Vector3(1, 1, float.NaN);
        Assert.Throws<ArgumentException>(profile.Validate);
        profile.Size = Vector3.One; profile.SupportedStates = new[] { "standing", "standing" };
        Assert.Throws<ArgumentException>(profile.Validate);
        Assert.Throws<ArgumentException>(() => new SpatialBarrierPrototype { SoundTransmission = float.NaN }.Validate());
        Assert.Throws<ArgumentException>(() => new SpatialBarrierPrototype { GasConductance = -0.1f }.Validate());
    }

    [Test]
    public void LegacyBlocksStayInTheirCellAndOpeningsRemainRealVoids()
    {
        var frame = new EntityUid(1); var layer = new LayerId(1);
        var surface = new SpatialSurfaceReference(frame, new SurfaceId(1));
        var block = new SpatialGeometryPrototype { Kind = SpatialGeometryKind.LegacyBlock, Material = "metal",
            Footprint = new Box2(-0.5f, -0.5f, 0.5f, 0.5f), Height = 3 };
        var slab = block.CreateSlab(surface, layer);
        var parts = slab.BuildSolidParts(new LayerDefinition(4, "Upper"));
        Assert.That(parts, Has.Count.EqualTo(1));
        Assert.That(parts[0].Bounds.Min, Is.EqualTo(new Vector3(-0.5f, -0.5f, 4)));
        Assert.That(parts[0].Bounds.Max, Is.EqualTo(new Vector3(0.5f, 0.5f, 7)));
        var window = new SpatialGeometryPrototype { Kind = SpatialGeometryKind.Boundary, Material = "metal",
            Thickness = 0.15f, Height = 3, BoundaryOpenings = new[] { new BoundaryOpening(0.1f, 0.9f, 0.9f, 2.4f) } };
        var key = new BoundaryKey(frame, layer, 0, 0, BoundaryAxis.Horizontal);
        var boundary = window.CreateBoundary(key, new SurfaceId(2), 1);
        var solids = boundary.BuildSolidParts(new LayerDefinition(0, "Ground"));
        Assert.That(solids.Any(p => p.Bounds.Contains(new Vector3(0.5f, 0, 1.5f))), Is.False);
        Assert.That(solids.Any(p => p.Bounds.Contains(new Vector3(0.5f, 0, 0.5f))), Is.True);
        Assert.Throws<ArgumentException>(() => window.CreateBoundary(key, new SurfaceId(2), 0.5f));
        Assert.Throws<InvalidOperationException>(() => block.CreateBoundary(key, new SurfaceId(2), 1));
        var floor = new SpatialGeometryPrototype { Kind = SpatialGeometryKind.Slab, Material = "floor",
            Footprint = new Box2(-2, -2, 2, 2), Thickness = 0.2f, SlabOpenings = new[] { new Box2(-0.5f, -0.5f, 0.5f, 0.5f) } };
        var deck = floor.CreateSlab(surface, layer).BuildSolidParts(new LayerDefinition(4, "Upper"));
        Assert.That(deck.Any(p => p.Bounds.Contains(new Vector3(0, 0, 3.9f))), Is.False);
        Assert.That(deck.Any(p => p.Bounds.Contains(new Vector3(1, 0, 3.9f))), Is.True);
    }
}
