using System.Collections.ObjectModel;
using System.Windows;
using WpfApp = System.Windows.Application;
using AltKey.Models;
using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

/// T-8.4: 클립보드 항목
public record ClipboardItem(string FullText, string Preview);

public partial class ClipboardViewModel : ObservableObject
{
    private readonly ClipboardService _clipboardService;
    private readonly InputService _inputService;

    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private ObservableCollection<ClipboardItem> items = [];

    public ClipboardViewModel(ClipboardService clipboardService, InputService inputService)
    {
        _clipboardService = clipboardService;
        _inputService = inputService;
        _clipboardService.HistoryChanged += RefreshItems;
    }

    private void RefreshItems()
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            Items.Clear();
            foreach (var text in _clipboardService.History)
            {
                Items.Add(new ClipboardItem(text, Preview(text)));
            }
        });
    }

    [RelayCommand]
    private void PasteItem(string text)
    {
        _clipboardService.PasteItem(text);
        // Ctrl+V 전송으로 붙여넣기
        _inputService.HandleAction(new SendComboAction(["VK_CONTROL", "VK_V"]));
        IsVisible = false; // 선택 즉시 패널 닫기
    }

    [RelayCommand]
    private void ClearHistory()
    {
        _clipboardService.ClearHistory();
        Items.Clear();
    }

    [RelayCommand]
    private void TogglePanel()
    {
        IsVisible = !IsVisible;
    }

    private static string Preview(string text)
    {
        // 최대 40자까지 미리보기
        var singleLine = text.Replace('\n', ' ').Replace('\r', ' ');
        return singleLine.Length <= 40 ? singleLine : singleLine[..37] + "...";
    }
}
