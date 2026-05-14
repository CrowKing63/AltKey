using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

/// <summary>
/// [역할] 키보드의 한 줄(행)을 나타내며, 그 줄에 포함된 키들의 목록을 가집니다.
/// </summary>
public partial class KeyRowVm : ObservableObject
{
    public KeyRowVm(int sharedRowIndex, double heightRatio, IReadOnlyList<KeySlotVm> keys)
    {
        SharedRowIndex = sharedRowIndex;
        HeightRatio = heightRatio;
        Keys = keys;
    }

    public int SharedRowIndex { get; }
    public double HeightRatio { get; }
    public IReadOnlyList<KeySlotVm> Keys { get; }

    [ObservableProperty]
    private double pixelHeight;
}

/// <summary>
/// [역할] 키보드의 한 열(세로 줄)을 나타내며, 열 사이의 간격과 그 안의 행들을 관리합니다.
/// </summary>
public record KeyColumnVm(double Gap, IReadOnlyList<KeyRowVm> Rows);

/// <summary>
/// [역할] 키보드 버튼 하나하나의 상태(글자, 크기, 고정 여부 등)를 관리하는 뷰모델입니다.
/// </summary>
public class KeySlotVm(KeySlot slot, AutoCompleteService autoComplete) : ObservableObject
{
    public KeySlot Slot { get; } = slot;
    // 키 스타일 분기용 프록시입니다. ControlTemplate 안에서 깊은 경로 대신 직접 바인딩해 런타임 누락 가능성을 줄입니다.
    public string StyleKey => Slot.StyleKey;
    public bool HasSoftAccentStyle => string.Equals(StyleKey, EditableKeySlotVm.SoftAccentStyleKey, StringComparison.Ordinal);
    public double Width  { get; } = slot.Width;
    public double Height { get; } = slot.Height;

    private bool _isSticky;
    private bool _isLocked;
    private bool _isA11yFocused;
    private FunctionLayerState _functionLayerState;
    private string? _autoCompleteComposeStateLabel;
    private bool _showShiftLabels;
    private bool _isCapsLockOn;
    public bool IsSticky { get => _isSticky; set => SetProperty(ref _isSticky, value); }
    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }
    public bool IsA11yFocused { get => _isA11yFocused; set => SetProperty(ref _isA11yFocused, value); }
    public bool IsFunctionOneShot => _functionLayerState == FunctionLayerState.OneShot;
    public bool IsFunctionLocked => _functionLayerState == FunctionLayerState.Locked;
    public bool HasFunctionLayerAccent => IsFunctionLayerToggle || Slot.FunctionAction is not null;

    public VirtualKeyCode? StickyVk =>
        Slot.Action is ToggleStickyAction ta &&
        Enum.TryParse<VirtualKeyCode>(ta.Vk, ignoreCase: true, out var vk)
            ? vk : null;

    public bool IsKoreanSubmodeToggle => Slot.Action is ToggleKoreanSubmodeAction;
    public bool IsFunctionLayerToggle => Slot.Action is ToggleFunctionLayerAction;

    private InputSubmode _activeSubmode = InputSubmode.HangulJamo;
    public InputSubmode ActiveSubmode
    {
        get => _activeSubmode;
        set
        {
            if (SetProperty(ref _activeSubmode, value))
            {
                RefreshDisplay();
            }
        }
    }

    /// <summary>
    /// 현재 입력 모드(한글/영어)와 상태(Shift 여부)에 따라 버튼에 표시할 글자를 결정합니다.
    /// </summary>
    public string GetLabel(InputSubmode submode)
    {
        if (IsKoreanSubmodeToggle)
            return _autoCompleteComposeStateLabel ?? "가";

        if (submode == InputSubmode.QuietEnglish && Slot.EnglishLabel is { Length: > 0 } eng)
        {
            string baseLabel = ShouldUppercaseEnglishLabel()
                ? (Slot.EnglishShiftLabel ?? eng.ToUpperInvariant())
                : eng;
            return baseLabel;
        }

        return _showShiftLabels && Slot.ShiftLabel is { Length: > 0 } s
            ? s
            : Slot.Label;
    }

    public bool GetIsDimmed(InputSubmode submode) => false;

    public string DisplayLabel { get; private set; } = "";
    public string SubLabelText { get; private set; } = "";
    public bool IsDimmed { get; private set; }

    public void RefreshDisplay()
    {
        DisplayLabel = GetLabel(_activeSubmode);
        SubLabelText = GetSubLabel();
        ApplyFunctionLayerDisplay();
        IsDimmed = GetIsDimmed(_activeSubmode);
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(SubLabelText));
        OnPropertyChanged(nameof(IsDimmed));
        OnPropertyChanged(nameof(AccessibleName));
        OnPropertyChanged(nameof(AccessibleHelp));
        OnPropertyChanged(nameof(AutomationId));
    }

    private void ApplyFunctionLayerDisplay()
    {
        if (_functionLayerState == FunctionLayerState.Inactive || IsFunctionLayerToggle || IsKoreanSubmodeToggle)
            return;

        if (_activeSubmode == InputSubmode.QuietEnglish && Slot.FunctionEnglishLabel is { Length: > 0 } fnEnglish)
        {
            DisplayLabel = ShouldUppercaseEnglishLabel()
                ? (Slot.FunctionEnglishShiftLabel ?? fnEnglish.ToUpperInvariant())
                : fnEnglish;
        }
        else if (_showShiftLabels && Slot.FunctionShiftLabel is { Length: > 0 } fnShift)
        {
            DisplayLabel = fnShift;
        }
        else if (Slot.FunctionLabel is { Length: > 0 } fnLabel)
        {
            DisplayLabel = fnLabel;
        }

        if (_activeSubmode == InputSubmode.HangulJamo && Slot.FunctionEnglishLabel is { Length: > 0 } fnSubEnglish)
        {
            SubLabelText = ShouldUppercaseEnglishLabel()
                ? (Slot.FunctionEnglishShiftLabel ?? fnSubEnglish.ToUpperInvariant())
                : fnSubEnglish;
        }
        else if (_activeSubmode == InputSubmode.QuietEnglish && Slot.FunctionLabel is { Length: > 0 } fnSubLabel)
        {
            SubLabelText = _showShiftLabels && Slot.FunctionShiftLabel is { Length: > 0 } fnSubShift
                ? fnSubShift
                : fnSubLabel;
        }
    }

    public void SetModifierDisplayState(bool showShiftLabels, bool isCapsLockOn)
    {
        if (_showShiftLabels == showShiftLabels && _isCapsLockOn == isCapsLockOn)
            return;

        _showShiftLabels = showShiftLabels;
        _isCapsLockOn = isCapsLockOn;
        RefreshDisplay();
    }

    public void SetComposeStateLabel(string? label)
    {
        if (_autoCompleteComposeStateLabel != label)
        {
            _autoCompleteComposeStateLabel = label;
            if (IsKoreanSubmodeToggle)
                RefreshDisplay();
        }
    }

    public void SetFunctionLayerState(FunctionLayerState state)
    {
        if (_functionLayerState == state)
            return;

        _functionLayerState = state;
        OnPropertyChanged(nameof(IsFunctionOneShot));
        OnPropertyChanged(nameof(IsFunctionLocked));
        RefreshDisplay();
    }

    public string GetSubLabel()
    {
        if (IsKoreanSubmodeToggle)
            return "";

        if (Slot.EnglishLabel is { Length: > 0 } eng)
        {
            if (_activeSubmode == InputSubmode.HangulJamo)
            {
                return ShouldUppercaseEnglishLabel()
                    ? (Slot.EnglishShiftLabel ?? eng.ToUpperInvariant())
                    : eng;
            }
            else if (_activeSubmode == InputSubmode.QuietEnglish)
            {
                return _showShiftLabels && Slot.ShiftLabel is { Length: > 0 } s
                    ? s
                    : Slot.Label;
            }
        }
        return "";
    }

    // 기존 호출부 호환을 위해 남겨 두는 래퍼입니다.
    public string GetSubLabel(bool _) => GetSubLabel();

    /// <summary>
    /// Caps Lock은 실제 영문자 키에만 적용하고, 숫자·기호는 Shift일 때만 치환 라벨을 보여줍니다.
    /// </summary>
    private bool ShouldUppercaseEnglishLabel()
    {
        return _showShiftLabels || (_isCapsLockOn && HasAlphabeticEnglishLabel());
    }

    /// <summary>
    /// 이 키가 Caps Lock으로 대소문자가 바뀌는 영문자 키인지 확인합니다.
    /// </summary>
    private bool HasAlphabeticEnglishLabel()
    {
        string? label = Slot.EnglishLabel;
        if (string.IsNullOrWhiteSpace(label) || label.Length != 1)
            return false;

        return char.IsLetter(label[0]) && label[0] < 128;
    }

    // ── Accessibility ────────────────────────────────────────────────────────

    public string AccessibleName => ComputeAccessibleName();
    public string AccessibleHelp => ComputeAccessibleHelp();
    public string AutomationId   => (_functionLayerState != FunctionLayerState.Inactive && Slot.FunctionAction is not null && !IsFunctionLayerToggle
        ? Slot.FunctionAction
        : Slot.Action)?.GetType().Name ?? "UnknownAction";

    private string ComputeAccessibleName()
    {
        if (IsFunctionLayerToggle)
        {
            return _functionLayerState switch
            {
                FunctionLayerState.OneShot => "Fn 키, 한 번만 적용 상태",
                FunctionLayerState.Locked => "Fn 키, 고정 상태",
                _ => "Fn 키"
            };
        }

        if (_functionLayerState != FunctionLayerState.Inactive && !string.IsNullOrWhiteSpace(DisplayLabel))
            return $"{DisplayLabel} 키";

        if (Slot.Action is ToggleKoreanSubmodeAction)
        {
            return autoComplete.ActiveSubmode == InputSubmode.HangulJamo
                ? "한국어 입력 중, 누르면 영어로 전환"
                : "영어 입력 중, 누르면 한국어로 전환";
        }

        var submode = _activeSubmode;
        if (submode == InputSubmode.HangulJamo)
        {
            string label = _showShiftLabels && Slot.ShiftLabel is { } s ? s : Slot.Label;
            string? jamoName = JamoNameResolver.ResolveKorean(label);
            if (jamoName is not null) return jamoName;
        }
        else
        {
            string? letter = ShouldUppercaseEnglishLabel()
                ? (Slot.EnglishShiftLabel ?? Slot.EnglishLabel?.ToUpperInvariant())
                : Slot.EnglishLabel;
            if (letter is not null) return $"{letter} 키";
        }

        return ResolveFunctionKeyName(Slot);
    }

    private string ComputeAccessibleHelp()
    {
        if (IsFunctionLayerToggle)
        {
            return _functionLayerState switch
            {
                FunctionLayerState.OneShot => "Fn 한 번만 적용",
                FunctionLayerState.Locked => "Fn 고정 상태",
                _ => "Fn 해제됨"
            };
        }

        if (IsSticky) return "일회성 고정 상태";
        if (IsLocked) return "영구 고정 상태";
        return "";
    }

    private static string ResolveFunctionKeyName(KeySlot slot)
    {
        if (slot.Action is SendKeyAction { Vk: var vkStr }
            && Enum.TryParse<VirtualKeyCode>(vkStr, ignoreCase: true, out var vk))
        {
            return vk switch
            {
                VirtualKeyCode.VK_SHIFT   => "시프트 키",
                VirtualKeyCode.VK_LSHIFT  => "왼쪽 시프트 키",
                VirtualKeyCode.VK_RSHIFT  => "오른쪽 시프트 키",
                VirtualKeyCode.VK_CONTROL => "컨트롤 키",
                VirtualKeyCode.VK_LCONTROL => "왼쪽 컨트롤 키",
                VirtualKeyCode.VK_RCONTROL => "오른쪽 컨트롤 키",
                VirtualKeyCode.VK_MENU    => "알트 키",
                VirtualKeyCode.VK_LMENU   => "왼쪽 알트 키",
                VirtualKeyCode.VK_RMENU   => "오른쪽 알트 키",
                VirtualKeyCode.VK_RETURN  => "엔터 키",
                VirtualKeyCode.VK_SPACE   => "스페이스 키",
                VirtualKeyCode.VK_TAB     => "탭 키",
                VirtualKeyCode.VK_BACK    => "백스페이스 키",
                VirtualKeyCode.VK_DELETE  => "딜리트 키",
                VirtualKeyCode.VK_INSERT  => "인서트 키",
                VirtualKeyCode.VK_HOME    => "홈 키",
                VirtualKeyCode.VK_END     => "엔드 키",
                VirtualKeyCode.VK_LEFT    => "왼쪽 화살표 키",
                VirtualKeyCode.VK_RIGHT   => "오른쪽 화살표 키",
                VirtualKeyCode.VK_UP      => "위쪽 화살표 키",
                VirtualKeyCode.VK_DOWN    => "아래쪽 화살표 키",
                VirtualKeyCode.VK_PRIOR   => "페이지 업 키",
                VirtualKeyCode.VK_NEXT    => "페이지 다운 키",
                VirtualKeyCode.VK_ESCAPE  => "이스케이프 키",
                VirtualKeyCode.VK_CAPITAL => "캡스 락 키",
                VirtualKeyCode.VK_F1 => "F1 키", VirtualKeyCode.VK_F2 => "F2 키",
                VirtualKeyCode.VK_F3 => "F3 키", VirtualKeyCode.VK_F4 => "F4 키",
                VirtualKeyCode.VK_F5 => "F5 키", VirtualKeyCode.VK_F6 => "F6 키",
                VirtualKeyCode.VK_F7 => "F7 키", VirtualKeyCode.VK_F8 => "F8 키",
                VirtualKeyCode.VK_F9 => "F9 키", VirtualKeyCode.VK_F10 => "F10 키",
                VirtualKeyCode.VK_F11 => "F11 키", VirtualKeyCode.VK_F12 => "F12 키",
                VirtualKeyCode.VK_HANGUL => "한글 키",
                VirtualKeyCode.VK_HANJA  => "한자 키",
                _ => slot.Label,
            };
        }

        if (slot.Action is ToggleStickyAction { Vk: var stickyVk })
        {
            return $"{stickyVk} 고정 키";
        }

        if (slot.Action is SwitchLayoutAction { Name: var layoutName })
        {
            return $"{layoutName} 레이아웃 전환";
        }

        return slot.Label;
    }
}

/// <summary>
/// [역할] AltKey의 메인 키보드 화면을 제어하는 핵심 엔진입니다.
/// [기능] 레이아웃 로딩, 키 입력 이벤트 처리, 입력 모드 전환, 접근성 내비게이션 등을 총괄합니다.
/// </summary>
public partial class KeyboardViewModel : ObservableObject
{
    private readonly InputService _inputService;
    private readonly SoundService _soundService;
    private readonly AutoCompleteService _autoComplete;
    private readonly ConfigService _configService;
    private readonly LiveRegionService _liveRegion;
    private readonly AccessibilityService _accessibilityService;
    private readonly SuggestionBarViewModel _suggestionBar;
    private readonly List<KeySlotVm> _a11yNavigableSlots = [];
    private int _a11yFocusIndex = -1;
    private A11yFocusOwner _a11yFocusOwner = A11yFocusOwner.None;

    // [접근성] 현재 포커스 하이라이트를 어떤 입력 모드가 소유하는지 외부에서 확인할 수 있게 노출합니다.
    public A11yFocusOwner A11yFocusOwner
    {
        get => _a11yFocusOwner;
        private set => SetProperty(ref _a11yFocusOwner, value);
    }

    private readonly DispatcherTimer _capsLockTimer;

    // L3: 스위치 스캔 입력 모드 타이머
    private DispatcherTimer? _scanTimer;
    private readonly List<ScanTargetVm> _scanTargets = [];
    private int _scanFocusIndex = -1;
    private bool _isRowSelectionPhase = true;
    private int _selectedRowIndex = -1;

    [ObservableProperty]
    private ObservableCollection<KeyColumnVm> columns = [];

    // 동적 크기 계산용 프로퍼티 (열 기준)
    // 키보드 레이아웃을 계산할 때 기준이 되는 값들입니다. (사용자가 직접 수정하기보다 레이아웃 파일에 의해 결정됩니다.)
    [ObservableProperty] private double maxRowUnits = 15.0; // 가장 긴 줄의 가로 길이 합계
    [ObservableProperty] private double maxRowCount = 14.0; // 가장 많은 키가 들어있는 줄의 키 개수
    [ObservableProperty] private double rowCount    = 5.0;  // 전체 줄 수

    /// 빈 행을 포함해 모든 행이 확보해야 할 픽셀 높이 (KeyUnit + 키 Margin 상하 합)
    public double KeyRowHeight => KeyUnit + 4.0;
    public double TotalRowUnits => Math.Max(1.0, GetSharedRowHeightMap().Values.Sum());

    /// 추천 칩 자체 높이입니다. 창 배율이 커지면 KeyUnit을 따라 함께 커지고, 큰 텍스트 모드에서도 글자가 눌리지 않도록 최소 높이를 둡니다.
    /// 너무 이른 구간에서 최소값이 먼저 걸리면 축소 배율에서 칩만 덜 줄어드니, 기본 모드에서는 조금 더 낮은 하한을 사용합니다.
    public double SuggestionChipHeight
    {
        get
        {
            double scaledFontSize = 13.0 * _configService.Current.KeyFontScalePercent / 100.0;
            double fontAwareMinHeight = scaledFontSize + 10.0;
            return Math.Max(fontAwareMinHeight, KeyUnit * 0.62);
        }
    }

    /// 추천 바 전체 높이입니다. 칩 높이에 상하 여백을 더한 값으로 계산해 100% 배율에서도 칩이 잘리지 않게 합니다.
    public double SuggestionBarHeight => SuggestionChipHeight + 6.0;

    partial void OnColumnsChanged(ObservableCollection<KeyColumnVm> value)
    {
        RecalculateLayoutMetrics();
    }

    partial void OnKeyUnitChanged(double value)
    {
        UpdateRowPixelHeights();
        OnPropertyChanged(nameof(KeyRowHeight));
        OnPropertyChanged(nameof(TotalRowUnits));
        OnPropertyChanged(nameof(SuggestionChipHeight));
        OnPropertyChanged(nameof(SuggestionBarHeight));
    }

    /// 열 단위 기준으로 가로·세로 단위를 재계산.
    /// - MaxRowUnits = Σ(열별 가장 긴 행의 단위) + Σ(열 간격)
    /// - MaxRowCount = Σ(열별 가장 긴 행의 키 개수)
    /// - RowCount    = max(열별 행 개수)
    private void RecalculateLayoutMetrics()
    {
        if (Columns.Count == 0 || Columns.All(c => c.Rows.Count == 0))
        {
            MaxRowUnits = 15.0;
            MaxRowCount = 14.0;
            RowCount    = 5.0;
            UpdateRowPixelHeights();
            return;
        }

        double totalUnits = 0;
        double totalKeys  = 0;
        double maxRows    = 0;
        bool first = true;

        foreach (var col in Columns)
        {
            if (!first) totalUnits += col.Gap;
            first = false;

            if (col.Rows.Count == 0) continue;

            double colUnits = col.Rows.Max(r =>
                r.Keys.Sum(k => k.Width) + r.Keys.Sum(k => k.Slot.GapBefore));
            int    colKeys  = col.Rows.Max(r => r.Keys.Count);

            totalUnits += colUnits;
            totalKeys  += colKeys;
            maxRows     = Math.Max(maxRows, col.Rows.Count);
        }

        MaxRowUnits = Math.Max(1, totalUnits);
        MaxRowCount = Math.Max(1, totalKeys);
        RowCount    = Math.Max(1, maxRows);
        UpdateRowPixelHeights();
        OnPropertyChanged(nameof(TotalRowUnits));
    }

    [ObservableProperty]
    private bool showUpperCase;

    [ObservableProperty]
    private bool showElevatedWarning;

    /// <summary>
    /// 키보드 버튼 하나의 기본 크기(단위: 픽셀)입니다. 
    /// 이 값을 바꾸면 전체 키보드의 크기가 비례해서 커지거나 작아집니다.
    /// </summary>
    [ObservableProperty]
    private double keyUnit = 48.0;

    [ObservableProperty]
    private string modeAnnouncement = "";

    public KeyboardViewModel(InputService inputService, SoundService soundService,
        AutoCompleteService autoComplete, ConfigService configService, LiveRegionService liveRegion,
        AccessibilityService accessibilityService, SuggestionBarViewModel suggestionBar)
    {
        _inputService = inputService;
        _soundService = soundService;
        _autoComplete = autoComplete;
        _configService = configService;
        _liveRegion = liveRegion;
        _accessibilityService = accessibilityService;
        _suggestionBar = suggestionBar;
        _inputService.StickyStateChanged += UpdateModifierState;
        _inputService.ElevatedAppDetected += OnElevatedAppDetected;
        _configService.ConfigChanged += OnConfigChanged;
        _suggestionBar.ScanTargetsChanged += OnSuggestionScanTargetsChanged;

        _autoComplete.SubmodeChanged += OnSubmodeChanged;

        _liveRegion.Announced += msg =>
        {
            ModeAnnouncement = msg;
            RaiseLiveRegionChanged();
        };

        _capsLockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _capsLockTimer.Tick += OnTimerTick;
        _capsLockTimer.Start();
    }

    public event Action? LiveRegionChanged;

    private void RaiseLiveRegionChanged()
    {
        LiveRegionChanged?.Invoke();
    }

    /// <summary>
    /// 레이아웃 설정 파일(.json)을 읽어와서 실제 화면에 보일 버튼들로 변환하여 배치합니다.
    /// </summary>
    public void LoadLayout(LayoutConfig layout)
    {
        if (layout.Columns is { Count: > 0 })
        {
            var sharedRowHeights = BuildSharedRowHeights(layout);
            Columns = new ObservableCollection<KeyColumnVm>(
                layout.Columns.Select(col => new KeyColumnVm(
                    col.Gap,
                    col.Rows?.Select((r, rowIndex) => new KeyRowVm(
                        rowIndex,
                        sharedRowHeights.TryGetValue(rowIndex, out var rowHeight) ? rowHeight : 1.0,
                        r.Keys.Select(k => new KeySlotVm(k, _autoComplete)).ToList()
                    )).ToList() ?? []
                ))
            );
        }
        else
        {
            Columns = [];
        }

        UpdateRowPixelHeights();

        _autoComplete.ResetState();
        _inputService.ResetFunctionLayer();
        RefreshKeyLabels(_autoComplete.ActiveSubmode);
        ResetA11yNavigationState();

        // L3: 레이아웃 변경 후 스캔 모드가 켜져 있으면 타이머를 재시작합니다.
        if (_configService.Current.SwitchScanEnabled)
            StartScan();
    }

    private void UpdateRowPixelHeights()
    {
        var sharedRowHeights = GetSharedRowHeightMap();
        foreach (var row in Columns.SelectMany(column => column.Rows))
        {
            var sharedHeight = sharedRowHeights.TryGetValue(row.SharedRowIndex, out var heightRatio)
                ? heightRatio
                : row.HeightRatio;
            row.PixelHeight = sharedHeight * KeyUnit + 4.0;
        }
    }

    private Dictionary<int, double> GetSharedRowHeightMap() =>
        Columns.SelectMany(column => column.Rows)
            .GroupBy(row => row.SharedRowIndex)
            .ToDictionary(group => group.Key, group => group.Min(row => row.HeightRatio));

    private static Dictionary<int, double> BuildSharedRowHeights(LayoutConfig layout)
    {
        var result = new Dictionary<int, double>();
        if (layout.Columns is null)
            return result;

        foreach (var column in layout.Columns)
        {
            if (column.Rows is null)
                continue;

            for (int rowIndex = 0; rowIndex < column.Rows.Count; rowIndex++)
            {
                var row = column.Rows[rowIndex];
                var heightRatio = row.Keys.Count > 0
                    ? NormalizeHeight(row.Keys[0].Height)
                    : EditableKeySlotVm.DefaultHeightRatio;

                if (!result.TryGetValue(rowIndex, out var existing)
                    || heightRatio < existing)
                {
                    result[rowIndex] = heightRatio;
                }
            }
        }

        return result;
    }

    private static double NormalizeHeight(double heightRatio) =>
        Math.Abs(heightRatio - EditableKeySlotVm.CompactHeightRatio) < 0.001
            ? EditableKeySlotVm.CompactHeightRatio
            : EditableKeySlotVm.DefaultHeightRatio;

    public event Action? KeyTapped;

    /// <summary>
    /// 사용자가 키보드의 버튼을 클릭했을 때 실행되는 핵심 함수입니다.
    /// 한글/영어 전환, 실제 키 입력 전송, 사운드 재생 등을 여기서 처리합니다.
    /// </summary>
    [RelayCommand]
    private void KeyPressed(KeySlot slot)
    {
        _soundService.Play();

        KeyAction? effectiveAction = ResolveEffectiveAction(slot);
        bool isFunctionToggleKey = slot.Action is ToggleFunctionLayerAction;
        bool shouldConsumeFunctionLayer = false;

        if (effectiveAction is ToggleKoreanSubmodeAction)
        {
            _autoComplete.ToggleKoreanSubmode();
            UpdateModifierState();
            KeyTapped?.Invoke();
            return;
        }

        if (_inputService.IsFunctionLayerActive
            && !isFunctionToggleKey
            && slot.FunctionAction is not null)
        {
            if (effectiveAction is not null)
                _inputService.HandleAction(effectiveAction);

            FinalizeFunctionLayerKeypress(slot, effectiveAction is not null);
            return;
        }

        if (_inputService.IsForegroundOwnWindow())
        {
            _autoComplete.CancelComposition();

            // 이미 해당 텍스트 상자에 키보드 포커스가 있으면 Focus()를 다시 호출하지 않습니다.
            // 불필요한 포커스 재설정은 WPF/IME 한글 조합을 끊는 원인이 됩니다.
            if (FocusTracker.LastFocused is { IsVisible: true } tb && !tb.IsKeyboardFocused)
                System.Windows.Input.Keyboard.Focus(tb);

            if (IsSeparatorKey(slot))
            {
                if (effectiveAction is not null)
                {
                    _inputService.HandleAction(effectiveAction);
                    shouldConsumeFunctionLayer = !isFunctionToggleKey;
                }
            }
            else if (effectiveAction is not null)
            {
                _inputService.HandleAction(effectiveAction);
                shouldConsumeFunctionLayer = !isFunctionToggleKey;
            }

            FinalizeFunctionLayerKeypress(slot, shouldConsumeFunctionLayer);
            return;
        }

        if (IsSeparatorKey(slot))
        {
            _autoComplete.OnSeparator();
            if (effectiveAction is not null)
            {
                _inputService.HandleAction(effectiveAction);
                shouldConsumeFunctionLayer = !isFunctionToggleKey;
            }
            FinalizeFunctionLayerKeypress(slot, shouldConsumeFunctionLayer);
            return;
        }

        var ctx = new KeyContext(
            ShowUpperCase,
            _inputService.HasActiveModifiers,
            _inputService.HasActiveModifiersExcludingShift,
            _inputService.Mode,
            _inputService.TrackedOnScreenLength);

        bool handled = _autoComplete.OnKey(slot, ctx);
        if (!handled && effectiveAction is not null)
        {
            _inputService.HandleAction(effectiveAction);
            handled = true;
        }

        shouldConsumeFunctionLayer = handled && !isFunctionToggleKey;
        FinalizeFunctionLayerKeypress(slot, shouldConsumeFunctionLayer);

    }

    private KeyAction? ResolveEffectiveAction(KeySlot slot)
    {
        if (_inputService.IsFunctionLayerActive
            && slot.Action is not ToggleFunctionLayerAction
            && slot.FunctionAction is not null)
        {
            return slot.FunctionAction;
        }

        return slot.Action;
    }

    private void FinalizeFunctionLayerKeypress(KeySlot slot, bool consumeFunctionLayer)
    {
        if (consumeFunctionLayer)
            _inputService.ConsumeFunctionLayerAfterAction();

        UpdateModifierState();
        KeyTapped?.Invoke();

        var slotVm = EnumerateSlotVms().FirstOrDefault(vm => vm.Slot == slot);
        _accessibilityService.SpeakLabel(slotVm?.DisplayLabel ?? slot.Label);
    }

    public void MoveA11yFocus(bool reverse)
    {
        if (!_configService.Current.KeyboardA11yNavigationEnabled)
            return;

        // 스캔 하이라이트와 탭 탐색 하이라이트가 동시에 남지 않도록 소유자를 전환합니다.
        if (A11yFocusOwner == A11yFocusOwner.SwitchScan)
            StopScan();
        A11yFocusOwner = A11yFocusOwner.KeyboardNavigation;

        RebuildA11yNavigableSlots();
        if (_a11yNavigableSlots.Count == 0)
            return;

        int count = _a11yNavigableSlots.Count;
        int nextIndex;

        if (_a11yFocusIndex < 0 || _a11yFocusIndex >= count)
        {
            nextIndex = reverse ? count - 1 : 0;
        }
        else
        {
            nextIndex = reverse
                ? (_a11yFocusIndex - 1 + count) % count
                : (_a11yFocusIndex + 1) % count;
        }

        SetA11yFocus(nextIndex);

        // 사용자가 탭으로 직접 이동시키는 경우에만 선택적으로 현재 위치를 공지합니다.
        if (_configService.Current.KeyboardA11yAnnounceFocus)
            AnnounceFocusedTarget();
    }

    public void ActivateA11yFocused()
    {
        if (!_configService.Current.KeyboardA11yNavigationEnabled)
            return;

        RebuildA11yNavigableSlots();
        if (_a11yNavigableSlots.Count == 0)
            return;

        if (_a11yFocusIndex < 0 || _a11yFocusIndex >= _a11yNavigableSlots.Count)
        {
            SetA11yFocus(0);
            return;
        }

        var focused = _a11yNavigableSlots[_a11yFocusIndex];
        KeyPressed(focused.Slot);
    }

    /// <summary>
    /// 접근성 탐색 포커스를 즉시 해제합니다. (Esc 탈출키 처리용)
    /// </summary>
    public void ClearA11yFocus()
    {
        ResetA11yNavigationState();
    }

    private void RebuildA11yNavigableSlots()
    {
        _a11yNavigableSlots.Clear();
        foreach (var vm in EnumerateSlotVms())
            _a11yNavigableSlots.Add(vm);
    }

    private IEnumerable<KeySlotVm> EnumerateSlotVms()
    {
        foreach (var col in Columns)
        foreach (var row in col.Rows)
        foreach (var slotVm in row.Keys)
            yield return slotVm;
    }

    private void SetA11yFocus(int nextIndex)
    {
        if (_a11yFocusIndex >= 0 && _a11yFocusIndex < _a11yNavigableSlots.Count)
            _a11yNavigableSlots[_a11yFocusIndex].IsA11yFocused = false;

        _a11yFocusIndex = nextIndex;

        if (_a11yFocusIndex >= 0 && _a11yFocusIndex < _a11yNavigableSlots.Count)
            _a11yNavigableSlots[_a11yFocusIndex].IsA11yFocused = true;
    }

    private void ResetA11yNavigationState()
    {
        foreach (var vm in EnumerateSlotVms())
            vm.IsA11yFocused = false;

        _a11yNavigableSlots.Clear();
        _a11yFocusIndex = -1;
        A11yFocusOwner = A11yFocusOwner.None;
    }

    private void OnConfigChanged(string? propertyName)
    {
        if (propertyName is null or nameof(AppConfig.KeyFontScalePercent))
        {
            OnPropertyChanged(nameof(SuggestionChipHeight));
            OnPropertyChanged(nameof(SuggestionBarHeight));
        }

        if (propertyName is null or nameof(AppConfig.KeyboardA11yNavigationEnabled))
        {
            if (!_configService.Current.KeyboardA11yNavigationEnabled)
                ResetA11yNavigationState();
        }

        // L3: 스위치 스캔 설정 변경 시 타이머 시작/중지
        if (propertyName is null
            or nameof(AppConfig.SwitchScanEnabled)
            or nameof(AppConfig.SwitchScanIntervalMs)
            or nameof(AppConfig.SwitchScanMode)
            or nameof(AppConfig.SwitchScanInitialDelayMs)
            or nameof(AppConfig.SwitchScanSelectPauseMs)
            or nameof(AppConfig.SwitchScanCyclesBeforePause)
            or nameof(AppConfig.SwitchScanWrapEnabled)
            or nameof(AppConfig.SwitchScanIncludeSuggestions)
            or nameof(AppConfig.SwitchScanSuggestionPriority))
        {
            if (_configService.Current.SwitchScanEnabled)
                StartScan();
            else
                StopScan();
        }
    }

    // ── L3: 스위치 스캔 입력 모드 ───────────────────────────────────────────

    /// <summary>
    /// 스위치 스캔 타이머를 시작하여 키보드 버튼을 순차적으로 하이라이트합니다.
    /// </summary>
    public void StartScan()
    {
        StopScan();
        A11yFocusOwner = A11yFocusOwner.SwitchScan;
        RebuildScanTargets();
        if (_scanTargets.Count == 0)
            return;

        int interval = Math.Clamp(_configService.Current.SwitchScanIntervalMs, 200, 3000);
        _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(interval) };
        _scanTimer.Tick += ScanTick;
        if (_configService.Current.SwitchScanMode != SwitchScanMode.Manual)
            _scanTimer.Start();
        _isRowSelectionPhase = true;
        _selectedRowIndex = -1;

        // 첫 번째 키부터 시작
        SetScanFocus(0);
        AnnounceScanMove(_scanTargets[0].AccessibleName);
    }

    /// <summary>
    /// 스위치 스캔 타이머를 중지하고 하이라이트를 해제합니다.
    /// </summary>
    public void StopScan()
    {
        _scanTimer?.Stop();
        _scanTimer = null;

        foreach (var vm in EnumerateSlotVms())
            vm.IsA11yFocused = false;
        foreach (var vm in _suggestionBar.ScanTargets)
            vm.SetScanFocused(false);
        _scanTargets.Clear();
        _scanFocusIndex = -1;
        _a11yFocusIndex = -1;
        if (A11yFocusOwner == A11yFocusOwner.SwitchScan)
            A11yFocusOwner = A11yFocusOwner.None;
    }

    private void ScanTick(object? sender, EventArgs e)
    {
        if (_scanTargets.Count == 0)
            return;
        AdvanceScan();
    }

    /// <summary>
    /// 현재 스캔 중인 키를 "선택"하여 입력합니다.
    /// </summary>
    public void SelectScanTarget()
    {
        if (_scanFocusIndex >= 0 && _scanFocusIndex < _scanTargets.Count)
        {
            var target = _scanTargets[_scanFocusIndex];

            if (_configService.Current.SwitchScanMode == SwitchScanMode.RowColumn && _isRowSelectionPhase)
            {
                _selectedRowIndex = _scanFocusIndex;
                _isRowSelectionPhase = false;
                RebuildScanTargets();
                if (_scanTargets.Count > 0)
                {
                    SetScanFocus(0);
                    AnnounceScanMove(_scanTargets[0].AccessibleName);
                }
                return;
            }

            AnnounceScanSelection(target.AccessibleName);
            target.Activate();
            if (_configService.Current.SwitchScanMode == SwitchScanMode.RowColumn)
            {
                _isRowSelectionPhase = true;
                _selectedRowIndex = -1;
                RebuildScanTargets();
                if (_scanTargets.Count > 0)
                    SetScanFocus(0);
            }
        }
    }

    /// <summary>
    /// 2스위치 모드에서 "다음" 키로 즉시 이동합니다.
    /// </summary>
    public void AdvanceScan()
    {
        if (_scanTargets.Count == 0)
            return;
        int next = GetNextScanIndex(reverse: false);
        SetScanFocus(next);
        var current = _scanTargets[next];
        AnnounceScanMove(current.AccessibleName);
    }

    /// <summary>
    /// 스캔 대상을 반대 방향으로 이동합니다.
    /// </summary>
    public void ReverseScan()
    {
        if (_scanTargets.Count == 0)
            return;
        int next = GetNextScanIndex(reverse: true);
        SetScanFocus(next);
        var current = _scanTargets[next];
        AnnounceScanMove(current.AccessibleName);
    }

    /// <summary>
    /// 스캔 일시정지/재개를 전환합니다.
    /// </summary>
    public void ToggleScanPaused()
    {
        if (_scanTimer is null) return;
        if (_scanTimer.IsEnabled) _scanTimer.Stop();
        else if (_configService.Current.SwitchScanMode != SwitchScanMode.Manual) _scanTimer.Start();
    }

    private int GetNextScanIndex(bool reverse)
    {
        int count = _scanTargets.Count;
        if (count == 0) return -1;

        int current = _scanFocusIndex;
        if (current < 0 || current >= count)
            current = reverse ? count - 1 : 0;
        else
            current = reverse ? current - 1 : current + 1;

        bool wrap = _configService.Current.SwitchScanWrapEnabled;
        if (wrap)
            return (current + count) % count;

        return Math.Clamp(current, 0, count - 1);
    }

    private void SetScanFocus(int index)
    {
        if (_scanFocusIndex >= 0 && _scanFocusIndex < _scanTargets.Count)
            _scanTargets[_scanFocusIndex].SetScanFocused(false);

        _scanFocusIndex = index;
        _a11yFocusIndex = -1;

        if (_scanFocusIndex >= 0 && _scanFocusIndex < _scanTargets.Count)
            _scanTargets[_scanFocusIndex].SetScanFocused(true);
    }

    private void RebuildScanTargets()
    {
        _scanTargets.Clear();
        var config = _configService.Current;
        var keyboardTargets = BuildKeyboardScanTargets(config.SwitchScanMode);

        var suggestionTargets = config.SwitchScanIncludeSuggestions
            ? _suggestionBar.ScanTargets.ToList()
            : [];

        if (config.SwitchScanSuggestionPriority == SwitchScanSuggestionPriority.BeforeKeyboard)
        {
            _scanTargets.AddRange(suggestionTargets);
            _scanTargets.AddRange(keyboardTargets);
        }
        else
        {
            _scanTargets.AddRange(keyboardTargets);
            _scanTargets.AddRange(suggestionTargets);
        }
    }

    private List<ScanTargetVm> BuildKeyboardScanTargets(SwitchScanMode mode)
    {
        if (mode == SwitchScanMode.RowColumn)
            return _isRowSelectionPhase ? BuildRowTargets() : BuildKeyTargetsInSelectedRow();

        return EnumerateSlotVms()
            .Select(vm => new ScanTargetVm
            {
                DisplayText = vm.DisplayLabel,
                Kind = "KeyboardKey",
                AccessibleName = vm.AccessibleName,
                Activate = () => KeyPressed(vm.Slot),
                SetScanFocused = isFocused => vm.IsA11yFocused = isFocused
            }).ToList();
    }

    private List<ScanTargetVm> BuildRowTargets()
    {
        var targets = new List<ScanTargetVm>();
        int rowIndex = 0;
        foreach (var row in Columns.SelectMany(c => c.Rows))
        {
            int capturedRow = rowIndex;
            string label = $"행 {capturedRow + 1}";
            targets.Add(new ScanTargetVm
            {
                DisplayText = label,
                Kind = "KeyboardRow",
                AccessibleName = label,
                Activate = () => { },
                SetScanFocused = isFocused =>
                {
                    foreach (var key in row.Keys)
                        key.IsA11yFocused = isFocused;
                }
            });
            rowIndex++;
        }
        return targets;
    }

    private List<ScanTargetVm> BuildKeyTargetsInSelectedRow()
    {
        var rows = Columns.SelectMany(c => c.Rows).ToList();
        if (_selectedRowIndex < 0 || _selectedRowIndex >= rows.Count)
            return [];

        var row = rows[_selectedRowIndex];
        return row.Keys.Select(vm => new ScanTargetVm
        {
            DisplayText = vm.DisplayLabel,
            Kind = "KeyboardKey",
            AccessibleName = vm.AccessibleName,
            Activate = () => KeyPressed(vm.Slot),
            SetScanFocused = isFocused => vm.IsA11yFocused = isFocused
        }).ToList();
    }

    private void OnSuggestionScanTargetsChanged()
    {
        if (A11yFocusOwner != A11yFocusOwner.SwitchScan)
            return;

        RebuildScanTargets();
        if (_scanTargets.Count == 0)
        {
            _scanFocusIndex = -1;
            return;
        }

        if (_scanFocusIndex >= _scanTargets.Count || _scanFocusIndex < 0)
            SetScanFocus(0);
    }

    private void AnnounceFocusedTarget()
    {
        if (_a11yFocusIndex < 0 || _a11yFocusIndex >= _a11yNavigableSlots.Count)
            return;
        _liveRegion.Announce(_a11yNavigableSlots[_a11yFocusIndex].AccessibleName);
    }

    private void AnnounceScanMove(string name)
    {
        if (_configService.Current.SwitchScanAnnounceMode != SwitchScanAnnounceMode.EveryMove)
            return;
        _liveRegion.Announce(name);
    }

    private void AnnounceScanSelection(string name)
    {
        if (_configService.Current.SwitchScanAnnounceMode == SwitchScanAnnounceMode.Off)
            return;
        _liveRegion.Announce($"선택: {name}");
    }

    private static bool IsSeparatorKey(KeySlot slot) => slot.Action switch
    {
        SendKeyAction { Vk: "VK_SPACE" }  => true,
        _ => false,
    };

    private void OnSubmodeChanged(InputSubmode submode)
    {
        RefreshKeyLabels(submode);

        _liveRegion.Announce(submode == InputSubmode.HangulJamo
            ? "한국어 입력 상태"
            : "영어 입력 상태");
    }

    private void RefreshKeyLabels(InputSubmode submode)
    {
        foreach (var col in Columns)
            foreach (var row in col.Rows)
                foreach (var keyVm in row.Keys)
                {
                    keyVm.ActiveSubmode = submode;
                    keyVm.SetComposeStateLabel(_autoComplete.ComposeStateLabel);
                    keyVm.SetFunctionLayerState(_inputService.FunctionLayerState);
                }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateModifierState();
    }

    private void UpdateModifierState()
    {
        ShowUpperCase =
            _inputService.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT) ||
            _inputService.LockedKeys.Contains(VirtualKeyCode.VK_SHIFT) ||
            _inputService.StickyKeys.Contains(VirtualKeyCode.VK_LSHIFT) ||
            _inputService.LockedKeys.Contains(VirtualKeyCode.VK_LSHIFT) ||
            _inputService.IsCapsLockOn;

        bool showShiftLabels =
            _inputService.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT) ||
            _inputService.LockedKeys.Contains(VirtualKeyCode.VK_SHIFT) ||
            _inputService.StickyKeys.Contains(VirtualKeyCode.VK_LSHIFT) ||
            _inputService.LockedKeys.Contains(VirtualKeyCode.VK_LSHIFT);

        foreach (var col in Columns)
        foreach (var row in col.Rows)
        foreach (var slotVm in row.Keys)
        {
            if (slotVm.StickyVk is { } vk)
            {
                slotVm.IsSticky = _inputService.StickyKeys.Contains(vk);
                slotVm.IsLocked = _inputService.LockedKeys.Contains(vk);
            }

            if (slotVm.Slot.Action is SendKeyAction { Vk: "VK_CAPITAL" })
            {
                slotVm.IsLocked = _inputService.IsCapsLockOn;
            }

            slotVm.SetFunctionLayerState(_inputService.FunctionLayerState);
            slotVm.SetModifierDisplayState(showShiftLabels, _inputService.IsCapsLockOn);
        }
    }

    private void OnElevatedAppDetected()
    {
        if (_inputService.Mode == InputMode.VirtualKey)
            return;

        ShowElevatedWarning = true;

        var dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        dismissTimer.Tick += (_, _) =>
        {
            ShowElevatedWarning = false;
            dismissTimer.Stop();
        };
        dismissTimer.Start();
    }
}
