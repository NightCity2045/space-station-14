using System;
using Content.Shared.Spatial.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.Graphics.Spatial;

namespace Content.Client.Spatial;

/// <summary>Pure binding of validated authored material policy to engine draw data.</summary>
public static class SpatialMaterialBinding
{
    /// <param name="texture">
    /// The content resource owner's fully loaded texture. PixelMip requires the
    /// completed isolated upload from the engine, never a pending texture or RSI atlas.
    /// </param>
    public static SpatialRenderMaterial Resolve(SpatialMaterialPrototype profile, Texture? texture)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        if ((profile.Texture != null) != (texture != null))
            throw new ArgumentException("The authored image binding must be fully resolved before rendering.");
        return new SpatialRenderMaterial(texture, profile.Color, profile.Emission, profile.Repeat,
            profile.Alpha switch
            {
                SpatialMaterialAlpha.Opaque => SpatialAlphaMode.Opaque,
                SpatialMaterialAlpha.Cutout => SpatialAlphaMode.Cutout,
                SpatialMaterialAlpha.Transparent => SpatialAlphaMode.Transparent,
                _ => throw new ArgumentOutOfRangeException(nameof(profile)),
            }, profile.AlphaCutoff, profile.DoubleSided,
            profile.Sampling == SpatialMaterialSampling.PixelMip ? SpatialTextureSampling.PixelMip : SpatialTextureSampling.Nearest);
    }
}
