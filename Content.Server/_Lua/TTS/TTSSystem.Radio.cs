using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.CCVar;
using Content.Shared._Lua.TTS;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Lua.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem
{
    private void InitializeRadio()
    {
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioSpoke);
        SubscribeLocalEvent<AnnounceSpokeEvent>(OnAnnounceSpoke);
        SubscribeLocalEvent<TTSComponent, EntitySpokeToEntityEvent>(OnEntitySpokeToEntity);
    }

    private async void OnRadioSpoke(RadioSpokeEvent args)
    {
        if (!_isEnabled || args.Message.Length > MaxMessageChars)
            return;

        if (!TryComp(args.Source, out TTSComponent? component) || !component.Enabled)
            return;

        var voiceId = component.VoicePrototypeId;
        if (voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(args.Source, voiceId);
        RaiseLocalEvent(args.Source, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        var lang = _language.GetLanguage(args.Source);
        var obf = _language.ObfuscateSpeech(args.Message, lang);

        foreach (var device in args.Receivers)
        {
            var recipients = Filter.Pvs(device).Recipients;
            if (!recipients.Any())
                continue;

            var org = new List<ICommonSession>();
            var obs = new List<ICommonSession>();

            foreach (var session in recipients)
            {
                if (session.AttachedEntity is not { Valid: true } listener)
                    continue;
                if (_language.CanUnderstand(listener, lang.ID))
                    org.Add(session);
                else
                    obs.Add(session);
            }

            if (org.Count > 0)
            {
                var soundData = await GenerateTTS(args.Message, protoVoice.Speaker);
                if (soundData != null)
                    RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(device), isRadio: true), Filter.Empty().AddPlayers(org));
            }

            if (obs.Count > 0)
            {
                var obsData = await GenerateTTS(obf, protoVoice.Speaker);
                if (obsData != null)
                    RaiseNetworkEvent(new PlayTTSEvent(obsData, GetNetEntity(device), isRadio: true), Filter.Empty().AddPlayers(obs));
            }
        }
    }

    private async void OnAnnounceSpoke(AnnounceSpokeEvent args)
    {
        var voiceId = args.Voice;
        if (!_isEnabled ||
            args.Message.Length > _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength) ||
            voiceId == null)
            return;

        if (args.Source != null)
        {
            var voiceEv = new TransformSpeakerVoiceEvent(args.Source.Value, voiceId);
            RaiseLocalEvent(args.Source.Value, voiceEv);
            voiceId = voiceEv.VoiceId;
        }

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        // Delay to sync with announcement sound (roughly 6 seconds)
        Timer.Spawn(6000, async () =>
        {
            var soundData = await GenerateTTS(args.Message, protoVoice.Speaker);
            if (soundData == null)
                return;
            RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.Broadcast());
        });
    }

    private async void OnEntitySpokeToEntity(EntityUid uid, TTSComponent component, EntitySpokeToEntityEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled || !component.Enabled ||
            args.Message.Length > MaxMessageChars ||
            voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        var soundData = await GenerateTTS(args.Message, protoVoice.Speaker);
        if (soundData == null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(args.Target)), args.Target);
    }
}
