using System.Collections.ObjectModel;
using AltKey.Models;
using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

// ── 편집 가능한 키 슬롯 VM ──────────────────────────────────────────────────

public partial class EditableKeySlotVm : ObservableObject
{
    [ObservableProperty] private string  editLabel       = "";
    [ObservableProperty] private string? editShiftLabel;
    [ObservableProperty] private double  editWidth       = 1.0;
    [ObservableProperty] private double  editGapBefore   = 0.0;
    [ObservableProperty] private KeyAction? editAction;
    [ObservableProperty] private bool    isSelected      = false;

    [ObservableProperty] private string? englishLabel;
    [ObservableProperty] private string? englishShiftLabel;

    /// 편집 결과를 KeySlot 레코드로 변환
    public KeySlot ToKeySlot() =>
        new(EditLabel, EditShiftLabel, EditAction, EditWidth, 1.0,
            "", EditGapBefore, EnglishLabel, EnglishShiftLabel);
}

// ── 편집 가능한 키 행 VM ────────────────────────────────────────────────────

public partial class EditableKeyRowVm : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<EditableKeySlotVm> keys = [];

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

// ── LayoutEditorViewModel ───────────────────────────────────────────────────

public partial class LayoutEditorViewModel : ObservableObject
{
    private readonly LayoutService _layoutService;
    private readonly ConfigService _configService;

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

    /// 기존 파일에서 불러온 상태인지 (저장/다른 이름 저장 분기용)
    public bool IsExistingLayout => !string.IsNullOrEmpty(CurrentFileName)
        && _layoutService.GetAvailableLayouts().Contains(CurrentFileName);

    /// 기본 레이아웃이 아닐 때만 삭제 가능
    public bool CanDeleteLayout => IsExistingLayout
        && !string.Equals(CurrentFileName, _configService.Current.DefaultLayout,
            StringComparison.OrdinalIgnoreCase);

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

    public LayoutEditorViewModel(LayoutService layoutService, ConfigService configService)
    {
        _layoutService = layoutService;
        _configService = configService;
        RefreshAvailableLayouts();
    }

    private void RefreshAvailableLayouts()
    {
        AvailableLayouts = new ObservableCollection<string>(
            _layoutService.GetAvailableLayouts());
        if (AvailableLayouts.Count > 0 && string.IsNullOrEmpty(SelectedLayoutToLoad))
            SelectedLayoutToLoad = AvailableLayouts[0];
        OnPropertyChanged(nameof(IsExistingLayout));
        OnPropertyChanged(nameof(CanDeleteLayout));
    }

    // ── 레이아웃 로드 ──────────────────────────────────────────────────────

    [RelayCommand]
    public void LoadLayout(string fileName)
    {
        var config = _layoutService.TryLoad(fileName);
        if (config is null) return;

        CurrentFileName = fileName;
        LayoutName = config.Name;

        if (config.Columns is { Count: > 0 })
        {
            Columns = new ObservableCollection<EditableKeyColumnVm>(
                config.Columns.Select(col => new EditableKeyColumnVm
                {
                    Gap = col.Gap,
                    Rows = new ObservableCollection<EditableKeyRowVm>(
                        col.Rows?.Select(r => new EditableKeyRowVm
                        {
                            Keys = new ObservableCollection<EditableKeySlotVm>(
                                r.Keys.Select(k => new EditableKeySlotVm
                                {
                                    EditLabel       = k.Label,
                                    EditShiftLabel  = k.ShiftLabel,
                                    EditWidth       = k.Width,
                                    EditGapBefore   = k.GapBefore,
                                    EditAction      = k.Action,
                                    EnglishLabel     = k.EnglishLabel,
                                    EnglishShiftLabel = k.EnglishShiftLabel,
                                }).ToList())
                        }).ToList() ?? [])
                }).ToList());
        }
        else
        {
            Columns = [];
        }

        SelectedColumn = null;
        SelectedRow    = null;
        SelectedKey    = null;
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
        CurrentFileName = "";
        LayoutName = "새 레이아웃";
        Columns = [];
        SelectedColumn = null;
        SelectedRow    = null;
        SelectedKey    = null;
        StatusMessage  = "새 레이아웃 생성됨";
    }

    // ── SelectedKey 변경 시 IsSelected 동기화 ────────────────────────────
    partial void OnSelectedKeyChanged(EditableKeySlotVm? oldValue, EditableKeySlotVm? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
        OnPropertyChanged(nameof(CanMoveKeyLeft));
        OnPropertyChanged(nameof(CanMoveKeyRight));
    }

    // ── 키 선택 / 편집 ────────────────────────────────────────────────────

    [RelayCommand]
    public void SelectKey(EditableKeySlotVm slot)
    {
        SelectedKey = slot;
        ActionBuilder.LoadFromAction(slot.EditAction);
    }

    /// ActionBuilder 의 현재 값을 선택된 키에 적용
    [RelayCommand]
    private void ApplyAction()
    {
        if (SelectedKey is null) return;
        SelectedKey.EditAction = ActionBuilder.BuildAction();
        StatusMessage = "액션 적용됨";
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

        StatusMessage = "행 삭제됨";
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
        row.Keys.Add(new EditableKeySlotVm { EditLabel = "Key" });
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
            _configService.Current.DefaultLayout, StringComparison.OrdinalIgnoreCase);

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
            if (_layoutService.Delete(_pendingDeleteLayoutName))
            {
                StatusMessage = $"'{_pendingDeleteLayoutName}' 삭제됨";
                CurrentFileName = "";
                LayoutName = "";
                Columns = [];
                SelectedColumn = null;
                SelectedRow    = null;
                SelectedKey    = null;
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

        try
        {
            _layoutService.Save(CurrentFileName, BuildLayoutConfig());
            RefreshAvailableLayouts();
            StatusMessage = $"'{CurrentFileName}' 저장 완료";
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

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

    private LayoutConfig BuildLayoutConfig() =>
        new(LayoutName, null, Columns.Select(c => c.ToKeyColumn()).ToList());
}
