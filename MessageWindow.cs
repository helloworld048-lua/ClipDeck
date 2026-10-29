using System;
using System.Windows.Interop;

namespace ClipDeck
{
    /// <summary>Invisible message-only window: receives global hotkeys and clipboard-change notifications.</summary>
    internal sealed class MessageWindow : IDisposable
    {
        readonly HwndSource source;

        public event Action ClipboardChanged;
        public event Action<int> HotkeyPressed;

        public IntPtr Handle { get { return source.Handle; } }

        public MessageWindow()
        {
            var p = new HwndSourceParameters("ClipDeckMessageWindow");
            p.ParentWindow = Native.HWND_MESSAGE;
            p.Width = 0;
            p.Height = 0;
            p.WindowStyle = 0;
            source = new HwndSource(p);
            source.AddHook(Hook);
            Native.AddClipboardFormatListener(source.Handle);
        }

        IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_HOTKEY)
            {
                handled = true;
                Action<int> h = HotkeyPressed;
                if (h != null) h(wParam.ToInt32());
            }
            else if (msg == Native.WM_CLIPBOARDUPDATE)
            {
                handled = true;
                Action c = ClipboardChanged;
                if (c != null) c();
            }
            return IntPtr.Zero;
        }

        public bool RegisterHotkey(int id, string combo)
        {
            ushort[] mods;
            ushort vk;
            if (!KeyCombo.TryParse(combo, out mods, out vk)) return false;
            return Native.RegisterHotKey(source.Handle, id, KeyCombo.ToModFlags(mods) | Native.MOD_NOREPEAT, vk);
        }

        public void UnregisterHotkey(int id)
        {
            Native.UnregisterHotKey(source.Handle, id);
        }

        public void Dispose()
        {
            try { Native.RemoveClipboardFormatListener(source.Handle); } catch { }
            source.RemoveHook(Hook);
            source.Dispose();
        }
    }
}
