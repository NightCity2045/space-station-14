using System;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Spatial.Prototypes;

/// <summary>
/// Independent gameplay permeability channels. Alpha and shader selection never
/// determine airtightness, movement, projectiles or acoustic propagation.
/// Door/window adapters select this from their existing authoritative state.
/// </summary>
[Prototype]
public sealed partial class SpatialBarrierPrototype : IPrototype, ISerializationHooks
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public bool Movement;
    [DataField(required: true)] public bool View;
    [DataField(required: true)] public bool Projectiles;
    [DataField(required: true)] public float GasConductance;
    [DataField(required: true)] public float SoundTransmission;

    public void Validate()
    {
        if (!float.IsFinite(GasConductance) || GasConductance < 0 || GasConductance > 1
            || !float.IsFinite(SoundTransmission) || SoundTransmission < 0 || SoundTransmission > 1)
            throw new ArgumentException("Normalized gas and sound permeability must be finite values in [0,1].");
    }
    void ISerializationHooks.AfterDeserialization() => Validate();
}
