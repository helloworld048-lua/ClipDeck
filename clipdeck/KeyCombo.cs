using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace ClipDeck
{
    /// <summary>Parses strings like "Ctrl+Shift+T", "F5", "Enter", "/" into virtual-key codes.</summary>
    internal static class KeyCombo
    {
        public const ushort VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B;

        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "enter", "Return" }, { "esc", "Escape" }, { "backspace", "Back" }, { "del", "Delete" }, { "ins", "Insert" },
            { "pageup", "Prior" }, { "pagedown", "Next" }, { "pgup", "Prior" }, { "pgdn", "Next" }, { "caps", "CapsLock" }
        };

        public static bool TryParse(string combo, out ushort[] mods, out ushort vk)
        {
            mods = new ushort[0];
            vk = 0;
            if (string.IsNullOrWhiteSpace(combo)) return false;
            var list = new List<ushort>();
            bool haveKey = false;
            string[] parts = combo.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in parts)
            {
                string t = raw.Trim();
                if (t.Length == 0) continue;
                string l = t.ToLowerInvariant();
                if (l == "ctrl" || l == "control") { AddOnce(list, VK_CONTROL); continue; }
                if (l == "alt") { AddOnce(list, VK_MENU); continue; }
                if (l == "shift") { AddOnce(list, VK_SHIFT); continue; }
                if (l == "win" || l == "windows" || l == "meta") { AddOnce(list, VK_LWIN); continue; }
                if (haveKey) return false;
                if (!TryKey(t, list, out vk)) return false;
                haveKey = true;
            }
            mods = list.ToArray();
            return haveKey;
        }

        static void AddOnce(List<ushort> list, ushort v) { if (!list.Contains(v)) list.Add(v); }

        static bool TryKey(string t, List<ushort> mods, out ushort vk)
        {
            vk = 0;
            if (t.Length == 1)
            {
                char c = t[0];
                if (c >= 'a' && c <= 'z') { vk = (ushort)char.ToUpperInvariant(c); return true; }
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) { vk = (ushort)c; return true; }
                short r = Native.VkKeyScan(c);
                if (r == -1) return false;
                vk = (ushort)(r & 0xFF);
                if ((r & 0x100) != 0) AddOnce(mods, VK_SHIFT);
                return true;
            }
            string name;
            if (!Alias.TryGetValue(t, out name)) name = t;
            if (char.IsDigit(name[0])) return false;
            Key k;
            if (!Enum.TryParse<Key>(name, true, out k)) return false;
            int v = KeyInterop.VirtualKeyFromKey(k);
            if (v == 0) return false;
            vk = (ushort)v;
            return true;
        }

        public static uint ToModFlags(ushort[] mods)
        {
            uint f = 0;
            foreach (ushort m in mods)
            {
                if (m == VK_CONTROL) f |= Native.MOD_CONTROL;
                else if (m == VK_MENU) f |= Native.MOD_ALT;
                else if (m == VK_SHIFT) f |= Native.MOD_SHIFT;
                else if (m == VK_LWIN) f |= Native.MOD_WIN;
            }
            return f;
        }
    }
}
