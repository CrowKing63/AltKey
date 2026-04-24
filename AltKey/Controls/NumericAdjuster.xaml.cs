using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace AltKey.Controls;

/// <summary>
/// [역할] 숫자를 직접 입력하거나 버튼을 눌러 미세하게 조절할 수 있는 커스텀 컨트롤입니다.
/// [참고] 설정 화면에서 투명도, 크기 비율 등을 조절할 때 슬라이더 대신 사용됩니다.
/// </summary>
public partial class NumericAdjuster : System.Windows.Controls.UserControl
{
    private bool _isUpdating;

    public NumericAdjuster()
    {
        InitializeComponent();

        DecreaseButton.Click += (s, e) => ChangeValue(-Step);
        IncreaseButton.Click += (s, e) => ChangeValue(Step);

        ValueTextBox.GotKeyboardFocus += (s, e) => ValueTextBox.SelectAll();
        ValueTextBox.LostFocus += OnTextBoxLostFocus;
        ValueTextBox.PreviewKeyDown += OnTextBoxKeyDown;

        Loaded += (s, e) => UpdateTextBox();
    }

    // ── DependencyProperty (설정값 연결) ──────────────────────────────────

    // 현재 설정된 숫자 값입니다.
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(NumericAdjuster),
            new FrameworkPropertyMetadata(0.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    // 입력 가능한 최소값입니다. 이보다 작은 숫자는 입력할 수 없습니다.
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum), typeof(double), typeof(NumericAdjuster),
            new PropertyMetadata(0.0));

    // 입력 가능한 최대값입니다. 이보다 큰 숫자는 입력할 수 없습니다.
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum), typeof(double), typeof(NumericAdjuster),
            new PropertyMetadata(100.0));

    /// <summary>
    /// [중요] 화살표 버튼을 한 번 눌렀을 때 변화하는 수치 단위입니다.
    /// 예를 들어 Step이 5라면, 버튼 클릭 시 5씩 커지거나 작아집니다.
    /// </summary>
    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(
            nameof(Step), typeof(double), typeof(NumericAdjuster),
            new PropertyMetadata(1.0));

    // 화면에 보여줄 소수점 자리수입니다. (0이면 정수만 표시)
    public int DecimalPlaces
    {
        get => (int)GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }

    public static readonly DependencyProperty DecimalPlacesProperty =
        DependencyProperty.Register(
            nameof(DecimalPlaces), typeof(int), typeof(NumericAdjuster),
            new PropertyMetadata(0));

    // ── 스타일 DependencyProperty ────────────────────────────────────────────

    public System.Windows.Media.Brush ButtonBackground
    {
        get => (System.Windows.Media.Brush)GetValue(ButtonBackgroundProperty);
        set => SetValue(ButtonBackgroundProperty, value);
    }

    public static readonly DependencyProperty ButtonBackgroundProperty =
        DependencyProperty.Register(
            nameof(ButtonBackground), typeof(System.Windows.Media.Brush), typeof(NumericAdjuster),
            new PropertyMetadata(null));

    public System.Windows.Media.Brush ButtonForeground
    {
        get => (System.Windows.Media.Brush)GetValue(ButtonForegroundProperty);
        set => SetValue(ButtonForegroundProperty, value);
    }

    public static readonly DependencyProperty ButtonForegroundProperty =
        DependencyProperty.Register(
            nameof(ButtonForeground), typeof(System.Windows.Media.Brush), typeof(NumericAdjuster),
            new PropertyMetadata(null));

    public System.Windows.Media.Brush TextBoxBackground
    {
        get => (System.Windows.Media.Brush)GetValue(TextBoxBackgroundProperty);
        set => SetValue(TextBoxBackgroundProperty, value);
    }

    public static readonly DependencyProperty TextBoxBackgroundProperty =
        DependencyProperty.Register(
            nameof(TextBoxBackground), typeof(System.Windows.Media.Brush), typeof(NumericAdjuster),
            new PropertyMetadata(null));

    public System.Windows.Media.Brush TextBoxForeground
    {
        get => (System.Windows.Media.Brush)GetValue(TextBoxForegroundProperty);
        set => SetValue(TextBoxForegroundProperty, value);
    }

    public static readonly DependencyProperty TextBoxForegroundProperty =
        DependencyProperty.Register(
            nameof(TextBoxForeground), typeof(System.Windows.Media.Brush), typeof(NumericAdjuster),
            new PropertyMetadata(null));

    public System.Windows.Media.Brush TextBoxBorderBrush
    {
        get => (System.Windows.Media.Brush)GetValue(TextBoxBorderBrushProperty);
        set => SetValue(TextBoxBorderBrushProperty, value);
    }

    public static readonly DependencyProperty TextBoxBorderBrushProperty =
        DependencyProperty.Register(
            nameof(TextBoxBorderBrush), typeof(System.Windows.Media.Brush), typeof(NumericAdjuster),
            new PropertyMetadata(null));

    // ── 로직 ────────────────────────────────────────────────────────────────

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NumericAdjuster ctrl && !ctrl._isUpdating)
        {
            ctrl.Value = Clamp(ctrl, (double)e.NewValue);
            ctrl.UpdateTextBox();
        }
    }

    /// <summary>
    /// 현재 값을 지정된 양(delta)만큼 변화시키고 소수점과 범위를 맞춥니다.
    /// </summary>
    private void ChangeValue(double delta)
    {
        Value = Clamp(this, Math.Round(Value + delta, DecimalPlaces));
    }

    private static double Clamp(NumericAdjuster ctrl, double v)
    {
        v = Math.Round(v, ctrl.DecimalPlaces);
        return Math.Clamp(v, ctrl.Minimum, ctrl.Maximum);
    }

    private void UpdateTextBox()
    {
        if (ValueTextBox == null) return;
        _isUpdating = true;
        ValueTextBox.Text = Value.ToString(DecimalPlaces <= 0 ? "F0" : $"F{DecimalPlaces}", CultureInfo.CurrentCulture);
        _isUpdating = false;
    }

    private void ApplyTextBox()
    {
        if (_isUpdating) return;
        _isUpdating = true;

        if (double.TryParse(ValueTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed)
            || double.TryParse(ValueTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            Value = Clamp(this, Math.Round(parsed, DecimalPlaces));
        }

        ValueTextBox.Text = Value.ToString(DecimalPlaces <= 0 ? "F0" : $"F{DecimalPlaces}", CultureInfo.CurrentCulture);
        _isUpdating = false;
    }

    private void OnTextBoxLostFocus(object sender, RoutedEventArgs e) => ApplyTextBox();

    private void OnTextBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyTextBox();
            Keyboard.ClearFocus();
        }
        else if (e.Key == Key.Up)
        {
            ChangeValue(Step);
        }
        else if (e.Key == Key.Down)
        {
            ChangeValue(-Step);
        }
    }
}