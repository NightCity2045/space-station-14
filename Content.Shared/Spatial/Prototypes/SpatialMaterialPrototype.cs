using System;
using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Spatial.Prototypes;

public enum SpatialMaterialAlpha : byte { Opaque, Cutout, Transparent }
public enum SpatialMaterialSampling : byte { Nearest, PixelMip }

/// <summary>
/// Content material binding for the engine's named semantic surfaces. Optional
/// authored PNGs use the existing resource tree; animated RSI appearances are
/// resolved from actual Sprite layers separately. Colors/emission are linear RGB.
/// This does not determine damage resistance, support or barrier permeability.
/// </summary>
[Prototype]
public sealed partial class SpatialMaterialPrototype : IPrototype, ISerializationHooks
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField] public ResPath? Texture;
    [DataField] public Vector4 Color = Vector4.One;
    [DataField] public Vector3 Emission;
    [DataField] public Vector2 Repeat = Vector2.One;
    [DataField] public SpatialMaterialAlpha Alpha;
    [DataField] public float AlphaCutoff = 0.5f;
    [DataField] public bool DoubleSided;
    [DataField] public SpatialMaterialSampling Sampling;

    public void Validate()
    {
        if (!Enum.IsDefined(Alpha) || !Enum.IsDefined(Sampling)
            || !float.IsFinite(Color.X) || !float.IsFinite(Color.Y) || !float.IsFinite(Color.Z) || !float.IsFinite(Color.W)
            || Color.X < 0 || Color.Y < 0 || Color.Z < 0 || Color.W < 0 || Color.W > 1
            || !float.IsFinite(Emission.X) || !float.IsFinite(Emission.Y) || !float.IsFinite(Emission.Z)
            || Emission.X < 0 || Emission.Y < 0 || Emission.Z < 0
            || !float.IsFinite(Repeat.X) || !float.IsFinite(Repeat.Y) || Repeat.X <= 0 || Repeat.Y <= 0
            || !float.IsFinite(AlphaCutoff) || AlphaCutoff < 0 || AlphaCutoff > 1)
            throw new ArgumentException("Invalid spatial material color, emission, sampling or alpha policy.");
        if (Texture is { } texture && (string.IsNullOrWhiteSpace(texture.ToString())
            || !texture.ToString().EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A static material texture must name an existing PNG resource.");
        if (Sampling == SpatialMaterialSampling.PixelMip && Texture == null)
            throw new ArgumentException("Isolated mip sampling requires a source texture.");
    }
    void ISerializationHooks.AfterDeserialization() => Validate();
}
