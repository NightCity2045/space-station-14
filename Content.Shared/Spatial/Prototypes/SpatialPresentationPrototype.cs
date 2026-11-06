using System;
using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Spatial.Prototypes;

/// <summary>Content presentation categories from the spatial asset contract.</summary>
public enum SpatialPresentationKind : byte
{
    DirectionalBillboard,
    GroundSprite,
    SurfaceQuad,
    Box,
    PrimitiveAssembly,
    BoundarySurface,
    TileSurface,
    MovingStructureGeometry,
}

/// <summary>
/// Authored physical presentation metadata. Existing Sprite layers remain the
/// image source. Neither a camera-facing billboard nor its pivot defines a body.
/// Geometry categories obtain their shape from the canonical engine geometry.
/// </summary>
[Prototype]
public sealed partial class SpatialPresentationPrototype : IPrototype, ISerializationHooks
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public SpatialPresentationKind Kind;
    [DataField(required: true)] public Vector3 Size;
    [DataField(required: true)] public Vector2 Pivot;
    [DataField] public float MaximumTilt;
    [DataField] public string[] SupportedStates = Array.Empty<string>();
    [DataField] public bool DecorativeFallback;
    [DataField] public string Notes = "";

    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || !Finite(Size) || Size.X <= 0 || Size.Y <= 0 || Size.Z <= 0
            || !float.IsFinite(Pivot.X) || !float.IsFinite(Pivot.Y)
            || !float.IsFinite(MaximumTilt) || MaximumTilt < 0 || MaximumTilt > MathF.PI / 2
            || SupportedStates == null || Notes == null)
            throw new ArgumentException("Spatial presentation requires finite positive authored dimensions, pivot and tilt.");
        if (Kind != SpatialPresentationKind.DirectionalBillboard && MaximumTilt != 0)
            throw new ArgumentException("Only a directional billboard can use observer-dependent tilt.");
        var states = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in SupportedStates)
            if (string.IsNullOrWhiteSpace(state) || !states.Add(state))
                throw new ArgumentException("Supported presentation states must be distinct nonempty names.");
        if (DecorativeFallback && Kind is not (SpatialPresentationKind.GroundSprite or SpatialPresentationKind.SurfaceQuad))
            throw new ArgumentException("A generic decorative fallback cannot stand in for a body or structure.");
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    void ISerializationHooks.AfterDeserialization() => Validate();
}
