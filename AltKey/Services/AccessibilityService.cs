using System.Speech.Synthesis;
using AltKey.Models;

namespace AltKey.Services;

/// <summary>
/// [역할] 접근성을 위한 TTS(텍스트 음성 변환) 기능을 제공하는 서비스입니다.
/// [기능] 키 라벨을 음성으로 읽어주며, 중복 발화 방지와 음성 속도 조절을 지원합니다.
/// </summary>
public sealed class AccessibilityService : IDisposable
{
    private readonly SpeechSynthesizer _synth;
    private readonly ConfigService _configService;
    private string _lastSpokenLabel = "";
    private DateTime _lastSpokenTime = DateTime.MinValue;

    public AccessibilityService(ConfigService configService)
    {
        _configService = configService;
        _synth = new SpeechSynthesizer();
        _synth.SetOutputToDefaultAudioDevice();

        // 한국어 음성을 우선적으로 선택합니다. 없으면 기본 음성을 사용합니다.
        try
        {
            var koVoice = _synth.GetInstalledVoices()
                .FirstOrDefault(v => v.VoiceInfo.Culture.Name.StartsWith("ko", StringComparison.OrdinalIgnoreCase));
            if (koVoice != null)
                _synth.SelectVoice(koVoice.VoiceInfo.Name);
        }
        catch
        {
            // 음성 선택 실패는 무시 — 기본 음성으로 폴백
        }
    }

    /// <summary>
    /// 주어진 라벨을 음성으로 읽어줍니다. 설정이 꺼져 있거나 중복이면 스킵합니다.
    /// </summary>
    public void SpeakLabel(string? label)
    {
        if (!_configService.Current.TtsEnabled)
            return;

        if (string.IsNullOrWhiteSpace(label))
            return;

        // 중복 발화 방지: 동일 라벨은 500ms 내에 반복하지 않습니다.
        if (label == _lastSpokenLabel && (DateTime.UtcNow - _lastSpokenTime).TotalMilliseconds < 500)
            return;

        _lastSpokenLabel = label;
        _lastSpokenTime = DateTime.UtcNow;

        try
        {
            int rate = Math.Clamp(_configService.Current.TtsRate, -5, 5);
            _synth.Rate = rate;
            // 이전 음성을 취소하여 너무 잦은 발화를 방지합니다.
            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(label);
        }
        catch
        {
            // TTS 실패는 무시 — 입력 안정성을 해치지 않습니다.
        }
    }

    public void Dispose()
    {
        try { _synth.Dispose(); } catch { /* 정리 실패 무시 */ }
    }
}
