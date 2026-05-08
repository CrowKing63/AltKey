using System.Threading;
namespace AltKey.Services;

/// <summary>
/// [역할] AltKey.Tools와 메인 AltKey 사이의 "재로드 알림 전용" 최소 IPC를 담당합니다.
/// [접근성] 편집기 저장 직후 메인 앱이 최신 설정/사전 데이터를 다시 읽어 사용자가 재실행 없이 결과를 쓰도록 돕습니다.
/// [한계] 1단계 원칙에 맞춰 데이터 본문은 보내지 않고, 이벤트 이름만 전달합니다.
/// </summary>
public sealed class ToolsReloadSignalService : IDisposable
{
    // 이벤트 이름은 도구 앱과 메인 앱이 같은 문자열을 공유해야 하므로 상수로 고정합니다.
    private const string ReloadLayoutsEventName = "AltKey.Tools.ReloadLayouts";
    private const string ReloadUserDictionaryEventName = "AltKey.Tools.ReloadUserDictionary";
    private const string ReloadBigramDataEventName = "AltKey.Tools.ReloadBigramData";
    private const string ReloadProfilesEventName = "AltKey.Tools.ReloadProfiles";
    private const string ReloadAiSettingsEventName = "AltKey.Tools.ReloadAiSettings";
    private const string ReloadHeaderButtonsEventName = "AltKey.Tools.ReloadHeaderButtons";

    private readonly LayoutService _layoutService;
    private readonly ConfigService _configService;
    private readonly KoreanDictionary _koreanDictionary;
    private readonly EnglishDictionary _englishDictionary;

    private readonly EventWaitHandle _reloadLayoutsEvent;
    private readonly EventWaitHandle _reloadUserDictionaryEvent;
    private readonly EventWaitHandle _reloadBigramDataEvent;
    private readonly EventWaitHandle _reloadProfilesEvent;
    private readonly EventWaitHandle _reloadAiSettingsEvent;
    private readonly EventWaitHandle _reloadHeaderButtonsEvent;

    private readonly RegisteredWaitHandle _reloadLayoutsWaitHandle;
    private readonly RegisteredWaitHandle _reloadUserDictionaryWaitHandle;
    private readonly RegisteredWaitHandle _reloadBigramDataWaitHandle;
    private readonly RegisteredWaitHandle _reloadProfilesWaitHandle;
    private readonly RegisteredWaitHandle _reloadAiSettingsWaitHandle;
    private readonly RegisteredWaitHandle _reloadHeaderButtonsWaitHandle;

    public ToolsReloadSignalService(
        ConfigService configService,
        LayoutService layoutService,
        KoreanDictionary koreanDictionary,
        EnglishDictionary englishDictionary)
    {
        _configService = configService;
        _layoutService = layoutService;
        _koreanDictionary = koreanDictionary;
        _englishDictionary = englishDictionary;

        _reloadLayoutsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ReloadLayoutsEventName);
        _reloadUserDictionaryEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ReloadUserDictionaryEventName);
        _reloadBigramDataEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ReloadBigramDataEventName);
        _reloadProfilesEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ReloadProfilesEventName);
        _reloadAiSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ReloadAiSettingsEventName);
        _reloadHeaderButtonsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ReloadHeaderButtonsEventName);

        _reloadLayoutsWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            _reloadLayoutsEvent,
            (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => _layoutService.NotifyExternalLayoutsChanged()),
            null,
            Timeout.Infinite,
            false);

        _reloadUserDictionaryWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            _reloadUserDictionaryEvent,
            (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _koreanDictionary.ReloadUserWords();
                _englishDictionary.ReloadUserWords();
            }),
            null,
            Timeout.Infinite,
            false);

        _reloadBigramDataWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            _reloadBigramDataEvent,
            (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _koreanDictionary.ReloadBigrams();
                _englishDictionary.ReloadBigrams();
            }),
            null,
            Timeout.Infinite,
            false);

        _reloadProfilesWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            _reloadProfilesEvent,
            (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => _configService.ReloadFromDiskAndNotify(nameof(Models.AppConfig.Profiles))),
            null,
            Timeout.Infinite,
            false);

        _reloadAiSettingsWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            _reloadAiSettingsEvent,
            // AI 기본 프롬프트는 설정 창의 해당 필드만 다시 읽어도 충분하므로 속성 이름을 함께 알립니다.
            (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => _configService.ReloadFromDiskAndNotify(nameof(Models.AppConfig.AiDefaultPrompt))),
            null,
            Timeout.Infinite,
            false);

        _reloadHeaderButtonsWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            _reloadHeaderButtonsEvent,
            (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => _configService.ReloadFromDiskAndNotify(nameof(Models.AppConfig.HeaderButtons))),
            null,
            Timeout.Infinite,
            false);
    }

    /// <summary>도구 앱에서 레이아웃 저장 후 메인 앱이 목록과 UI를 다시 계산하게 합니다.</summary>
    public static void NotifyReloadLayouts() => Signal(ReloadLayoutsEventName);

    /// <summary>도구 앱에서 사용자 단어 저장 후 자동완성 사전을 다시 읽게 합니다.</summary>
    public static void NotifyReloadUserDictionary() => Signal(ReloadUserDictionaryEventName);

    /// <summary>도구 앱에서 bigram 저장 후 추천 통계를 다시 읽게 합니다.</summary>
    public static void NotifyReloadBigramData() => Signal(ReloadBigramDataEventName);

    /// <summary>프로필 매핑처럼 설정 파일 기반 데이터가 바뀌었을 때 메인 앱 설정을 다시 읽게 합니다.</summary>
    public static void NotifyReloadProfiles() => Signal(ReloadProfilesEventName);

    /// <summary>AI 프롬프트처럼 한글 입력이 필요한 설정을 외부 편집기에서 저장한 뒤 메인 앱 설정을 다시 읽게 합니다.</summary>
    public static void NotifyReloadAiSettings() => Signal(ReloadAiSettingsEventName);

    /// <summary>상단바 버튼 설정을 외부 편집기에서 저장한 뒤 메인 앱과 설정 창이 즉시 다시 읽게 합니다.</summary>
    public static void NotifyReloadHeaderButtons() => Signal(ReloadHeaderButtonsEventName);

    private static void Signal(string eventName)
    {
        try
        {
            using var ev = EventWaitHandle.OpenExisting(eventName);
            ev.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // 메인 앱이 실행 중이 아니면 재로드할 대상이 없으므로 조용히 무시합니다.
        }
    }

    public void Dispose()
    {
        _reloadLayoutsWaitHandle.Unregister(_reloadLayoutsEvent);
        _reloadUserDictionaryWaitHandle.Unregister(_reloadUserDictionaryEvent);
        _reloadBigramDataWaitHandle.Unregister(_reloadBigramDataEvent);
        _reloadProfilesWaitHandle.Unregister(_reloadProfilesEvent);
        _reloadAiSettingsWaitHandle.Unregister(_reloadAiSettingsEvent);
        _reloadHeaderButtonsWaitHandle.Unregister(_reloadHeaderButtonsEvent);

        _reloadLayoutsEvent.Dispose();
        _reloadUserDictionaryEvent.Dispose();
        _reloadBigramDataEvent.Dispose();
        _reloadProfilesEvent.Dispose();
        _reloadAiSettingsEvent.Dispose();
        _reloadHeaderButtonsEvent.Dispose();
    }
}
