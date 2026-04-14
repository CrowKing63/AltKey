using System.Runtime.InteropServices;
using AltKey.Models;
using AltKey.Platform;

namespace AltKey.Services;

public class InputService
{
    // ── Sticky / Lock 상태 ────────────────────────────────────────────────────
    private readonly HashSet<VirtualKeyCode> _stickyKeys = [];
    private readonly HashSet<VirtualKeyCode> _lockedKeys = [];

    public IReadOnlySet<VirtualKeyCode> StickyKeys => _stickyKeys;
    public IReadOnlySet<VirtualKeyCode> LockedKeys => _lockedKeys;

    // ── 이벤트 ───────────────────────────────────────────────────────────────
    /// Sticky / Lock 상태 변경 시 발생 (UI 갱신용)
    public event Action? StickyStateChanged;

    /// SendInput이 ERROR_ACCESS_DENIED를 반환했을 때 발생 (T-2.10)
    public event Action? ElevatedAppDetected;

    // ── T-2.7: Caps Lock 상태 조회 ──────────────────────────────────────────
    public bool IsCapsLockOn => (Win32.GetKeyState((int)VirtualKeyCode.VK_CAPITAL) & 0x0001) != 0;

    // ── T-2.4: 단일 키 전송 ──────────────────────────────────────────────────
    public void SendKeyPress(VirtualKeyCode vk)
    {
        var inputs = new Win32.INPUT[] { MakeKeyDown((ushort)vk), MakeKeyUp((ushort)vk) };
        DispatchInput(inputs);
    }

    public virtual void SendKeyDown(VirtualKeyCode vk)
        => DispatchInput([MakeKeyDown((ushort)vk)]);

    public virtual void SendKeyUp(VirtualKeyCode vk)
        => DispatchInput([MakeKeyUp((ushort)vk)]);

    // ── T-2.10: Win32 SendInput 래퍼 (권한 오류 감지) ────────────────────────
    private void DispatchInput(Win32.INPUT[] inputs)
    {
        uint sent = Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
        if (sent == 0 && Marshal.GetLastWin32Error() == Win32.ERROR_ACCESS_DENIED)
            ElevatedAppDetected?.Invoke();
    }

    // ── T-2.5: 고정 키(Sticky Keys) 상태 관리 ────────────────────────────────
    /// 수식자 키 토글: 미고정 → 일회성 고정 → 영구 잠금 → 해제 순환
    public void ToggleModifier(VirtualKeyCode vk)
    {
        if (_lockedKeys.Contains(vk))
        {
            _lockedKeys.Remove(vk);
            _stickyKeys.Remove(vk);
            SendKeyUp(vk);
        }
        else if (_stickyKeys.Contains(vk))
        {
            _lockedKeys.Add(vk);
            // 영구 잠금 진입 — KeyDown 이미 유지 중
        }
        else
        {
            _stickyKeys.Add(vk);
            SendKeyDown(vk);
        }

        StickyStateChanged?.Invoke();
    }

    /// 일반 키 입력 후 일회성 고정 수식자를 해제한다 (잠금 키는 유지).
    internal void ReleaseTransientModifiers()
    {
        var transient = _stickyKeys.Except(_lockedKeys).ToList();
        foreach (var mod in transient)
        {
            SendKeyUp(mod);
            _stickyKeys.Remove(mod);
        }
        if (transient.Count > 0)
            StickyStateChanged?.Invoke();
    }

    // ── T-2.6: KeyAction 디스패처 ────────────────────────────────────────────
    public void HandleAction(KeyAction action)
    {
        switch (action)
        {
            case SendKeyAction { Vk: var vkStr }:
                if (Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
                {
                    SendKeyPress(vk);
                    ReleaseTransientModifiers();
                }
                break;

            case SendComboAction { Keys: var keys }:
                var vkList = keys
                    .Select(k => Enum.TryParse<VirtualKeyCode>(k, out var v) ? (VirtualKeyCode?)v : null)
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();
                SendCombo(vkList);
                break;

            case ToggleStickyAction { Vk: var vkStr2 }:
                if (Enum.TryParse<VirtualKeyCode>(vkStr2, out var modVk))
                    ToggleModifier(modVk);
                break;
        }
    }

    private void SendCombo(List<VirtualKeyCode> keys)
    {
        foreach (var k in keys) SendKeyDown(k);
        foreach (var k in Enumerable.Reverse(keys)) SendKeyUp(k);
        ReleaseTransientModifiers();
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────────────────
    private static Win32.INPUT MakeKeyDown(ushort vk) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        U = new() { Ki = new() { WVk = vk } }
    };

    private static Win32.INPUT MakeKeyUp(ushort vk) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        U = new() { Ki = new() { WVk = vk, DwFlags = Win32.KEYEVENTF_KEYUP } }
    };
}
