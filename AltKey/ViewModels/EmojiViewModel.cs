using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

/// T-8.3: 이모지 데이터 모델
public record EmojiCategory(string Name, IReadOnlyList<string> Emoji);

public partial class EmojiViewModel : ObservableObject
{
    private readonly InputService _inputService;

    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private ObservableCollection<EmojiCategory> categories = [];

    [ObservableProperty]
    private EmojiCategory? selectedCategory;

    public EmojiViewModel(InputService inputService)
    {
        _inputService = inputService;
        LoadEmoji();
    }

    private void LoadEmoji()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "emoji.json");
        if (!File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var data = JsonSerializer.Deserialize<EmojiData>(json, options);
            Categories = new ObservableCollection<EmojiCategory>(data?.Categories ?? []);
            SelectedCategory = Categories.FirstOrDefault();
        }
        catch
        {
            // 이모지 로드 실패 시 silently 무시
        }
    }

    [RelayCommand]
    private void SendEmoji(string emoji)
    {
        _inputService.SendUnicode(emoji);
        IsVisible = false; // 선택 즉시 패널 닫기
    }

    [RelayCommand]
    private void SelectCategory(EmojiCategory category)
    {
        SelectedCategory = category;
    }

    [RelayCommand]
    private void TogglePanel()
    {
        IsVisible = !IsVisible;
    }

    [RelayCommand]
    private void Close() => IsVisible = false;
}

// JSON 역직렬화용 데이터 모델
public class EmojiData
{
    public List<EmojiCategory> Categories { get; set; } = [];
}
