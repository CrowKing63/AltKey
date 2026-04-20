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

    public string? EnglishLabel      { get; set; }
    public string? EnglishShiftLabel { get; set; }

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
    [ObservableProperty] private double gap = 0.5;

    [ObservableProperty]
    private ObservableCollection<EditableKeyRowVm> rows = [];

    public KeyColumn ToKeyColumn() => new(Gap, Rows.Select(r => r.ToKeyRow()).ToList());
}

// ── LayoutEditorViewModel ───────────────────────────────────────────────────

public partial class LayoutEditorViewModel : ObservableObject
{
    private readonly LayoutService _layoutService;

    // 현재 편집 중인 파일명 (확장자 없이)
    private string _currentFileName = "custom";

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

    // ── 내장 ActionBuilder ────────────────────────────────────────────────
    public ActionBuilderViewModel ActionBuilder { get; } = new();

    // ── 사용 가능한 레이아웃 파일 목록 ────────────────────────────────────
    [ObservableProperty]
    private ObservableCollection<string> availableLayouts = [];

    [ObservableProperty]
    private string selectedLayoutToLoad = "";

    public LayoutEditorViewModel(LayoutService layoutService)
    {
        _layoutService = layoutService;
        RefreshAvailableLayouts();
    }

    private void RefreshAvailableLayouts()
    {
        AvailableLayouts = new ObservableCollection<string>(
            _layoutService.GetAvailableLayouts());
        if (AvailableLayouts.Count > 0 && string.IsNullOrEmpty(SelectedLayoutToLoad))
            SelectedLayoutToLoad = AvailableLayouts[0];
    }

    // ── 레이아웃 로드 ──────────────────────────────────────────────────────

    [RelayCommand]
    public void LoadLayout(string fileName)
    {
        var config = _layoutService.TryLoad(fileName);
        if (config is null) return;

        _currentFileName = fileName;
        LayoutName = config.Name;

        // Columns가 있으면 열 단위로 로드
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
        else if (config.Rows is { Count: > 0 })
        {
            // Rows가 있으면 단일 열로 변환 (하위 호환)
            var rowVms = config.Rows.Select(r => new EditableKeyRowVm
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
            }).ToList();

            Columns = [new EditableKeyColumnVm { Gap = 0, Rows = new ObservableCollection<EditableKeyRowVm>(rowVms) }];
        }
        else
        {
            // Columns와 Rows가 모두 없으면 빈 컬렉션
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

    // ── 열 추가/삭제 ─────────────────────────────────────────────────────

    [RelayCommand]
    private void AddColumn()
    {
        var newColumn = new EditableKeyColumnVm { Gap = 0.5 };
        Columns.Add(newColumn);
        SelectedColumn = newColumn;
        StatusMessage = "열 추가됨";
    }

    [RelayCommand]
    private void RemoveColumn(EditableKeyColumnVm column)
    {
        // 해당 열의 행에 선택된 키가 있으면 초기화
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
    private void AddRow()
    {
        // 선택된 열이 없으면 첫 번째 열에 추가 (또는 새 열 생성)
        var targetColumn = SelectedColumn ?? Columns.FirstOrDefault();
        if (targetColumn is null)
        {
            // 열이 없으면 새 열 생성 후 행 추가
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
        // 해당 행에 선택된 키가 있으면 초기화
        if (SelectedKey is not null && row.Keys.Contains(SelectedKey))
            SelectedKey = null;

        if (SelectedRow == row)
            SelectedRow = null;

        // 행이 속한 열에서 제거
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

    // ── 키 추가/삭제 ─────────────────────────────────────────────────────

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
        // 모든 열의 모든 행에서 해당 키 제거
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

    // ── 저장 ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Save()
    {
        try
        {
            _layoutService.Save(_currentFileName, BuildLayoutConfig());
            RefreshAvailableLayouts();
            StatusMessage = $"'{_currentFileName}' 저장 완료";
        }
        catch (Exception ex)
        {
            StatusMessage = $"저장 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveAs(string newFileName)
    {
        if (string.IsNullOrWhiteSpace(newFileName)) return;
        _currentFileName = newFileName.Trim();
        Save();
        SelectedLayoutToLoad = _currentFileName;
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

    private LayoutConfig BuildLayoutConfig() =>
        new(LayoutName, null, Columns.Select(c => c.ToKeyColumn()).ToList());
}
