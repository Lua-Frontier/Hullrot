using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Lua.TTS;

/// <summary>
/// Apply TTS for entity chat say messages
/// </summary>
[RegisterComponent, NetworkedComponent]
// ReSharper disable once InconsistentNaming
public sealed partial class TTSComponent : Component
{
    /// <summary>
    /// Prototype of used voice for TTS.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("voice", customTypeSerializer: typeof(PrototypeIdSerializer<TTSVoicePrototype>))]
    public string? VoicePrototypeId { get; set; }

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("enabled")]
    public bool Enabled { get; set; } = true;
}
