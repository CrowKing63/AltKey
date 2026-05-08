using System.Collections.ObjectModel;
using AltKey.Models;
using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Threading;

namespace AltKey.ViewModels;

// ── 편집 가능한 키 슬롯 VM ──────────────────────────────────────────────────

public partial class EditableKeySlotVm : ObservableObject
{
    public const double DefaultHeightRatio = 1.0;
    public const double CompactHeightRatio = 2.0 / 3.0;

    // 접근성/가시성: 기본 키와 살짝 다른 톤을 줄 때 사용하는 내부 style_key 값입니다.
    public const string SoftAccentStyleKey = "soft_accent";

    // 문자 키는 옵션을 숨기고, 탐색/수정/모디파이어 계열 키만 사용 가능하게 제한합니다.
    private static readonly HashSet<string> AccentStyleSupportedVks = new(StringComparer.OrdinalIgnoreCase)
    {
        "VK_LSHIFT", "VK_RSHIFT", "VK_SHIFT",
        "VK_LCONTROL", "VK_RCONTROL", "VK_CONTROL",
        "VK_LMENU", "VK_RMENU", "VK_MENU",
        "VK_LEFT", "VK_RIGHT", "VK_UP", "VK_DOWN",
        "VK_TAB", "VK_CAPITAL", "VK_BACK", "VK_RETURN",
        "VK_SPACE", "VK_ESCAPE", "VK_LWIN", "VK_RWIN",
        "VK_PRIOR", "VK_NEXT", "VK_HOME", "VK_END",
        "VK_INSERT", "VK_DELETE"
    };

    [ObservableProperty] private string  editLabel       = "";
    [ObservableProperty] private string? editShiftLabel;
    [ObservableProperty] private double  editWidth       = 1.0;
    [ObservableProperty] private double  editHeight      = DefaultHeightRatio;
    [ObservableProperty] private double  editGapBefore   = 0.0;
    [ObservableProperty] private KeyAction? editAction;
    [ObservableProperty] private string  editStyleKey    = "";
    [ObservableProperty] private bool    useSoftAccentStyle;
    [ObservableProperty] private bool    isSelected      = false;

    [ObservableProperty] private string? englishLabel;
    [ObservableProperty] private string? englishShiftLabel;

    /// <summary>
    /// 편집기 단순화: 기본 문자 키에는 옵션을 숨기고, 일부 특수 키에만 색상 변형 체크를 노출합니다.
    /// </summary>
    public bool SupportsAccentStyle => TryGetActionVk(EditAction) is string vk && AccentStyleSupportedVks.Contains(vk);

    /// 편집 결과를 KeySlot 레코드로 변환
    public KeySlot ToKeySlot() =>
        new(EditLabel, EditShiftLabel, EditAction, EditWidth, EditHeight,
            SupportsAccentStyle && UseSoftAccentStyle ? SoftAccentStyleKey : "", EditGapBefore, EnglishLabel, EnglishShiftLabel);

    partial void OnEditActionChanged(KeyAction? value)
    {
        // 액션이 문자 키에서 특수 키로, 또는 반대로 바뀌면 옵션 노출 여부와 저장값을 함께 정리합니다.
        if (!SupportsAccentStyle)
        {
            UseSoftAccentStyle = false;
            if (!string.IsNullOrEmpty(EditStyleKey))
                EditStyleKey = "";
        }

        OnPropertyChanged(nameof(SupportsAccentStyle));
    }

    partial void OnEditStyleKeyChanged(string value)
    {
        var next = string.Equals(value, SoftAccentStyleKey, StringComparison.Ordinal);
        if (UseSoftAccentStyle != next)
            UseSoftAccentStyle = next;
    }

    partial void OnUseSoftAccentStyleChanged(bool value)
    {
        var next = value && SupportsAccentStyle ? SoftAccentStyleKey : "";
        if (EditStyleKey != next)
            EditStyleKey = next;
    }

    private static string? TryGetActionVk(KeyAction? action) => action switch
    {
        SendKeyAction sendKey => sendKey.Vk,
        ToggleStickyAction toggleSticky => toggleSticky.Vk,
        _ => null
    };
}

// ── 편집 가능한 키 행 VM ────────────────────────────────────────────────────

public partial class EditableKeyRowVm : ObservableObject
{
    [ObservableProperty] private int sharedRowIndex;
    [ObservableProperty] private double heightRatio = EditableKeySlotVm.DefaultHeightRatio;

    [ObservableProperty]
    private ObservableCollection<EditableKeySlotVm> keys = [];

    /// <summary>
    /// 행 높이는 첫 버전에서 기본/낮음 두 단계만 허용합니다.
    /// 숫자열처럼 얇은 줄을 빠르게 만들 수 있게 하되, 자유 수치 편집은 나중 단계로 남겨 둡니다.
    /// </summary>
    public string HeightPresetLabel =>
        Math.Abs(HeightRatio - EditableKeySlotVm.CompactHeightRatio) < 0.001 ? "낮음" : "기본";

    public void ApplyHeight(double heightRatio)
    {
        HeightRatio = heightRatio;
        foreach (var key in Keys)
            key.EditHeight = heightRatio;
        OnPropertyChanged(nameof(HeightPresetLabel));
    }

    public KeyRow ToKeyRow() => new(Keys.Select(k => k.ToKeySlot()).ToList());
}

// ── 편집 가능한 열 VM ────────────────────────────────────────────────────────

public partial class EditableKeyColumnVm : ObservableObject
{
    [ObservableProperty] private double gap = 0;

    [ObservableProperty]
    private ObservableCollection<EditableKeyRowVm> rows = [];

    public KeyColumn ToKeyColumn() => new(Gap, Rows.Select(r => r.ToKeyRow()).ToList());
}

internal sealed record LayoutEditorSnapshot(string CurrentFileName, LayoutConfig Layout);

// ── LayoutEditorViewModel ───────────────────────────────────────────────────

public partial class LayoutEditorViewModel : ObservableObject
{
    private readonly ILayoutRepository _layoutRepository;
    private readonly ConfigService _configService;
    private readonly DispatcherTimer _changeCheckpointTimer;
    private readonly Stack<LayoutEditorSnapshot> _undoStack = [];
    private readonly JsonSerializerOptions _snapshotJsonOptions = new() { WriteIndented = false };
    private LayoutEditorSnapshot? _savedSnapshot;
    private LayoutEditorSnapshot? _trackingSnapshot;
    private LayoutEditorSnapshot? _pendingUndoSnapshot;
    private ObservableCollection<ObservableString>? _actionBuilderComboCollection;
    private bool _isRestoringSnapshot;
    private bool _isLoadingActionBuilder;

    // ── VK → 한글 라벨 매핑 (QWERTY 한국어 표준 배열) ─────────────────────
    private static readonly Dictionary<string, (string Label, string? ShiftLabel, string? EnglishLabel)> VkLabelMap
        = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VK_Q"] = ("ㅂ", "ㅃ", "q"), ["VK_W"] = ("ㅈ", "ㅉ", "w"),
        ["VK_E"] = ("ㄷ", "ㄸ", "e"), ["VK_R"] = ("ㄱ", "ㄲ", "r"),
        ["VK_T"] = ("ㅅ", "ㅆ", "t"), ["VK_Y"] = ("ㅛ", null, "y"),
        ["VK_U"] = ("ㅕ", null, "u"), ["VK_I"] = ("ㅑ", null, "i"),
        ["VK_O"] = ("ㅐ", "ㅒ", "o"), ["VK_P"] = ("ㅔ", "ㅖ", "p"),
        ["VK_A"] = ("ㅁ", null, "a"), ["VK_S"] = ("ㄴ", null, "s"),
        ["VK_D"] = ("ㅇ", null, "d"), ["VK_F"] = ("ㄹ", null, "f"),
        ["VK_G"] = ("ㅎ", null, "g"), ["VK_H"] = ("ㅗ", null, "h"),
        ["VK_J"] = ("ㅓ", null, "j"), ["VK_K"] = ("ㅏ", null, "k"),
        ["VK_L"] = ("ㅣ", null, "l"),
        ["VK_Z"] = ("ㅋ", null, "z"), ["VK_X"] = ("ㅌ", null, "x"),
        ["VK_C"] = ("ㅊ", null, "c"), ["VK_V"] = ("ㅍ", null, "v"),
        ["VK_B"] = ("ㅠ", null, "b"), ["VK_N"] = ("ㅜ", null, "n"),
        ["VK_M"] = ("ㅡ", null, "m"),
    };

    // ── 현재 편집 중인 파일명 ────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExistingLayout))]
    [NotifyPropertyChangedFor(nameof(CanDeleteLayout))]
    private string currentFileName = "";

    partial void OnCurrentFileNameChanged(string value)
    {
        OnPropertyChanged(nameof(IsEditingCurrentLayout));
        HandleWorkingCopyMutated();
    }

    partial void OnLayoutNameChanged(string value) => HandleWorkingCopyMutated();

    /// 기존 파일에서 불러온 상태인지 (저장/다른 이름 저장 분기용)
    public bool IsExistingLayout => !string.IsNullOrEmpty(CurrentFileName)
        && _layoutRepository.GetAvailableLayouts().Contains(CurrentFileName);

    /// 기본 레이아웃이 아닐 때만 삭제 가능
    public bool CanDeleteLayout => IsExistingLayout
        && !string.Equals(CurrentFileName, _layoutRepository.DefaultLayoutName,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 메인 키보드가 현재 기본으로 읽는 레이아웃 이름입니다.
    /// 편집기 저장 대상과 다르면 저장 후에도 메인 키보드에 변화가 없을 수 있습니다.
    /// </summary>
    public string CurrentActiveLayoutName => _configService.Current.DefaultLayout;

    /// <summary>
    /// 현재 편집 중인 파일이 메인 키보드가 쓰는 기본 레이아웃과 같은지 표시합니다.
    /// </summary>
    public bool IsEditingCurrentLayout =>
        !string.IsNullOrWhiteSpace(CurrentFileName)
        && string.Equals(CurrentFileName, CurrentActiveLayoutName, StringComparison.OrdinalIgnoreCase);

    // ── 레이아웃 데이터 ────────────────────────────────────────────────────
    [ObservableProperty] private string layoutName = "";

    [ObservableProperty] private ObservableCollection<EditableKeyColumnVm> columns = [];

    // ── 선택된 열/행/키 ─────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedColumn))]
    private EditableKeyColumnVm? selectedColumn;

    public bool HasSelectedColumn => SelectedColumn is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedRow))]
    private EditableKeyRowVm? selectedRow;

    public bool HasSelectedRow => SelectedRow is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedKey))]
    [NotifyPropertyChangedFor(nameof(CanMoveKeyLeft))]
    [NotifyPropertyChangedFor(nameof(CanMoveKeyRight))]
    private EditableKeySlotVm? selectedKey;

    public bool HasSelectedKey => SelectedKey is not null;

    public bool CanMoveKeyLeft
    {
        get
        {
            if (SelectedKey is null) return false;
            var row = FindRowContaining(SelectedKey);
            if (row is null) return false;
            return row.Keys.IndexOf(SelectedKey) > 0;
        }
    }

    public bool CanMoveKeyRight
    {
        get
        {
            if (SelectedKey is null) return false;
            var row = FindRowContaining(SelectedKey);
            if (row is null) return false;
            return row.Keys.IndexOf(SelectedKey) < row.Keys.Count - 1;
        }
    }

    // ── 저장 결과 알림 메시지 ─────────────────────────────────────────────
    [ObservableProperty] private string statusMessage = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelEditsCommand))]
    private bool hasUnsavedChanges;

    public bool CanUndo => _undoStack.Count > 0;

    // ── 열 삭제 확인 다이얼로그 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanMoveColumnContents))]
    private bool showColumnDeleteDialog = false;

    private EditableKeyColumnVm? _pendingDeleteColumn;

    /// 앞 열이 있을 때만 '앞 열로 이동' 가능
    public bool CanMoveColumnContents =>
        _pendingDeleteColumn is not null && Columns.IndexOf(_pendingDeleteColumn) > 0;

    // ── 내장 ActionBuilder ────────────────────────────────────────────────
    public ActionBuilderViewModel ActionBuilder { get; } = new();

    // ── 사용 가능한 레이아웃 파일 목록 ────────────────────────────────────
    [ObservableProperty]
    private ObservableCollection<string> availableLayouts = [];

    [ObservableProperty]
    private string selectedLayoutToLoad = "";

    public LayoutEditorViewModel(ILayoutRepository layoutRepository, ConfigService configService)
    {
        _layoutRepository = layoutRepository;
        _configService = configService;
        _changeCheckpointTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _changeCheckpointTimer.Tick += (_, _) => FlushPendingCheckpoint();
        HookActionBuilderEvents();
        RefreshAvailableLayouts();
        RefreshCurrentActiveLayoutInfo();
    }

    private void RefreshAvailableLayouts()
    {
        AvailableLayouts = new ObservableCollection<string>(
            _layoutRepository.GetAvailableLayouts());
        if (AvailableLayouts.Count > 0 && string.IsNullOrEmpty(SelectedLayoutToLoad))
            SelectedLayoutToLoad = AvailableLayouts[0];
        OnPropertyChanged(nameof(IsExistingLayout));
        OnPropertyChanged(nameof(CanDeleteLayout));
    }

    private void RefreshCurrentActiveLayoutInfo()
    {
        _configService.Load();
        OnPropertyChanged(nameof(CurrentActiveLayoutName));
        OnPropertyChanged(nameof(IsEditingCurrentLayout));
    }

    // ── 레이아웃 로드 ──────────────────────────────────────────────────────

    [RelayCommand]
    public void LoadLayout(string fileName)
    {
        var config = _layoutRepository.TryLoad(fileName);
        if (config is null) return;
        ApplySnapshot(new LayoutEditorSnapshot(fileName, CloneLayoutConfig(config)), resetUndoHistory: true);
        StatusMessage  = $"'{config.Name}' 불러옴";
    }

    [RelayCommand]
    private void LoadSelected()
    {
        if (!string.IsNullOrEmpty(SelectedLayoutToLoad))
            LoadLayout(SelectedLayoutToLoad);
    }

    // ── 새 레이아웃 생성 ─────────────────────────────────────────────────
    [RelayCommand]
    private void NewLayout()
    {
        ApplySnapshot(new LayoutEditorSnapshot("", new LayoutConfig("새 레이아웃", null, [])), resetUndoHistory: true);
        StatusMessage  = "새 레이아웃 생성됨";
    }

    // ── SelectedKey 변경 시 IsSelected 동기화 ────────────────────────────
    partial void OnSelectedKeyChanged(EditableKeySlotVm? oldValue, EditableKeySlotVm? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
        LoadActionBuilderFromSelectedKey(newValue);
        OnPropertyChanged(nameof(CanMoveKeyLeft));
        OnPropertyChanged(nameof(CanMoveKeyRight));
    }

    // ── 키 선택 / 편집 ────────────────────────────────────────────────────

    [RelayCommand]
    public void SelectKey(EditableKeySlotVm slot)
    {
        SelectedKey = slot;
    }

    /// 선택된 키의 VK 코드를 기반으로 한글/영어 라벨을 자동 채우기
    [RelayCommand]
    private void AutoFillLabels()
    {
        if (SelectedKey is null) return;

        var action = SelectedKey.EditAction ?? ActionBuilder.BuildAction();
        string? vk = action switch
        {
            SendKeyAction a => a.Vk,
            _ => null
        };

        if (vk is null) { StatusMessage = "SendKey 액션이 아닙니다"; return; }

        if (VkLabelMap.TryGetValue(vk, out var mapping))
        {
            SelectedKey.EditLabel       = mapping.Label;
            SelectedKey.EditShiftLabel  = mapping.ShiftLabel;
            SelectedKey.EnglishLabel     = mapping.EnglishLabel;
            SelectedKey.EnglishShiftLabel = null;
            StatusMessage = $"라벨 자동 채움: {mapping.Label} / {mapping.EnglishLabel}";
        }
        else if (ActionBuilderViewModel.KeyDisplayNameMap.TryGetValue(vk, out var displayName))
        {
            SelectedKey.EditLabel = displayName;
            SelectedKey.EditShiftLabel  = null;
            SelectedKey.EnglishLabel     = null;
            SelectedKey.EnglishShiftLabel = null;
            StatusMessage = $"라벨 자동 채움: {displayName}";
        }
        else
        {
            StatusMessage = $"'{vk}'에 대한 라벨 매핑 없음";
        }
    }

    // ── 열 추가/삭제 ─────────────────────────────────────────────────────

    [RelayCommand]
    private void AddColumn()
    {
        var newColumn = new EditableKeyColumnVm { Gap = 0 };
        Columns.Add(newColumn);
        NormalizeSharedRowHeights();
        SelectedColumn = newColumn;
        StatusMessage = "열 추가됨";
    }

    [RelayCommand]
    private void RequestRemoveColumn(EditableKeyColumnVm column)
    {
        // 열에 내용물(행 또는 키)이 있으면 확인 다이얼로그 표시
        bool hasContent = column.Rows.Count > 0 && column.Rows.Any(r => r.Keys.Count > 0);
        if (hasContent)
        {
            _pendingDeleteColumn = column;
            ShowColumnDeleteDialog = true;
        }
        else
        {
            // 빈 열이면 바로 삭제
            ExecuteRemoveColumn(column);
        }
    }

    [RelayCommand]
    private void ConfirmDeleteColumnAll()
    {
        if (_pendingDeleteColumn is not null)
        {
            ExecuteRemoveColumn(_pendingDeleteColumn);
            _pendingDeleteColumn = null;
        }
        ShowColumnDeleteDialog = false;
    }

    [RelayCommand]
    private void ConfirmDeleteColumnMove()
    {
        if (_pendingDeleteColumn is not null)
        {
            int idx = Columns.IndexOf(_pendingDeleteColumn);
            if (idx > 0)
            {
                var prevColumn = Columns[idx - 1];
                // 삭제 대상 열의 행/키를 앞 열로 이동
                // 같은 인덱스의 행이 있으면 그 행에 키를 추가하고,
                // 없으면 새로운 행을 만들어서 추가
                for (int i = 0; i < _pendingDeleteColumn.Rows.Count; i++)
                {
                    if (i < prevColumn.Rows.Count)
                    {
                        // 같은 인덱스의 행이 있으면, 그 행에 모든 키를 추가
                        foreach (var key in _pendingDeleteColumn.Rows[i].Keys)
                            prevColumn.Rows[i].Keys.Add(key);
                    }
                    else
                    {
                        // 같은 인덱스의 행이 없으면, 새로운 행을 만들어서 추가
                        prevColumn.Rows.Add(_pendingDeleteColumn.Rows[i]);
                    }
                }
            }
            // 행을 이동한 후 열 제거
            ExecuteRemoveColumn(_pendingDeleteColumn, clearSelection: true);
            NormalizeSharedRowHeights();
            _pendingDeleteColumn = null;
        }
        ShowColumnDeleteDialog = false;
    }

    [RelayCommand]
    private void CancelDeleteColumn()
    {
        _pendingDeleteColumn = null;
        ShowColumnDeleteDialog = false;
    }

    private void ExecuteRemoveColumn(EditableKeyColumnVm column, bool clearSelection = false)
    {
        if (SelectedKey is not null)
        {
            foreach (var row in column.Rows)
            {
                if (row.Keys.Contains(SelectedKey))
                {
                    SelectedKey = null;
                    break;
                }
            }
        }

        if (SelectedRow is not null && column.Rows.Contains(SelectedRow))
            SelectedRow = null;

        if (SelectedColumn == column)
            SelectedColumn = null;

        Columns.Remove(column);
        NormalizeSharedRowHeights();
        StatusMessage = "열 삭제됨";
    }

    // ── 행 추가/삭제 ─────────────────────────────────────────────────────

    [RelayCommand]
    private void AddRow(EditableKeyColumnVm? targetColumn = null)
    {
        // CommandParameter로 전달된 열이 있으면 그것을 사용하고,
        // 없으면 SelectedColumn을 사용하며, 그것도 없으면 첫 번째 열을 사용
        targetColumn ??= SelectedColumn ?? Columns.FirstOrDefault();
        if (targetColumn is null)
        {
            targetColumn = new EditableKeyColumnVm { Gap = 0 };
            Columns.Add(targetColumn);
        }

        var newRow = new EditableKeyRowVm();
        targetColumn.Rows.Add(newRow);
        NormalizeSharedRowHeights();
        SelectedColumn = targetColumn;
        SelectedRow = newRow;
        StatusMessage = "행 추가됨";
    }

    [RelayCommand]
    private void RemoveRow(EditableKeyRowVm row)
    {
        if (SelectedKey is not null && row.Keys.Contains(SelectedKey))
            SelectedKey = null;

        if (SelectedRow == row)
            SelectedRow = null;

        foreach (var column in Columns)
        {
            if (column.Rows.Contains(row))
            {
                column.Rows.Remove(row);
                break;
            }
        }

        NormalizeSharedRowHeights();
        StatusMessage = "행 삭제됨";
    }

    [RelayCommand]
    private void SetRowDefaultHeight(EditableKeyRowVm row)
    {
        ApplySharedHeightToRowBand(row, EditableKeySlotVm.DefaultHeightRatio);
        StatusMessage = "행 높이를 기본으로 되돌림";
    }

    [RelayCommand]
    private void SetRowCompactHeight(EditableKeyRowVm row)
    {
        ApplySharedHeightToRowBand(row, EditableKeySlotVm.CompactHeightRatio);
        StatusMessage = "행 높이를 낮게 조정함";
    }

    // ── 키 추가/삭제/이동 ─────────────────────────────────────────────────

    [RelayCommand]
    private void MoveKeyLeft()
    {
        if (SelectedKey is null) return;
        var row = FindRowContaining(SelectedKey);
        if (row is null) return;
        var idx = row.Keys.IndexOf(SelectedKey);
        if (idx <= 0) return;
        row.Keys.Move(idx, idx - 1);
        OnPropertyChanged(nameof(CanMoveKeyLeft));
        OnPropertyChanged(nameof(CanMoveKeyRight));
    }

    [RelayCommand]
    private void MoveKeyRight()
    {
        if (SelectedKey is null) return;
        var row = FindRowContaining(SelectedKey);
        if (row is null) return;
        var idx = row.Keys.IndexOf(SelectedKey);
        if (idx < 0 || idx >= row.Keys.Count - 1) return;
        row.Keys.Move(idx, idx + 1);
        OnPropertyChanged(nameof(CanMoveKeyLeft));
        OnPropertyChanged(nameof(CanMoveKeyRight));
    }

    private EditableKeyRowVm? FindRowContaining(EditableKeySlotVm key)
    {
        foreach (var column in Columns)
            foreach (var row in column.Rows)
                if (row.Keys.Contains(key))
                    return row;
        return null;
    }

    [RelayCommand]
    private void AddKeyToRow(EditableKeyRowVm row)
    {
        row.Keys.Add(new EditableKeySlotVm
        {
            EditLabel = "Key",
            EditHeight = row.HeightRatio
        });
    }

    [RelayCommand]
    private void RemoveKey(EditableKeySlotVm key)
    {
        foreach (var column in Columns)
        {
            foreach (var row in column.Rows)
            {
                if (row.Keys.Contains(key))
                {
                    row.Keys.Remove(key);
                    break;
                }
            }
        }

        if (SelectedKey == key)
            SelectedKey = null;
    }

    // ── 레이아웃 삭제 확인 다이얼로그 ──────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeletePendingLayout))]
    private bool showDeleteLayoutDialog = false;

    private string? _pendingDeleteLayoutName;

    /// 삭제할 레이아웃이 기본 레이아웃이 아닌지 확인
    public bool CanDeletePendingLayout =>
        _pendingDeleteLayoutName is not null && !string.Equals(_pendingDeleteLayoutName,
            _layoutRepository.DefaultLayoutName, StringComparison.OrdinalIgnoreCase);

    // ── 레이아웃 삭제 요청 ───────────────────────────────────────────────────
    [RelayCommand]
    private void RequestDeleteLayout()
    {
        if (string.IsNullOrWhiteSpace(CurrentFileName)) return;
        _pendingDeleteLayoutName = CurrentFileName;
        ShowDeleteLayoutDialog = true;
    }

    // ── 레이아웃 삭제 확인 ───────────────────────────────────────────────────
    [RelayCommand]
    private void ConfirmDeleteLayout()
    {
        if (_pendingDeleteLayoutName is null) return;

        try
        {
            if (_layoutRepository.Delete(_pendingDeleteLayoutName))
            {
                // 삭제 후 메인 앱이 목록/캐시를 다시 읽도록 최소 재로드 알림을 보냅니다.
                ToolsReloadSignalService.NotifyReloadLayouts();
                StatusMessage = $"'{_pendingDeleteLayoutName}' 삭제됨";
                ApplySnapshot(new LayoutEditorSnapshot("", new LayoutConfig("", null, [])), resetUndoHistory: true);
                RefreshAvailableLayouts();
            }
            else
            {
                StatusMessage = "삭제 실패: 파일을 찾을 수 없음";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"삭제 실패: {ex.Message}";
        }

        _pendingDeleteLayoutName = null;
        ShowDeleteLayoutDialog = false;
    }

    // ── 레이아웃 삭제 취소 ───────────────────────────────────────────────────
    [RelayCommand]
    private void CancelDeleteLayout()
    {
        _pendingDeleteLayoutName = null;
        ShowDeleteLayoutDialog = false;
    }

    // ── 저장 / 다른 이름으로 저장 / 삭제 ────────────────────────────────

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(CurrentFileName))
        {
            StatusMessage = "파일명을 입력하세요";
            return;
        }

        FlushPendingCheckpoint();

        try
        {
            var layoutToSave = BuildLayoutConfig();
            _layoutRepository.Save(CurrentFileName, layoutToSave);
            // 데이터 본문을 IPC로 보내지 않고, "다시 읽기" 신호만 전달합니다.
            ToolsReloadSignalService.NotifyReloadLayouts();
            RefreshAvailableLayouts();
            RefreshCurrentActiveLayoutInfo();
            ResetUndoHistory(new LayoutEditorSnapshot(CurrentFileName, CloneLayoutConfig(layoutToSave)));

            var verification = _layoutRepository.TryLoad(CurrentFileName);
            var accentKeyCount = verification is null ? 0 : CountSoftAccentKeys(verification);
            var currentLayoutHint = IsEditingCurrentLayout
                ? "현재 메인 키보드에 적용되는 레이아웃입니다."
                : $"메인 키보드는 현재 '{CurrentActiveLayoutName}' 레이아웃을 사용 중입니다.";

            StatusMessage = $"'{CurrentFileName}' 저장 완료 · 강조 키 {accentKeyCount}개 확인 · {currentLayoutHint}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"저장 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveAs()
    {
        if (string.IsNullOrWhiteSpace(CurrentFileName))
        {
            StatusMessage = "파일명을 입력하세요";
            return;
        }

        Save();
        SelectedLayoutToLoad = CurrentFileName;
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        FlushPendingCheckpoint();
        if (_undoStack.Count == 0)
            return;

        var snapshot = _undoStack.Pop();
        ApplySnapshot(snapshot, resetUndoHistory: false);
        StatusMessage = "한 단계 되돌림";
        OnPropertyChanged(nameof(CanUndo));
        UndoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasUnsavedChanges))]
    private void CancelEdits()
    {
        if (_savedSnapshot is null)
            return;

        _changeCheckpointTimer.Stop();
        _pendingUndoSnapshot = null;
        ApplySnapshot(_savedSnapshot, resetUndoHistory: true);
        StatusMessage = "저장 이후 변경을 모두 취소함";
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

    private void HookActionBuilderEvents()
    {
        ActionBuilder.PropertyChanged += OnActionBuilderPropertyChanged;
        RewireActionBuilderComboCollection(_actionBuilderComboCollection, ActionBuilder.SendComboKeysCollection);
    }

    private void OnActionBuilderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActionBuilderViewModel.SendComboKeysCollection))
        {
            RewireActionBuilderComboCollection(_actionBuilderComboCollection, ActionBuilder.SendComboKeysCollection);
        }

        if (_isLoadingActionBuilder || SelectedKey is null)
            return;

        SelectedKey.EditAction = CloneAction(ActionBuilder.BuildAction());
    }

    private void RewireActionBuilderComboCollection(ObservableCollection<ObservableString>? oldCollection,
        ObservableCollection<ObservableString> newCollection)
    {
        if (oldCollection is not null)
        {
            oldCollection.CollectionChanged -= OnActionBuilderComboCollectionChanged;
            foreach (var item in oldCollection)
                item.PropertyChanged -= OnActionBuilderComboItemPropertyChanged;
        }

        newCollection.CollectionChanged -= OnActionBuilderComboCollectionChanged;
        newCollection.CollectionChanged += OnActionBuilderComboCollectionChanged;
        foreach (var item in newCollection)
        {
            item.PropertyChanged -= OnActionBuilderComboItemPropertyChanged;
            item.PropertyChanged += OnActionBuilderComboItemPropertyChanged;
        }

        _actionBuilderComboCollection = newCollection;
    }

    private void OnActionBuilderComboCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ObservableString item in e.OldItems)
                item.PropertyChanged -= OnActionBuilderComboItemPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (ObservableString item in e.NewItems)
            {
                item.PropertyChanged -= OnActionBuilderComboItemPropertyChanged;
                item.PropertyChanged += OnActionBuilderComboItemPropertyChanged;
            }
        }

        OnActionBuilderPropertyChanged(ActionBuilder, new PropertyChangedEventArgs(nameof(ActionBuilderViewModel.SendComboKeysCollection)));
    }

    private void OnActionBuilderComboItemPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnActionBuilderPropertyChanged(ActionBuilder, new PropertyChangedEventArgs(nameof(ActionBuilderViewModel.SendComboKeysCollection)));

    private void LoadActionBuilderFromSelectedKey(EditableKeySlotVm? key)
    {
        _isLoadingActionBuilder = true;
        try
        {
            ActionBuilder.LoadFromAction(key?.EditAction);
        }
        finally
        {
            _isLoadingActionBuilder = false;
        }
    }

    private void AttachWorkingCopyObservers()
    {
        Columns.CollectionChanged -= OnColumnsCollectionChanged;
        Columns.CollectionChanged += OnColumnsCollectionChanged;
        foreach (var column in Columns)
            AttachColumnObservers(column);
    }

    private void DetachWorkingCopyObservers()
    {
        Columns.CollectionChanged -= OnColumnsCollectionChanged;
        foreach (var column in Columns)
            DetachColumnObservers(column);
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (EditableKeyColumnVm column in e.OldItems)
                DetachColumnObservers(column);
        }

        if (e.NewItems is not null)
        {
            foreach (EditableKeyColumnVm column in e.NewItems)
                AttachColumnObservers(column);
        }

        HandleWorkingCopyMutated();
    }

    private void AttachColumnObservers(EditableKeyColumnVm column)
    {
        column.PropertyChanged -= OnWorkingItemPropertyChanged;
        column.PropertyChanged += OnWorkingItemPropertyChanged;
        column.Rows.CollectionChanged -= OnRowsCollectionChanged;
        column.Rows.CollectionChanged += OnRowsCollectionChanged;
        foreach (var row in column.Rows)
            AttachRowObservers(row);
    }

    private void DetachColumnObservers(EditableKeyColumnVm column)
    {
        column.PropertyChanged -= OnWorkingItemPropertyChanged;
        column.Rows.CollectionChanged -= OnRowsCollectionChanged;
        foreach (var row in column.Rows)
            DetachRowObservers(row);
    }

    private void OnRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (EditableKeyRowVm row in e.OldItems)
                DetachRowObservers(row);
        }

        if (e.NewItems is not null)
        {
            foreach (EditableKeyRowVm row in e.NewItems)
                AttachRowObservers(row);
        }

        HandleWorkingCopyMutated();
    }

    private void AttachRowObservers(EditableKeyRowVm row)
    {
        row.PropertyChanged -= OnWorkingItemPropertyChanged;
        row.PropertyChanged += OnWorkingItemPropertyChanged;
        row.Keys.CollectionChanged -= OnKeysCollectionChanged;
        row.Keys.CollectionChanged += OnKeysCollectionChanged;
        foreach (var key in row.Keys)
            AttachKeyObservers(key);
    }

    private void DetachRowObservers(EditableKeyRowVm row)
    {
        row.PropertyChanged -= OnWorkingItemPropertyChanged;
        row.Keys.CollectionChanged -= OnKeysCollectionChanged;
        foreach (var key in row.Keys)
            DetachKeyObservers(key);
    }

    private void OnKeysCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (EditableKeySlotVm key in e.OldItems)
                DetachKeyObservers(key);
        }

        if (e.NewItems is not null)
        {
            foreach (EditableKeySlotVm key in e.NewItems)
                AttachKeyObservers(key);
        }

        HandleWorkingCopyMutated();
    }

    private void AttachKeyObservers(EditableKeySlotVm key)
    {
        key.PropertyChanged -= OnWorkingItemPropertyChanged;
        key.PropertyChanged += OnWorkingItemPropertyChanged;
    }

    private void DetachKeyObservers(EditableKeySlotVm key)
    {
        key.PropertyChanged -= OnWorkingItemPropertyChanged;
    }

    private void OnWorkingItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditableKeySlotVm.IsSelected))
            return;

        HandleWorkingCopyMutated();
    }

    private void HandleWorkingCopyMutated()
    {
        if (_isRestoringSnapshot || _trackingSnapshot is null)
            return;

        if (_pendingUndoSnapshot is null)
            _pendingUndoSnapshot = _trackingSnapshot;

        _changeCheckpointTimer.Stop();
        _changeCheckpointTimer.Start();
    }

    private void FlushPendingCheckpoint()
    {
        _changeCheckpointTimer.Stop();
        if (_pendingUndoSnapshot is null || _isRestoringSnapshot)
            return;

        var currentSnapshot = CaptureSnapshot();
        if (!SnapshotEquals(currentSnapshot, _pendingUndoSnapshot))
        {
            _undoStack.Push(_pendingUndoSnapshot);
            _trackingSnapshot = currentSnapshot;
        }

        _pendingUndoSnapshot = null;
        UpdateDirtyState();
        OnPropertyChanged(nameof(CanUndo));
        UndoCommand.NotifyCanExecuteChanged();
    }

    private void ApplySnapshot(LayoutEditorSnapshot snapshot, bool resetUndoHistory)
    {
        _isRestoringSnapshot = true;
        _changeCheckpointTimer.Stop();
        _pendingUndoSnapshot = null;

        try
        {
            DetachWorkingCopyObservers();

            CurrentFileName = snapshot.CurrentFileName;
            LayoutName = snapshot.Layout.Name;
            Columns = BuildEditableColumns(snapshot.Layout);
            NormalizeSharedRowHeights();
            SelectedColumn = null;
            SelectedRow = null;
            SelectedKey = null;
            RefreshCurrentActiveLayoutInfo();
            AttachWorkingCopyObservers();

            var clonedSnapshot = CloneSnapshot(snapshot);
            _trackingSnapshot = clonedSnapshot;

            if (resetUndoHistory)
            {
                _savedSnapshot = clonedSnapshot;
                _undoStack.Clear();
            }
        }
        finally
        {
            _isRestoringSnapshot = false;
        }

        UpdateDirtyState();
        OnPropertyChanged(nameof(CanUndo));
        UndoCommand.NotifyCanExecuteChanged();
    }

    private void ResetUndoHistory(LayoutEditorSnapshot snapshot)
    {
        _savedSnapshot = CloneSnapshot(snapshot);
        _trackingSnapshot = CloneSnapshot(snapshot);
        _pendingUndoSnapshot = null;
        _undoStack.Clear();
        UpdateDirtyState();
        OnPropertyChanged(nameof(CanUndo));
        UndoCommand.NotifyCanExecuteChanged();
    }

    private void UpdateDirtyState() =>
        HasUnsavedChanges = _savedSnapshot is not null && !SnapshotEquals(CaptureSnapshot(), _savedSnapshot);

    private LayoutEditorSnapshot CaptureSnapshot() =>
        new(CurrentFileName, CloneLayoutConfig(BuildLayoutConfig()));

    private LayoutEditorSnapshot CloneSnapshot(LayoutEditorSnapshot snapshot) =>
        new(snapshot.CurrentFileName, CloneLayoutConfig(snapshot.Layout));

    private ObservableCollection<EditableKeyColumnVm> BuildEditableColumns(LayoutConfig config)
    {
        if (config.Columns is not { Count: > 0 })
            return [];

        var sharedRowHeights = BuildSharedRowHeights(config);

        return new ObservableCollection<EditableKeyColumnVm>(
            config.Columns.Select(col => new EditableKeyColumnVm
            {
                Gap = col.Gap,
                Rows = new ObservableCollection<EditableKeyRowVm>(
                    col.Rows?.Select((r, rowIndex) => new EditableKeyRowVm
                    {
                        SharedRowIndex = rowIndex,
                        HeightRatio = sharedRowHeights.TryGetValue(rowIndex, out var rowHeight)
                            ? rowHeight
                            : EditableKeySlotVm.DefaultHeightRatio,
                        Keys = new ObservableCollection<EditableKeySlotVm>(
                            r.Keys.Select(k => new EditableKeySlotVm
                            {
                                EditLabel = k.Label,
                                EditShiftLabel = k.ShiftLabel,
                                EditWidth = k.Width,
                                EditHeight = sharedRowHeights.TryGetValue(rowIndex, out var slotHeight)
                                    ? slotHeight
                                    : NormalizeHeight(k.Height),
                                EditGapBefore = k.GapBefore,
                                EditStyleKey = k.StyleKey,
                                UseSoftAccentStyle = string.Equals(k.StyleKey, EditableKeySlotVm.SoftAccentStyleKey, StringComparison.Ordinal),
                                EditAction = CloneAction(k.Action),
                                EnglishLabel = k.EnglishLabel,
                                EnglishShiftLabel = k.EnglishShiftLabel,
                            }).ToList())
                    }).ToList() ?? [])
            }).ToList());
    }

    private static LayoutConfig CloneLayoutConfig(LayoutConfig config) =>
        new(config.Name, null,
            config.Columns?.Select(column =>
                new KeyColumn(column.Gap,
                    column.Rows?.Select(row =>
                        new KeyRow(row.Keys.Select(slot =>
                            new KeySlot(slot.Label, slot.ShiftLabel, CloneAction(slot.Action), slot.Width,
                                slot.Height, slot.StyleKey, slot.GapBefore, slot.EnglishLabel, slot.EnglishShiftLabel)).ToList()
                        )).ToList() ?? []
                )).ToList() ?? []);

    private static KeyAction? CloneAction(KeyAction? action) => action switch
    {
        SendKeyAction sendKey => new SendKeyAction(sendKey.Vk),
        SendComboAction sendCombo => new SendComboAction(sendCombo.Keys.ToList()),
        ToggleStickyAction sticky => new ToggleStickyAction(sticky.Vk),
        SwitchLayoutAction switchLayout => new SwitchLayoutAction(switchLayout.Name),
        RunAppAction runApp => new RunAppAction(runApp.Path, runApp.Args),
        BoilerplateAction boilerplate => new BoilerplateAction(boilerplate.Text),
        ShellCommandAction shell => new ShellCommandAction(shell.Command, shell.Shell),
        VolumeControlAction volume => new VolumeControlAction(volume.Direction, volume.Step),
        ClipboardPasteAction clipboard => new ClipboardPasteAction(clipboard.Text),
        ToggleKoreanSubmodeAction => new ToggleKoreanSubmodeAction(),
        _ => action
    };

    private bool SnapshotEquals(LayoutEditorSnapshot left, LayoutEditorSnapshot right) =>
        string.Equals(SerializeSnapshot(left), SerializeSnapshot(right), StringComparison.Ordinal);

    private string SerializeSnapshot(LayoutEditorSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, _snapshotJsonOptions);

    private LayoutConfig BuildLayoutConfig() =>
        new(LayoutName, null, Columns.Select(c => c.ToKeyColumn()).ToList());

    /// <summary>
    /// 같은 행 번호는 모든 열에서 같은 높이로 다루므로, 편집기 안에서도 행 인덱스를 다시 계산해
    /// 공통 높이를 맞춰 둡니다. 새 열/행 추가나 삭제 뒤에 보조 열만 어긋나는 일을 막기 위한 정리 단계입니다.
    /// </summary>
    private void NormalizeSharedRowHeights()
    {
        for (int columnIndex = 0; columnIndex < Columns.Count; columnIndex++)
        {
            var column = Columns[columnIndex];
            for (int rowIndex = 0; rowIndex < column.Rows.Count; rowIndex++)
                column.Rows[rowIndex].SharedRowIndex = rowIndex;
        }

        foreach (var entry in BuildSharedRowHeightMap())
        {
            foreach (var row in FindRowsBySharedIndex(entry.Key))
                row.ApplyHeight(entry.Value);
        }
    }

    private void ApplySharedHeightToRowBand(EditableKeyRowVm sourceRow, double heightRatio)
    {
        foreach (var row in FindRowsBySharedIndex(sourceRow.SharedRowIndex))
            row.ApplyHeight(heightRatio);
    }

    private IEnumerable<EditableKeyRowVm> FindRowsBySharedIndex(int sharedRowIndex) =>
        Columns.SelectMany(column => column.Rows)
            .Where(row => row.SharedRowIndex == sharedRowIndex);

    private Dictionary<int, double> BuildSharedRowHeightMap()
    {
        var result = new Dictionary<int, double>();

        foreach (var row in Columns.SelectMany(column => column.Rows))
        {
            var normalizedHeight = NormalizeHeight(row.HeightRatio);
            if (!result.TryGetValue(row.SharedRowIndex, out var existing)
                || normalizedHeight < existing)
            {
                result[row.SharedRowIndex] = normalizedHeight;
            }
        }

        return result;
    }

    private static Dictionary<int, double> BuildSharedRowHeights(LayoutConfig config)
    {
        var result = new Dictionary<int, double>();
        if (config.Columns is null)
            return result;

        foreach (var column in config.Columns)
        {
            if (column.Rows is null)
                continue;

            for (int rowIndex = 0; rowIndex < column.Rows.Count; rowIndex++)
            {
                var row = column.Rows[rowIndex];
                var rowHeight = row.Keys.Count > 0
                    ? NormalizeHeight(row.Keys[0].Height)
                    : EditableKeySlotVm.DefaultHeightRatio;

                if (!result.TryGetValue(rowIndex, out var existing)
                    || rowHeight < existing)
                {
                    result[rowIndex] = rowHeight;
                }
            }
        }

        return result;
    }

    private static double NormalizeHeight(double heightRatio) =>
        Math.Abs(heightRatio - EditableKeySlotVm.CompactHeightRatio) < 0.001
            ? EditableKeySlotVm.CompactHeightRatio
            : EditableKeySlotVm.DefaultHeightRatio;

    private static int CountSoftAccentKeys(LayoutConfig config) =>
        config.Columns?
            .SelectMany(column => column.Rows ?? [])
            .SelectMany(row => row.Keys)
            .Count(slot => string.Equals(slot.StyleKey, EditableKeySlotVm.SoftAccentStyleKey, StringComparison.Ordinal))
        ?? 0;
}
