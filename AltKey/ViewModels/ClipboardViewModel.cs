using System.Collections.ObjectModel;
using System.Windows;
using WpfApp = System.Windows.Application;
using AltKey.Models;
using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

/// <summary>
/// [역할] 클립보드 항목 하나를 표현하는 데이터 클래스입니다.
/// [속성] FullText: 전체 텍스트, Preview: 미리보기 텍스트, IsFavorite: 즐겨찾기 여부
/// </summary>
public record ClipboardItem(string FullText, string Preview, bool IsFavorite);

public partial class ClipboardViewModel : ObservableObject
{
    private readonly ClipboardService _clipboardService;
    private readonly InputService _inputService;

    [ObservableProperty]
    private bool isVisible;

    // 현재 활성화된 탭 (false: 최근, true: 즐겨찾기)
    [ObservableProperty]
    private bool isFavoritesTab;

    [ObservableProperty]
    private ObservableCollection<ClipboardItem> items = [];

    public ClipboardViewModel(ClipboardService clipboardService, InputService inputService)
    {
        _clipboardService = clipboardService;
        _inputService = inputService;
        _clipboardService.HistoryChanged += RefreshItems;
        _clipboardService.FavoritesChanged += RefreshItems;
    }

    // ── 탭 전환 ───────────────────────────────────────────────────────────

    partial void OnIsFavoritesTabChanged(bool value)
    {
        RefreshItems();
    }

    /// <summary>
    /// "최근" 탭으로 전환합니다.
    /// </summary>
    [RelayCommand]
    private void SwitchToHistoryTab()
    {
        IsFavoritesTab = false;
    }

    /// <summary>
    /// "즐겨찾기" 탭으로 전환합니다.
    /// </summary>
    [RelayCommand]
    private void SwitchToFavoritesTab()
    {
        IsFavoritesTab = true;
    }

    // ── 목록 갱신 ─────────────────────────────────────────────────────────

    private void RefreshItems()
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            Items.Clear();

            if (IsFavoritesTab)
            {
                // 즐겨찾기 탭: 즐겨찾기 목록 표시
                foreach (var text in _clipboardService.Favorites)
                {
                    Items.Add(new ClipboardItem(text, Preview(text), true));
                }
            }
            else
            {
                // 최근 탭: 히스토리 목록 표시 (즐겨찾기 여부 포함)
                foreach (var text in _clipboardService.History)
                {
                    var isFav = _clipboardService.IsFavorite(text);
                    Items.Add(new ClipboardItem(text, Preview(text), isFav));
                }
            }
        });
    }

    // ── 명령 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 항목을 클립보드에 설정하고 붙여넣기 (Ctrl+V)합니다.
    /// </summary>
    [RelayCommand]
    private void PasteItem(string text)
    {
        _clipboardService.PasteItem(text);
        // Ctrl+V 전송으로 붙여넣기
        _inputService.HandleAction(new SendComboAction(["VK_CONTROL", "VK_V"]));
        IsVisible = false; // 선택 즉시 패널 닫기
    }

    /// <summary>
    /// 즐겨찾기를 토글합니다 (추가/제거).
    /// </summary>
    [RelayCommand]
    private void ToggleFavorite(string text)
    {
        _clipboardService.ToggleFavorite(text);
    }

    /// <summary>
    /// 히스토리 전체를 삭제합니다.
    /// </summary>
    [RelayCommand]
    private void ClearHistory()
    {
        _clipboardService.ClearHistory();
        Items.Clear();
    }

    /// <summary>
    /// 패널 표시를 토글합니다.
    /// </summary>
    [RelayCommand]
    private void TogglePanel()
    {
        IsVisible = !IsVisible;
    }

    /// <summary>
    /// 패널을 닫습니다.
    /// </summary>
    [RelayCommand]
    private void Close() => IsVisible = false;

    // ── 유틸리티 ──────────────────────────────────────────────────────────

    private static string Preview(string text)
    {
        // 최대 40자까지 미리보기
        var singleLine = text.Replace('\n', ' ').Replace('\r', ' ');
        return singleLine.Length <= 40 ? singleLine : singleLine[..37] + "...";
    }
}
