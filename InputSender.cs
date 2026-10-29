using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ClipDeck
{
    /// <summary>Sends keyboard input with SendInput. In "scan code" mode keys are sent as hardware scan codes, which some games need.</summary>
    internal static class InputSender
    {
        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_EXTENDEDKEY = 1, KEYEVENTF_KEYUP = 2, KEYEVENTF_UNICODE = 4, KEYEVENTF_SCANCODE = 8;
        const ushort VK_RETURN = 0x0D;

        static bool IsExtended(ushort vk)
        {
            switch (vk)
            {
                case 0x21: case 0x22: case 0x23: case 0x24: case 0x25: case 0x26: case 0x27: case 0x28:
                case 0x2C: case 0x2D: case 0x2E: case 0x5B: case 0x5C: case 0x6F: case 0x90: case 0xA3: case 0xA5:
                    return true;
                default:
                    return false;
            }
        }

        static INPUT MakeKey(ushort vk, bool up, bool scan)
        {
            var inp = new INPUT();
            inp.type = INPUT_KEYBOARD;
            uint flags = up ? KEYEVENTF_KEYUP : 0u;
            ushort sc = 0;
            if (scan)
            {
                sc = (ushort)Native.MapVirtualKey(vk, 0);
                flags |= KEYEVENTF_SCANCODE;
            }
            if (IsExtended(vk)) flags |= KEYEVENTF_EXTENDEDKEY;
            var ki = new KEYBDINPUT();
            ki.wVk = scan ? (ushort)0 : vk;
            ki.wScan = sc;
            ki.dwFlags = flags;
            ki.time = 0;
            ki.dwExtraInfo = IntPtr.Zero;
            inp.U.ki = ki;
            return inp;
        }

        static INPUT MakeUnicode(char c, bool up)
        {
            var inp = new INPUT();
            inp.type = INPUT_KEYBOARD;
            var ki = new KEYBDINPUT();
            ki.wVk = 0;
            ki.wScan = c;
            ki.dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0u);
            ki.time = 0;
            ki.dwExtraInfo = IntPtr.Zero;
            inp.U.ki = ki;
            return inp;
        }

        static void Send(INPUT inp)
        {
            Native.SendInput(1, new[] { inp }, Marshal.SizeOf(typeof(INPUT)));
        }

        public static async Task<bool> PressAsync(string combo, bool scan, int holdMs)
        {
            ushort[] mods;
            ushort vk;
            if (!KeyCombo.TryParse(combo, out mods, out vk)) return false;
            for (int i = 0; i < mods.Length; i++) Send(MakeKey(mods[i], false, scan));
            Send(MakeKey(vk, false, scan));
            await Task.Delay(Math.Max(holdMs, 1));
            Send(MakeKey(vk, true, scan));
            for (int i = mods.Length - 1; i >= 0; i--) Send(MakeKey(mods[i], true, scan));
            return true;
        }

        public static async Task TypeTextAsync(string text, bool scan, int holdMs, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char c in text)
            {
                ct.ThrowIfCancellationRequested();
                if (c == '\r') continue;
                if (c == '\n')
                {
                    await PressAsync("Enter", scan, holdMs);
                    await Task.Delay(10, ct);
                    continue;
                }
                bool done = false;
                if (scan)
                {
                    short r = Native.VkKeyScan(c);
                    int state = (r >> 8) & 0xFF;
                    if (r != -1 && (state == 0 || state == 1))
                    {
                        ushort vk = (ushort)(r & 0xFF);
                        if (state == 1) Send(MakeKey(KeyCombo.VK_SHIFT, false, true));
                        Send(MakeKey(vk, false, true));
                        await Task.Delay(Math.Max(holdMs, 1), ct);
                        Send(MakeKey(vk, true, true));
                        if (state == 1) Send(MakeKey(KeyCombo.VK_SHIFT, true, true));
                        done = true;
                    }
                }
                if (!done)
                {
                    Send(MakeUnicode(c, false));
                    Send(MakeUnicode(c, true));
                    await Task.Delay(4, ct);
                }
            }
        }

        public static bool AnyModifierDown()
        {
            int[] keys = { 0x10, 0x11, 0x12, 0x5B, 0x5C };
            foreach (int k in keys) if ((Native.GetAsyncKeyState(k) & 0x8000) != 0) return true;
            return false;
        }
    }
}
