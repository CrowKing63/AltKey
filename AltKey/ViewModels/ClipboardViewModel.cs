using System.Collections.ObjectModel;
using AltKey.Models;
using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp = System.Windows.Application;

namespace AltKey.ViewModels;

/// <summary>
/// [역할] 클립보드 항목 하나를 표현하는 데이터 클래스입니다.
/// [속성] FullText: 전체 텍스트, Preview: 미리보기(최대 2줄·120자), CopiedAt: 마지막 복사 시각(구형 데이터면 null),
///        IsFavorite: 고정 여부, TimeText: 상대 시간 표시 문자열
/// </summary>
public partial class ClipboardItem : ObservableObject
{
    public string FullText { get; init; } = "";
    public string Preview { get; init; } = "";
    public DateTime? CopiedAt { get; init; }

    [ObservableProperty] private bool isFavorite;
    // 상대 시간("N분 전") — VM이 갱신할 때만 바뀝니다 (30초 주기 + 목록 갱신 시).
    [ObservableProperty] private string timeText = "";
}

public partial class ClipboardViewModel : ObservableObject
{
    // ── 표시 목록 ────────────────────────────────────────────────────────
    // 고정 섹션: 즐겨찾기. 최근 리스트: 히스토리에서 고정 항목을 뺀 것 — 같은 텍스트가 두 섹션에 중복 표시되지 않습니다.
    public ObservableCollection<ClipboardItem> Favorites { get; } = [];
    public ObservableCollection<ClipboardItem> HistoryItems { get; } = [];

    [ObservableProperty] private bool _showFavoritesSection; // 고정 섹션 표시 여부 (비어 있으면 숨김)
    [ObservableProperty] private bool _hasAnyItems;          // 빈 상태 안내 표시 여부 (두 목록 모두 비어 있으면 false)
    [ObservableProperty] private string _emptyStateMessage = "아직 복사한 항목이 없습니다";
    // 정렬 순서: true = 내림차순(최신순, 기본값), false = 오름차순(오래된 순). 최근 리스트에만 적용됩니다.
    [ObservableProperty] private bool _isDescending = true;

    private readonly ClipboardService _clipboardService;
    private readonly InputService _inputService;
    private readonly ConfigService _configService;
    private readonly SoundService _soundService;

    // [스크래치패드] Ctrl+V 전송 전 입력 대상(패드의 메모 박스)을 확보하는 훅 — MainViewModel이 연결한다.
    // 패드가 열려 있으면 포그라운드가 자기(키보드) 창으로 쏠려 있을 수 있어, 그대로 Ctrl+V를 보내면
    // AltKey 자체로 가 사라진다(제안 수락 경로와 동일한 증세). 외부 앱이 대상이면 훅은 아무 일도 안 한다.
    // true 반환 = 프로세스 내 대상(스크래치패드)을 확보했으니 호출 쪽은 외부 복원을 스킵한다.
    public Func<bool>? PrepareInputTarget { get; set; }

    // [붙여넣기 대상 복원] 패널이 포커스를 갖고 있을 때 클릭으로 보내는 Ctrl+V가 AltKey 자체로 가지 않도록,
    // 전송 전 포그라운드를 열기 직전의 외부 앱으로 되돌리는 훅 — ClipboardPanelWindow가 연결한다.
    // (스크래치패드가 대상이면 PrepareInputTarget이 true를 돌려 주므로 아래에서 자동 스킵된다.)
    public Func<bool>? RestoreInputTarget { get; set; }

    // "패널 닫기" 요청 훅 — ✕·Esc 버튼과 '붙여넣기 후 닫기' 설정이 여기로 수렴합니다 (창/서비스가 연결).
    internal Action RequestClose { get; set; } = () => { };

    public ClipboardViewModel(ClipboardService clipboardService, InputService inputService, ConfigService configService, SoundService soundService)
    {
        _clipboardService = clipboardService;
        _inputService = inputService;
        _configService = configService;
        _soundService = soundService;
        _clipboardService.HistoryChanged += RefreshItems;
        _clipboardService.FavoritesChanged += RefreshItems;

        // 서비스 생성 시점에 Load()가 이미 파일에서 히스토리를 채웠으므로, 초기 목록을 한 번만 표시합니다.
        WpfApp.Current.Dispatcher.Invoke(() => RefreshItems());
    }

    // ── 목록 갱신 ─────────────────────────────────────────────────────

    /// <summary>두 목록을 다시 구성합니다 (최근 리스트엔 정렬 순서 적용).</summary>
    private void RefreshItems()
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            Favorites.Clear();
            HistoryItems.Clear();

            foreach (var text in _clipboardService.Favorites)
                Favorites.Add(CreateItem(text, isFavorite: true));

            // 고정 항목은 '고정' 섹션에서 이미 표시되므로 최근 리스트에선 뺍니다 (중복 방지).
            var history = _clipboardService.History.Where(t => !_clipboardService.IsFavorite(t));
            // 정렬: 내림차순(최신순) = 서비스 기본 순서, 오름차순(오래된 순) = 역순.
            if (!IsDescending)
                history = history.Reverse();

            foreach (var text in history)
                HistoryItems.Add(CreateItem(text, isFavorite: false));

            ShowFavoritesSection = Favorites.Count > 0;
            HasAnyItems = Favorites.Count + HistoryItems.Count > 0;
        });
    }

    private ClipboardItem CreateItem(string text, bool isFavorite) => new()
    {
        FullText = text,
        Preview = Preview(text),
        CopiedAt = _clipboardService.CopiedAt(text),
        IsFavorite = isFavorite,
        TimeText = FormatCopiedAt(_clipboardService.CopiedAt(text))
    };

    // ── 상대 시간 표시 ────────────────────────────────────────────────

    /// <summary>상대 시간을 사람이 읽기 좋게 포맷합니다 — "방금"(&lt;1분) / "N분 전" / "N시간 전" / "M/d". 기록 없으면 빈 문자열.</summary>
    public static string FormatCopiedAt(DateTime? copiedAt)
    {
        if (copiedAt is not { } t) return "";
        var diff = DateTime.Now - t;
        if (diff.TotalMinutes < 1) return "방금";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}분 전";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}시간 전";
        return t.ToString("M/d");
    }

    /// <summary>열려 있는 동안 상대 시간이 흐르도록, 30초마다 시간 라벨만 갱신합니다 (목록 재구성은 안 함 — 선택·스크롤 유지).</summary>
    internal void RefreshTimes()
    {
        foreach (var it in HistoryItems)
            it.TimeText = FormatCopiedAt(it.CopiedAt);
    }

    // ── 명령 ──────────────────────────────────────────────────────────────

    /// <summary>항목을 클립보드에 설정하고 붙여넣기 (Ctrl+V)합니다. "붙여넣기 후 닫기" 설정이 ON이면 패널을 닫습니다.</summary>
    [RelayCommand]
    private void PasteItem(string text)
    {
        _clipboardService.PasteItem(text);
        // [스크래치패드] 전송 전 입력 대상(패드의 메모 박스)을 확보한다.
        bool ownTarget = PrepareInputTarget?.Invoke() ?? false;

        // [붙여넣기 대상 복원] 패널이 포커스를 갖고 있으면 외부 앱으로 되돌린다 (클릭 붙여넣기).
        // 단, 방금 스크래치패드를 확보한 경우(ownTarget)는 복원이 오히려 패드에서 포커스를 빼앗아
        // Ctrl+V가 엉뚱한 외부 창으로 가므로 스킵한다.
        if (!ownTarget && RestoreInputTarget is not null && !RestoreInputTarget())
            return; // 엉뚱한 창/컨트롤에 Ctrl+V를 보내지 않는다.
        // Ctrl+V 전송으로 붙여넣기
        _inputService.HandleAction(new SendComboAction(["VK_CONTROL", "VK_V"]));

        // [사운드] 붙여넣기가 실제로 전송됐을 때만 재생 — 위 복원 실패로 반환한 경우엔 소리가 안 나도록 한다.
        // 다른 UI 동작(버튼 클릭·제안 수락)과 같은 효과음을 사용한다 (설정에서 켜기/파일/볼륨 변경 가능).
        _soundService.Play();

        if (_configService.Current.ClipboardCloseAfterPaste)
            RequestClose();
    }

    /// <summary>고정(즐겨찾기)을 토글합니다 — ★. 고정하면 핀 칩 스트립으로, 해제하면 원래 위치로 최근 리스트에 복귀.</summary>
    [RelayCommand]
    private void ToggleFavorite(string text) => _clipboardService.ToggleFavorite(text);

    /// <summary>핀에서 제거합니다 (칩 ✕ = 핀 해제). 항목은 히스토리에서 그대로 유지됩니다.</summary>
    [RelayCommand]
    private void RemoveFavorite(string text) => _clipboardService.RemoveFavorite(text);

    /// <summary>히스토리에서 항목 하나를 삭제합니다 (행 ✕). 즐겨찾기(★)는 보존 — 완전히 없애려면 ★부터 해제하세요.</summary>
    [RelayCommand]
    private void DeleteItem(string text) => _clipboardService.RemoveFromHistory(text);

    /// <summary>히스토리 전체를 삭제합니다 (헤더 "전체 삭제"). 즐겨찾기는 보존 — Win+V 시맨틱.</summary>
    [RelayCommand]
    private void ClearHistory() => _clipboardService.ClearHistory();

    /// <summary>최근 리스트 정렬 순서를 전환합니다 (내림차순 최신순 ↔ 오름차순 오래된 순).</summary>
    [RelayCommand]
    private void ToggleSortOrder()
    {
        IsDescending = !IsDescending;
        RefreshItems();
    }

    // ── 유틸리티 ──────────────────────────────────────────────────────────

    private static string Preview(string text)
    {
        // 줄바꿈 제거 + 120자 제한 — UI는 최대 2줄로 줄바꿈해 표시(행 MaxHeight 35)하므로
        // 여기서는 표시를 위한 여유분만 남긴다.
        var singleLine = text.Replace("\n", " ").Replace("\r", " ").TrimStart();
        return singleLine.Length <= 120 ? singleLine : singleLine[..120] + "...";
    }
}
