using System.Threading;
using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Server.Language;
using Content.Server.Radio.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared._Lua.CCCVars;
using Content.Shared._Lua.TTS;
using Content.Shared.GameTicking;
using Content.Shared.Language;
using Content.Shared.Players.RateLimiting;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lua.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;
    [Dependency] private readonly LanguageSystem _language = default!;

    private readonly List<string> _sampleText =
        new()
        {
            "Съешь же ещё этих мягких французских булок, да выпей чаю.",
            "Клоун, прекрати разбрасывать банановые кожурки офицерам под ноги!",
            "Капитан, вы уверены что хотите назначить клоуна на должность главы персонала?",
            "Эс Бэ! Тут человек в сером костюме, с тулбоксом и в маске! Помогите!!",
            "Я надеюсь что инженеры внимательно следят за сингулярностью...",
            "Вы слышали эти странные крики в техах? Мне кажется туда ходить небезопасно.",
            "Вы не видели Гамлета? Мне кажется он забегал к вам на кухню.",
            "Здесь есть доктор? Человек умирает от отравленного пончика! Нужна помощь!",
            "Возле эвакуационного шаттла разгерметизация! Инженеры, нам срочно нужна ваша помощь!",
            "Бармен, налей мне самого крепкого вина, которое есть в твоих запасах!"
        };

    private const int MaxMessageChars = 100 * 3; // same as SingleBubbleCharLimit * 3
    private bool _isEnabled = false;

    public override void Initialize()
    {
        _cfg.OnValueChanged(CCCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);

        InitializeRadio(); // Lua-TTS
        RegisterRateLimits();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _ttsManager.ResetCache();
    }

    private async void OnRequestPreviewTTS(RequestPreviewTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        if (HandleRateLimit(args.SenderSession) != RateLimitStatus.Allowed)
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, protoVoice.Speaker);
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession));
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
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

        if (args.IsWhisper)
        {
            await HandleWhisper(uid, args.Message, protoVoice.Speaker, args.Language);
            return;
        }

        await HandleSay(uid, args.Message, protoVoice.Speaker, args.Language);
    }

    private async Task HandleSay(EntityUid uid, string message, string speaker, LanguagePrototype language)
    {
        var netEntity = GetNetEntity(uid);
        var obfMessage = _language.ObfuscateSpeech(message, language);

        var recipients = Filter.Pvs(uid).Recipients;
        var org = new List<ICommonSession>();
        var obs = new List<ICommonSession>();

        foreach (var session in recipients)
        {
            if (session.AttachedEntity is not { Valid: true } listener)
                continue;
            if (_language.CanUnderstand(listener, language.ID))
                org.Add(session);
            else
                obs.Add(session);
        }

        if (org.Count > 0)
        {
            var soundData = await GenerateTTS(message, speaker);
            if (soundData != null)
                RaiseNetworkEvent(new PlayTTSEvent(soundData, netEntity), Filter.Empty().AddPlayers(org));
        }

        if (obs.Count > 0)
        {
            var obsData = await GenerateTTS(obfMessage, speaker);
            if (obsData != null)
                RaiseNetworkEvent(new PlayTTSEvent(obsData, netEntity), Filter.Empty().AddPlayers(obs));
        }
    }

    private async Task HandleWhisper(EntityUid uid, string message, string speaker, LanguagePrototype language)
    {
        var netEntity = GetNetEntity(uid);
        var obfMessage = _language.ObfuscateSpeech(message, language);

        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourcePos = _xforms.GetWorldPosition(xformQuery.GetComponent(uid), xformQuery);
        var receptions = Filter.Pvs(uid).Recipients;

        var cache = new Dictionary<string, byte[]?>();
        async Task<byte[]?> GetAudio(string text)
        {
            if (cache.TryGetValue(text, out var d)) return d;
            var data = await GenerateTTS(text, speaker, true);
            cache[text] = data;
            return data;
        }

        foreach (var session in receptions)
        {
            if (!session.AttachedEntity.HasValue) continue;
            var xform = xformQuery.GetComponent(session.AttachedEntity.Value);
            var distance = (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).Length();
            if (distance > ChatSystem.WhisperMuffledRange)
                continue;

            var understands = _language.CanUnderstand(session.AttachedEntity.Value, language.ID);
            string textToSpeak;
            if (distance <= ChatSystem.WhisperClearRange)
                textToSpeak = understands ? message : obfMessage;
            else
                textToSpeak = obfMessage;

            var data = await GetAudio(textToSpeak);
            if (data == null) continue;
            RaiseNetworkEvent(new PlayTTSEvent(data, netEntity, true), session);
        }
    }

    // ReSharper disable once InconsistentNaming
    private readonly Dictionary<string, Task<byte[]?>> _ttsTasks = new();
    private readonly SemaphoreSlim _ttsLock = new(1, 1);
    private async Task<byte[]?> GenerateTTS(string text, string speaker, bool isWhisper = false)
    {
        var textSanitized = Sanitize(text);
        if (textSanitized == "") return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        var ssmlTraits = SoundTraits.RateFast;
        if (isWhisper)
            ssmlTraits = SoundTraits.PitchVerylow;
        var textSsml = ToSsmlText(textSanitized, ssmlTraits);

        var taskKey = $"{textSanitized}_{speaker}_{isWhisper}";
        await _ttsLock.WaitAsync();
        try
        {
            if (_ttsTasks.TryGetValue(taskKey, out var existing)) return await existing;
            var newTask = _ttsManager.ConvertTextToSpeech(speaker, textSsml);
            _ttsTasks[taskKey] = newTask;
        }
        finally
        { _ttsLock.Release(); }
        try
        { return await _ttsTasks[taskKey]; }
        finally
        {
            await _ttsLock.WaitAsync();
            try { _ttsTasks.Remove(taskKey); }
            finally { _ttsLock.Release(); }
        }
    }
}
