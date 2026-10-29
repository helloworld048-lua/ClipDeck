using System;
using System.Runtime.InteropServices;

namespace ClipDeck
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT { public uint type; public InputUnion U; }

    internal static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const int WM_CLIPBOARDUPDATE = 0x031D;
        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_NOACTIVATE = 0x08000000L;
        public const long WS_EX_TOOLWINDOW = 0x00000080L;
        public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;
        public static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll", EntryPoint = "VkKeyScanW", CharSet = CharSet.Unicode)] public static extern short VkKeyScan(char ch);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
