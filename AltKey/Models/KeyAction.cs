using System.Text.Json.Serialization;

namespace AltKey.Models;

// 판별 유니온 패턴 (System.Text.Json JsonDerivedType)
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SendKeyAction),      "SendKey")]
[JsonDerivedType(typeof(SendComboAction),    "SendCombo")]
[JsonDerivedType(typeof(ToggleStickyAction), "ToggleSticky")]
[JsonDerivedType(typeof(SwitchLayoutAction), "SwitchLayout")]
public abstract record KeyAction;

public record SendKeyAction(string Vk)               : KeyAction;
public record SendComboAction(List<string> Keys)     : KeyAction;
public record ToggleStickyAction(string Vk)          : KeyAction;
public record SwitchLayoutAction(string Name)        : KeyAction;
