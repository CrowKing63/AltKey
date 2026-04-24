using System.Text.RegularExpressions;
using AltKey.Models;

namespace AltKey.Services;

public static partial class KeyNotationParser
{
    private static readonly Dictionary<string, VirtualKeyCode> ModifierMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = VirtualKeyCode.VK_CONTROL,
        ["control"] = VirtualKeyCode.VK_CONTROL,
        ["alt"] = VirtualKeyCode.VK_MENU,
        ["menu"] = VirtualKeyCode.VK_MENU,
        ["shift"] = VirtualKeyCode.VK_SHIFT,
        ["win"] = VirtualKeyCode.VK_LWIN,
        ["windows"] = VirtualKeyCode.VK_LWIN,
        ["lctrl"] = VirtualKeyCode.VK_LCONTROL,
        ["lcontrol"] = VirtualKeyCode.VK_LCONTROL,
        ["rctrl"] = VirtualKeyCode.VK_RCONTROL,
        ["rcontrol"] = VirtualKeyCode.VK_RCONTROL,
        ["lalt"] = VirtualKeyCode.VK_LMENU,
        ["ralt"] = VirtualKeyCode.VK_RMENU,
        ["lshift"] = VirtualKeyCode.VK_LSHIFT,
        ["rshift"] = VirtualKeyCode.VK_RSHIFT,
        ["lwin"] = VirtualKeyCode.VK_LWIN,
        ["rwin"] = VirtualKeyCode.VK_RWIN,
    };

    private static readonly Dictionary<string, VirtualKeyCode> KeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = VirtualKeyCode.VK_A, ["b"] = VirtualKeyCode.VK_B, ["c"] = VirtualKeyCode.VK_C,
        ["d"] = VirtualKeyCode.VK_D, ["e"] = VirtualKeyCode.VK_E, ["f"] = VirtualKeyCode.VK_F,
        ["g"] = VirtualKeyCode.VK_G, ["h"] = VirtualKeyCode.VK_H, ["i"] = VirtualKeyCode.VK_I,
        ["j"] = VirtualKeyCode.VK_J, ["k"] = VirtualKeyCode.VK_K, ["l"] = VirtualKeyCode.VK_L,
        ["m"] = VirtualKeyCode.VK_M, ["n"] = VirtualKeyCode.VK_N, ["o"] = VirtualKeyCode.VK_O,
        ["p"] = VirtualKeyCode.VK_P, ["q"] = VirtualKeyCode.VK_Q, ["r"] = VirtualKeyCode.VK_R,
        ["s"] = VirtualKeyCode.VK_S, ["t"] = VirtualKeyCode.VK_T, ["u"] = VirtualKeyCode.VK_U,
        ["v"] = VirtualKeyCode.VK_V, ["w"] = VirtualKeyCode.VK_W, ["x"] = VirtualKeyCode.VK_X,
        ["y"] = VirtualKeyCode.VK_Y, ["z"] = VirtualKeyCode.VK_Z,
        ["0"] = VirtualKeyCode.VK_0, ["1"] = VirtualKeyCode.VK_1, ["2"] = VirtualKeyCode.VK_2,
        ["3"] = VirtualKeyCode.VK_3, ["4"] = VirtualKeyCode.VK_4, ["5"] = VirtualKeyCode.VK_5,
        ["6"] = VirtualKeyCode.VK_6, ["7"] = VirtualKeyCode.VK_7, ["8"] = VirtualKeyCode.VK_8,
        ["9"] = VirtualKeyCode.VK_9,
        ["f1"] = VirtualKeyCode.VK_F1, ["f2"] = VirtualKeyCode.VK_F2, ["f3"] = VirtualKeyCode.VK_F3,
        ["f4"] = VirtualKeyCode.VK_F4, ["f5"] = VirtualKeyCode.VK_F5, ["f6"] = VirtualKeyCode.VK_F6,
        ["f7"] = VirtualKeyCode.VK_F7, ["f8"] = VirtualKeyCode.VK_F8, ["f9"] = VirtualKeyCode.VK_F9,
        ["f10"] = VirtualKeyCode.VK_F10, ["f11"] = VirtualKeyCode.VK_F11, ["f12"] = VirtualKeyCode.VK_F12,
        ["enter"] = VirtualKeyCode.VK_RETURN, ["return"] = VirtualKeyCode.VK_RETURN,
        ["space"] = VirtualKeyCode.VK_SPACE, ["tab"] = VirtualKeyCode.VK_TAB,
        ["backspace"] = VirtualKeyCode.VK_BACK, ["bs"] = VirtualKeyCode.VK_BACK,
        ["escape"] = VirtualKeyCode.VK_ESCAPE, ["esc"] = VirtualKeyCode.VK_ESCAPE,
        ["left"] = VirtualKeyCode.VK_LEFT, ["right"] = VirtualKeyCode.VK_RIGHT,
        ["up"] = VirtualKeyCode.VK_UP, ["down"] = VirtualKeyCode.VK_DOWN,
        ["home"] = VirtualKeyCode.VK_HOME, ["end"] = VirtualKeyCode.VK_END,
        ["pageup"] = VirtualKeyCode.VK_PRIOR, ["pagedown"] = VirtualKeyCode.VK_NEXT,
        ["prior"] = VirtualKeyCode.VK_PRIOR, ["next"] = VirtualKeyCode.VK_NEXT,
        ["insert"] = VirtualKeyCode.VK_INSERT, ["delete"] = VirtualKeyCode.VK_DELETE,
        ["numlock"] = VirtualKeyCode.VK_NUMLOCK, ["scroll"] = VirtualKeyCode.VK_SCROLL,
        ["pause"] = VirtualKeyCode.VK_PAUSE, ["printscreen"] = VirtualKeyCode.VK_PRINT,
        ["snapshot"] = VirtualKeyCode.VK_PRINT,
        ["hangul"] = VirtualKeyCode.VK_HANGUL, ["hanja"] = VirtualKeyCode.VK_HANJA,
        ["capslock"] = VirtualKeyCode.VK_CAPITAL, ["capital"] = VirtualKeyCode.VK_CAPITAL,
    };

    public static (bool IsCombo, List<string> Keys) Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (false, []);

        input = input.Trim();

        if (input.StartsWith("VK_", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<VirtualKeyCode>(input, true, out var vk))
                return (false, [vk.ToString()]);
            return (false, [input.ToUpperInvariant()]);
        }

        var parts = Regex.Split(input, @"\s*\+\s*|\s+")
                         .Where(s => !string.IsNullOrWhiteSpace(s))
                         .ToList();

        if (parts.Count == 1)
        {
            var part = parts[0];
            if (ModifierMap.TryGetValue(part, out var modVk))
                return (false, [modVk.ToString()]);
            if (KeyMap.TryGetValue(part, out var keyVk))
                return (false, [keyVk.ToString()]);
            return (false, [part.ToUpperInvariant()]);
        }

        var keys = new List<string>();
        foreach (var part in parts)
        {
            if (ModifierMap.TryGetValue(part, out var modVk))
                keys.Add(modVk.ToString());
            else if (KeyMap.TryGetValue(part, out var keyVk))
                keys.Add(keyVk.ToString());
            else
                keys.Add(part.ToUpperInvariant());
        }

        return (keys.Count > 1, keys);
    }

    public static string ToNotation(IList<string> vkCodes) =>
        string.Join(",", vkCodes);

    public static string FormatHint(string vkCode) =>
        vkCode.ToUpperInvariant() switch
        {
            "VK_CONTROL" => "Ctrl",
            "VK_MENU" => "Alt",
            "VK_SHIFT" => "Shift",
            "VK_LWIN" => "Win",
            _ => vkCode.Replace("VK_", "")
        };
}
