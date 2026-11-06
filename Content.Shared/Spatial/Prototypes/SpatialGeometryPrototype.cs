using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Spatial;

namespace Content.Shared.Spatial.Prototypes;

public enum SpatialGeometryKind : byte { Boundary, Slab, LegacyBlock }

/// <summary>
/// Content-authored dimensions/materials translated directly into engine semantic
/// geometry. Boundary placement supplies the canonical key; legacy blocks keep
/// their original cell footprint and are never moved onto an edge.
/// This definition does not perform transactions or decide door state.
/// </summary>
[Prototype]
public sealed partial class SpatialGeometryPrototype : IPrototype, ISerializationHooks
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public SpatialGeometryKind Kind;
    [DataField(required: true)] public string Material = default!;
    [DataField] public ProtoId<SpatialBarrierPrototype>? Barrier;
    [DataField] public float Elevation;
    [DataField] public float Height;
    [DataField] public float Thickness;
    [DataField] public Box2 Footprint;
    [DataField] public BoundaryOpening[] BoundaryOpenings = Array.Empty<BoundaryOpening>();
    [DataField] public Box2[] SlabOpenings = Array.Empty<Box2>();

    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || string.IsNullOrWhiteSpace(Material) || !float.IsFinite(Elevation)
            || !float.IsFinite(Height) || !float.IsFinite(Thickness) || BoundaryOpenings == null || SlabOpenings == null)
            throw new ArgumentException("Invalid spatial geometry profile.");
        if (Kind == SpatialGeometryKind.Boundary)
        {
            if (Height <= 0 || Thickness <= 0 || !float.IsFinite(Elevation + Height) || SlabOpenings.Length != 0)
                throw new ArgumentException("A boundary needs positive authored height/thickness and boundary openings.");
            foreach (var opening in BoundaryOpenings)
            {
                opening.Validate();
                if (opening.Top > Height) throw new ArgumentException("Boundary opening exceeds authored height.");
            }
        }
        else
        {
            ValidateRectangle(Footprint);
            if (BoundaryOpenings.Length != 0) throw new ArgumentException("Slabs and legacy blocks use footprint openings.");
            var thickness = Kind == SpatialGeometryKind.Slab ? Thickness : Height;
            var top = Kind == SpatialGeometryKind.Slab ? Elevation : Elevation + Height;
            if (thickness <= 0 || !float.IsFinite(top) || !float.IsFinite(top - thickness))
                throw new ArgumentException("Slabs and legacy blocks need finite positive thickness/height.");
            foreach (var opening in SlabOpenings)
            {
                ValidateRectangle(opening);
                if (opening.Left < Footprint.Left || opening.Right > Footprint.Right
                    || opening.Bottom < Footprint.Bottom || opening.Top > Footprint.Top)
                    throw new ArgumentException("A slab opening must be contained in its footprint.");
            }
        }
    }

    public BoundarySegment CreateBoundary(BoundaryKey key, SurfaceId surface, float authoringGrid,
        EntityUid? entity = null, ulong revision = 0)
    {
        Validate();
        if (Kind != SpatialGeometryKind.Boundary) throw new InvalidOperationException("Profile is not boundary geometry.");
        if (!float.IsFinite(authoringGrid) || authoringGrid <= 0) throw new ArgumentOutOfRangeException(nameof(authoringGrid));
        foreach (var opening in BoundaryOpenings)
            if (opening.End > authoringGrid) throw new ArgumentException("Boundary opening exceeds the selected frame's authoring grid.");
        return new BoundarySegment(key, surface, Thickness, Elevation, Height, BoundaryOpenings, Material, entity, revision);
    }

    public SlabSurface CreateSlab(SpatialSurfaceReference surface, LayerId layer, EntityUid? entity = null, ulong revision = 0)
    {
        Validate();
        if (Kind == SpatialGeometryKind.Boundary) throw new InvalidOperationException("Profile is not slab or legacy block geometry.");
        return new SlabSurface(surface, layer, Footprint,
            Kind == SpatialGeometryKind.Slab ? Elevation : Elevation + Height,
            Kind == SpatialGeometryKind.Slab ? Thickness : Height, SlabOpenings, Material, entity, revision);
    }

    void ISerializationHooks.AfterDeserialization() => Validate();

    private static void ValidateRectangle(Box2 box)
    {
        if (!float.IsFinite(box.Left) || !float.IsFinite(box.Right) || !float.IsFinite(box.Bottom) || !float.IsFinite(box.Top)
            || box.Right <= box.Left || box.Top <= box.Bottom || !float.IsFinite(box.Width) || !float.IsFinite(box.Height))
            throw new ArgumentException("An authored footprint requires finite ordered bounds.");
    }
}
