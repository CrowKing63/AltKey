using AltKey.Models;

namespace AltKey.ViewModels;

public class KeyRowViewModel
{
    public IReadOnlyList<KeySlot> Keys { get; init; } = [];
}
