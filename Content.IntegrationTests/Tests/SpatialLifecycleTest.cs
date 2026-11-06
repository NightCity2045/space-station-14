using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Spatial;
using Robust.Shared.Spatial.Physics;

namespace Content.IntegrationTests.Tests;

/// <summary>Pair qualification for engine components which require structural context.</summary>
[TestFixture]
public sealed class SpatialLifecycleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task SpatialWorldGeometryAndBodyInitializeSimulateAndDeleteInContent()
    {
        await Server.WaitAssertion(() =>
        {
            var entities = SEntMan;
            var spatial = entities.System<SharedSpatialSystem>();
            var physics = entities.System<SharedSpatialPhysicsSystem>();
            var geometry = entities.System<SharedGeometrySystem>();
            var transforms = entities.System<SharedTransformSystem>();
            for (var cycle = 0; cycle < 3; cycle++)
            {
                var world = spatial.CreateWorld();
                try
                {
                    physics.EnablePhysics(world);
                    var map = entities.GetComponent<MapComponent>(world).MapId;
                    var frame = entities.CreateEntityUninitialized(null, world, RigidTransform3.Identity);
                    var layers = entities.AddComponent<SpatialFrameComponent>(frame);
                    var layer = new LayerId(7);
                    spatial.ConfigureInitialLayers((frame, layers), new Dictionary<LayerId, LayerDefinition>
                    {
                        [layer] = new(4, "Upper floor")
                    });
                    var component = entities.AddComponent<SpatialGeometryComponent>(frame);
                    var surface = new SpatialSurfaceReference(frame, new SurfaceId(1));
                    geometry.ConfigureInitialGeometry((frame, component), Array.Empty<BoundarySegment>(),
                        new[] { new SlabSurface(surface, layer, new Box2(-5, -5, 5, 5), 0, 0.2f) });
                    entities.InitializeAndStartEntity(frame, map);
                    var body = entities.CreateEntityUninitialized(null, world,
                        new RigidTransform3(new Vector3(0, 0, 8), Quaternion.Identity));
                    var bodyComp = entities.AddComponent<SpatialBodyComponent>(body);
                    physics.ConfigureInitialBody((body, bodyComp), new SpatialBodyDefinition(SpatialMotionOwner.DynamicBody,
                        new[] { new SpatialColliderDefinition(new SpatialSphereShape(0.25f), RigidTransform3.Identity) }));
                    entities.InitializeAndStartEntity(body, map);
                    var link = entities.CreateEntityUninitialized(null, world, RigidTransform3.Identity);
                    var connection = entities.AddComponent<SpatialConstraintComponent>(link);
                    physics.ConfigureInitialConstraint((link, connection), new SpatialConstraintDefinition(frame, body,
                        SpatialConstraintKind.DistanceRange, localAnchorA: new Vector3(0, 0, 4),
                        maximumDistance: 10, collideConnected: true));
                    entities.InitializeAndStartEntity(link, map);
                    var actor = entities.CreateEntityUninitialized(null, world,
                        new RigidTransform3(new Vector3(-2, 0, 8), Quaternion.Identity));
                    var motor = entities.AddComponent<SpatialMotorComponent>(actor);
                    var actorBody = entities.AddComponent<SpatialBodyComponent>(actor);
                    physics.ConfigureInitialMotor((actor, motor), SpatialMotorSettings.Default);
                    physics.ConfigureInitialBody((actor, actorBody), new SpatialBodyDefinition(SpatialMotionOwner.Motor,
                        new[] { new SpatialColliderDefinition(new SpatialCapsuleShape(0.3f, 1), RigidTransform3.Identity) },
                        mass: 80, material: SpatialContactMaterial.Default with { Friction = 0 }));
                    entities.InitializeAndStartEntity(actor, map);
                    for (var tick = 0; tick < 150; tick++)
                        physics.Update(1f / 30);
                    Assert.That(transforms.GetWorldPose(body).Position.Z, Is.EqualTo(4.25f).Within(0.03));
                    Assert.That(bodyComp.LinearVelocity.Length(), Is.LessThan(0.05));
                    Assert.That(geometry.CaptureSnapshot(frame).SolidParts, Has.Count.EqualTo(1));
                    Assert.That(physics.IsConstraintActive(link), Is.True);
                    Assert.That(transforms.GetWorldPose(actor).Position.Z, Is.EqualTo(4.8f).Within(0.03));
                    Assert.That(motor.Support!.Value.Source.Surface, Is.EqualTo(surface));
                    Assert.That(physics.TryResizeMotor((actor, motor), 0.3f, 0.5f, 0), Is.True);
                    Assert.That(actorBody.DefinitionRevision, Is.EqualTo(1));
                    ISpatialQueryService queries = entities.System<SpatialQuerySystem>();
                    var context = new SpatialQueryContext(world, frame, "integration-placement", uint.MaxValue);
                    Assert.That(queries.FindSurface(context, new Vector3(2, 0, 6), -Vector3.UnitZ, 4)!.Value.Surface,
                        Is.EqualTo(surface));
                    Assert.That(queries.IsVolumeFree(context, new SpatialSphereShape(0.2f),
                        new RigidTransform3(new Vector3(2, 0, 4), Quaternion.Identity)), Is.False);
                    physics.ReconfigureBody((body, bodyComp), bodyComp.Definition, 0,
                        bodyComp.LinearVelocity, bodyComp.AngularVelocity);
                    physics.Update(1f / 30);
                    Assert.That(bodyComp.DefinitionRevision, Is.EqualTo(1));
                    Assert.That(physics.IsConstraintActive(link), Is.True);
                    using var edit = geometry.PrepareTransaction(new[]
                    {
                        new SpatialGeometryEdit(geometry.CaptureSnapshot(frame), slabs: Array.Empty<SlabSurface>())
                    });
                    geometry.QueueTransaction(edit);
                    physics.Update(1f / 30);
                    Assert.That(edit.Status, Is.EqualTo(WorldEditStatus.Committed));
                    Assert.That(component.Revision, Is.EqualTo(1));
                    Assert.That(motor.Support, Is.Null);
                    Assert.That(actorBody.LinearVelocity.Z, Is.LessThan(0));
                    Assert.That(physics.IsConstraintActive(link), Is.False);
                    Assert.That(queries.FindSurface(context, new Vector3(2, 0, 6), -Vector3.UnitZ, 4), Is.Null);
                    entities.DeleteEntity(world);
                    Assert.That(entities.EntityExists(frame), Is.False);
                    Assert.That(entities.EntityExists(body), Is.False);
                    Assert.That(entities.EntityExists(actor), Is.False);
                    Assert.That(physics.IsConstraintActive(link), Is.False);
                }
                finally
                {
                    if (entities.EntityExists(world))
                        entities.DeleteEntity(world);
                }
            }
        });
    }
}
