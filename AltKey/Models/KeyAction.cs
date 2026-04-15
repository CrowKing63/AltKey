using System.Text.Json.Serialization;

namespace AltKey.Models;

// 판별 유니온 패턴 (System.Text.Json JsonDerivedType)
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SendKeyAction),       "SendKey")]
[JsonDerivedType(typeof(SendComboAction),     "SendCombo")]
[JsonDerivedType(typeof(ToggleStickyAction),  "ToggleSticky")]
[JsonDerivedType(typeof(SwitchLayoutAction),  "SwitchLayout")]
// ── T-9.1 신규 액션 타입 ──────────────────────────────────────────────────────
[JsonDerivedType(typeof(RunAppAction),        "RunApp")]
[JsonDerivedType(typeof(BoilerplateAction),   "Boilerplate")]
[JsonDerivedType(typeof(ShellCommandAction),  "ShellCommand")]
[JsonDerivedType(typeof(VolumeControlAction), "VolumeControl")]
[JsonDerivedType(typeof(ClipboardPasteAction),"ClipboardPaste")]
public abstract record KeyAction;

public record SendKeyAction(string Vk)               : KeyAction;
public record SendComboAction(List<string> Keys)     : KeyAction;
public record ToggleStickyAction(string Vk)          : KeyAction;
public record SwitchLayoutAction(string Name)        : KeyAction;

// ── T-9.1 신규 액션 레코드 ────────────────────────────────────────────────────

/// 애플리케이션 실행
/// Path: 실행 파일 경로 (예: "notepad.exe")
/// Args: 실행 인수 (선택, 기본 "")
public record RunAppAction(string Path, string Args = "") : KeyAction;

/// 상용구 텍스트를 유니코드로 직접 입력
public record BoilerplateAction(string Text) : KeyAction;

/// 셸 명령 실행
/// Shell: "cmd" | "powershell" (기본 "cmd")
/// Hidden: true 면 콘솔 창 숨김 (기본 true)
public record ShellCommandAction(string Command, string Shell = "cmd", bool Hidden = true) : KeyAction;

/// 볼륨 조정
/// Direction: "up" | "down" | "mute"
/// Step: 조정 단계 1~100 (기본 5)
public record VolumeControlAction(string Direction, int Step = 5) : KeyAction;

/// 지정 텍스트를 클립보드에 복사 후 Ctrl+V 로 붙여넣기
public record ClipboardPasteAction(string Text) : KeyAction;
