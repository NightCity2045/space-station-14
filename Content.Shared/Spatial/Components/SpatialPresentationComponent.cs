using Content.Shared.Spatial.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Spatial.Components;

/// <summary>
/// Explicit authored profile binding, inherited through ordinary entity prototypes.
/// No second pose, sprite layers or physical body is stored here.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpatialPresentationComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<SpatialPresentationPrototype> Profile;
}
