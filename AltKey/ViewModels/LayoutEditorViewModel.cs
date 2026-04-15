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

    public string? HangulLabel      { get; set; }
    public string? HangulShiftLabel { get; set; }

    /// 편집 결과를 KeySlot 레코드로 변환
    public KeySlot ToKeySlot() =>
        new(EditLabel, EditShiftLabel, EditAction, EditWidth, 1.0,
            "", EditGapBefore, HangulLabel, HangulShiftLabel);
}

// ── 편집 가능한 키 행 VM ────────────────────────────────────────────────────

public partial class EditableKeyRowVm : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<EditableKeySlotVm> keys = [];

    public KeyRow ToKeyRow() => new(Keys.Select(k => k.ToKeySlot()).ToList());
}

// ── LayoutEditorViewModel ───────────────────────────────────────────────────

public partial class LayoutEditorViewModel : ObservableObject
{
    private readonly LayoutService _layoutService;

    // 현재 편집 중인 파일명 (확장자 없이)
    private string _currentFileName = "custom";

    // ── 레이아웃 데이터 ────────────────────────────────────────────────────
    [ObservableProperty] private string layoutName = "";

    [ObservableProperty] private ObservableCollection<EditableKeyRowVm> rows = [];

    // ── 선택된 키 ─────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedKey))]
    private EditableKeySlotVm? selectedKey;

    public bool HasSelectedKey => SelectedKey is not null;

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
        Rows = new ObservableCollection<EditableKeyRowVm>(
            config.Rows.Select(r => new EditableKeyRowVm
            {
                Keys = new ObservableCollection<EditableKeySlotVm>(
                    r.Keys.Select(k => new EditableKeySlotVm
                    {
                        EditLabel       = k.Label,
                        EditShiftLabel  = k.ShiftLabel,
                        EditWidth       = k.Width,
                        EditGapBefore   = k.GapBefore,
                        EditAction      = k.Action,
                        HangulLabel     = k.HangulLabel,
                        HangulShiftLabel = k.HangulShiftLabel,
                    }).ToList())
            }).ToList());

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

    // ── 행 추가/삭제 ─────────────────────────────────────────────────────

    [RelayCommand]
    private void AddRow()
    {
        Rows.Add(new EditableKeyRowVm
        {
            Keys = new ObservableCollection<EditableKeySlotVm>
            {
                new() { EditLabel = "Key" }
            }
        });
    }

    [RelayCommand]
    private void RemoveRow(EditableKeyRowVm row)
    {
        if (SelectedKey is not null && row.Keys.Contains(SelectedKey))
        {
            SelectedKey = null;
        }
        Rows.Remove(row);
    }

    // ── 키 추가/삭제 ─────────────────────────────────────────────────────

    [RelayCommand]
    private void AddKeyToRow(EditableKeyRowVm row)
    {
        row.Keys.Add(new EditableKeySlotVm { EditLabel = "Key" });
    }

    [RelayCommand]
    private void RemoveKey(EditableKeySlotVm key)
    {
        foreach (var row in Rows)
            row.Keys.Remove(key);

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
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

    private LayoutConfig BuildLayoutConfig() =>
        new(LayoutName, null, Rows.Select(r => r.ToKeyRow()).ToList());
}
