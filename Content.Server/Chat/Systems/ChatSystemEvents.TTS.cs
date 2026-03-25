namespace Content.Server.Chat.Systems;

// Corvax-TTS-Start

/// <summary>
///     Raised on an entity after <see cref="EntitySpokeEvent"/> when it speaks using radio.
/// </summary>
public sealed class RadioSpokeEvent : EntityEventArgs
{
    public readonly EntityUid Source;
    public readonly string Message;
    /// <summary>
    ///     The list of radio speaker entities that received this message.
    /// </summary>
    public readonly EntityUid[] Receivers;

    public RadioSpokeEvent(EntityUid source, string message, EntityUid[] receivers)
    {
        Source = source;
        Message = message;
        Receivers = receivers;
    }
}

/// <summary>
///     Raised when a station announcement is made and TTS should be played.
/// </summary>
public sealed class AnnounceSpokeEvent : EntityEventArgs
{
    public readonly string Voice;
    public readonly string Message;
    public readonly EntityUid? Source;

    public AnnounceSpokeEvent(string voice, string message, EntityUid? source)
    {
        Voice = voice;
        Message = message;
        Source = source;
    }
}

/// <summary>
///     Raised on an entity when it sends a direct message to another entity.
/// </summary>
public sealed class EntitySpokeToEntityEvent : EntityEventArgs
{
    public readonly EntityUid Target;
    public readonly string Message;

    public EntitySpokeToEntityEvent(EntityUid target, string message)
    {
        Target = target;
        Message = message;
    }
}

// Corvax-TTS-End
