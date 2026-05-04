using System.Threading;
namespace AltKey.Services;

/// <summary>
/// [역할] AltKey.Tools와 메인 AltKey 사이의 "재로드 알림 전용" 최소 IPC를 담당합니다.
/// [접근성] 편집기 저장/삭제 후 메인 앱이 즉시 데이터를 다시 읽어, 사용자가 재실행 없이 최신 입력 보조 결과를 얻도록 돕습니다.
/// [설계] 1단계 요구사항에 맞춰 데이터 본문은 보내지 않고, 명령 이름만 네임드 이벤트로 전달합니다.
/// </summary>
public sealed class ToolsReloadSignalService : IDisposable
{
    // 이벤트 이름은 앱 전체에서 고정된 식별자이므로 하드코딩합니다.
    private const string ReloadLayoutsEventName = "AltKey.Tools.ReloadLayouts";
    private const string ReloadUserDictionaryEventName = "AltKey.Tools.ReloadUserDictionary";
    private const string ReloadBigramDataEventName = "AltKey.Tools.ReloadBigramData";

    private readonly LayoutService _layoutService;
    private readonly KoreanDictionary _koreanDictionary;
    private readonly EnglishDictionary _englishDictionary;

    private readonly EventWaitHandle _reloadLayoutsEvent;
    private readonly EventWaitHandle _reloadUserDictionaryEvent;
    private readonly EventWaitHandle _reloadBigramDataEvent;

    private readonly RegisteredWaitHandle _reloadLayoutsWaitHandle;
    private readonly RegisteredWaitHandle _reloadUserDictionaryWaitHandle;
    private readonly RegisteredWaitHandle _reloadBigramDataWaitHandle;

    public ToolsReloadSignalService(
        LayoutService layoutService,
        KoreanDictionary koreanDictionary,
        EnglishDictionary englishDictionary)
    {
        _layoutService = layoutService;
        _koreanDictionary = koreanDictionary;
        _englishDictionary = englishDictionary;

        _reloadLayoutsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ReloadLayoutsEventName);
        _reloadUserDictionaryEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ReloadUserDictionaryEventName);
        _reloadBigramDataEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ReloadBigramDataEventName);

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
    }

    /// <summary>도구 앱에서 레이아웃 저장/삭제 후 호출합니다.</summary>
    public static void NotifyReloadLayouts() => Signal(ReloadLayoutsEventName);

    /// <summary>도구 앱에서 사용자 단어 저장/삭제 후 호출합니다.</summary>
    public static void NotifyReloadUserDictionary() => Signal(ReloadUserDictionaryEventName);

    /// <summary>도구 앱에서 bigram 저장/삭제 후 호출합니다.</summary>
    public static void NotifyReloadBigramData() => Signal(ReloadBigramDataEventName);

    private static void Signal(string eventName)
    {
        try
        {
            using var ev = EventWaitHandle.OpenExisting(eventName);
            ev.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // 메인 앱이 실행 중이 아닐 때는 신호를 보낼 대상이 없으므로 조용히 무시합니다.
        }
    }

    public void Dispose()
    {
        _reloadLayoutsWaitHandle.Unregister(_reloadLayoutsEvent);
        _reloadUserDictionaryWaitHandle.Unregister(_reloadUserDictionaryEvent);
        _reloadBigramDataWaitHandle.Unregister(_reloadBigramDataEvent);

        _reloadLayoutsEvent.Dispose();
        _reloadUserDictionaryEvent.Dispose();
        _reloadBigramDataEvent.Dispose();
    }
}
