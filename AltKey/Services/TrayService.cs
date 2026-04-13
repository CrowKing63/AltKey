using System.Windows;
using System.Windows.Forms;
using AltKey.ViewModels;
using Application = System.Windows.Application;

namespace AltKey.Services;

/// T-5.5 / T-5.11: 시스템 트레이 NotifyIcon + 레이아웃 서브메뉴
public class TrayService : IDisposable
{
    private readonly LayoutService _layoutService;
    private readonly MainViewModel _mainViewModel;

    private NotifyIcon _notifyIcon = null!;
    private Window?    _mainWindow;

    public TrayService(LayoutService layoutService, MainViewModel mainViewModel)
    {
        _layoutService = layoutService;
        _mainViewModel = mainViewModel;
    }

    public void Initialize(Window window)
    {
        _mainWindow = window;

        _notifyIcon = new NotifyIcon
        {
            Text    = "AltKey",
            Visible = true,
        };

        // 아이콘 로드 (없으면 기본 아이콘)
        try
        {
            var iconPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
            if (System.IO.File.Exists(iconPath))
                _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
            else
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }
        catch
        {
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        var menu = BuildContextMenu();
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick     += (_, _) => ToggleVisibility();
    }

    // ── 컨텍스트 메뉴 ────────────────────────────────────────────────────────

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("보이기/숨기기", null, (_, _) => ToggleVisibility());

        // T-5.11: 레이아웃 서브메뉴
        var layoutMenu = new ToolStripMenuItem("레이아웃");
        foreach (var name in _layoutService.GetAvailableLayouts())
        {
            var itemName = name; // 클로저 캡처
            var item = new ToolStripMenuItem(itemName);
            item.Click += (_, _) =>
                Application.Current.Dispatcher.Invoke(() =>
                    _mainViewModel.SwitchLayout(itemName));
            layoutMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(layoutMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("설정", null, (_, _) =>
            Application.Current.Dispatcher.Invoke(() =>
                _mainViewModel.IsSettingsOpen = true));
        menu.Items.Add("종료", null, (_, _) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is MainWindow mw)
                    mw.IsShuttingDown = true;
                Application.Current.Shutdown();
            }));

        return menu;
    }

    // ── 가시성 토글 ──────────────────────────────────────────────────────────

    public void ToggleVisibility()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_mainWindow is null) return;
            if (_mainWindow.IsVisible)
                _mainWindow.Hide();
            else
            {
                _mainWindow.Show();
                _mainWindow.Activate();
            }
        });
    }

    // ── 풍선 알림 ────────────────────────────────────────────────────────────

    public void ShowBalloon(string message)
    {
        _notifyIcon.ShowBalloonTip(3000, "AltKey", message, ToolTipIcon.Info);
    }

    public void Dispose() => _notifyIcon?.Dispose();
}
