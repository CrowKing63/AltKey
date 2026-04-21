using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AltKey.Services;

public static class FocusTracker
{
    public static System.Windows.Controls.Primitives.TextBoxBase? LastFocused { get; private set; }

    public static void Register(Window window)
    {
        window.AddHandler(Keyboard.GotKeyboardFocusEvent,
            (KeyboardFocusChangedEventHandler)OnGotKeyboardFocus);
        window.Closed += (_, _) => ClearIfBelongsTo(window);
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is System.Windows.Controls.Primitives.TextBoxBase textBox)
            LastFocused = textBox;
    }

    private static void ClearIfBelongsTo(Window window)
    {
        if (LastFocused == null) return;
        try
        {
            if (Window.GetWindow(LastFocused) == window)
                LastFocused = null;
        }
        catch
        {
            LastFocused = null;
        }
    }
}