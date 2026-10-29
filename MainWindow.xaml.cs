using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace ClipDeck
{
    public partial class MainWindow : Window
    {
        const int IdOverlay = 1, IdStop = 2, IdScriptBase = 100;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        readonly MessageWindow msg;
        readonly ScriptRunner runner = new ScriptRunner();
        readonly List<int> registeredIds = new List<int>();
        readonly Dictionary<int, Action> hotkeyActions = new Dictionary<int, Action>();
        readonly ICollectionView clipView;
        System.Windows.Forms.NotifyIcon tray;
        string clipSearch = "";
        DateTime ignoreClipUntil = DateTime.MinValue;
        bool noActivate = true, exiting, placed, busy;
        IntPtr hwnd;

        public MainWindow()
        {
            InitializeComponent();
            hwnd = new WindowInteropHelper(this).EnsureHandle();
            long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
            ex |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
            Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));

            Settings st = Store.Settings;
            ClipOptions.DataContext = st;
            SettingsStack.DataContext = st;
            st.PropertyChanged += Settings_Changed;
            ApplySettings();

            clipView = CollectionViewSource.GetDefaultView(Store.Clips);
            clipView.SortDescriptions.Add(new SortDescription("Pinned", ListSortDirection.Descending));
            clipView.SortDescriptions.Add(new SortDescription("Time", ListSortDirection.Descending));
            clipView.Filter = o =>
            {
                if (clipSearch.Length == 0) return true;
                var c = o as ClipItem;
                return c != null && c.Text.IndexOf(clipSearch, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            var live = clipView as ICollectionViewLiveShaping;
            if (live != null && live.CanChangeLiveSorting)
            {
                live.LiveSortingProperties.Add("Pinned");
                live.LiveSortingProperties.Add("Time");
                live.IsLiveSorting = true;
            }
            ClipList.ItemsSource = clipView;
            ScriptList.ItemsSource = Store.Scripts;
            if (Store.Scripts.Count > 0) ScriptList.SelectedIndex = 0;
            AutoStartBox.IsChecked = GetAutostart();

            msg = new MessageWindow();
            msg.ClipboardChanged += OnClipboardChanged;
            msg.HotkeyPressed += OnHotkey;
            RegisterHotkeys();

            tray = new System.Windows.Forms.NotifyIcon();
            tray.Icon = System.Drawing.SystemIcons.Application;
            tray.Text = "ClipDeck";
            tray.Visible = true;
            tray.MouseClick += (s, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) ToggleOverlay(); };
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Показать / скрыть", null, (s, e) => ToggleOverlay());
            menu.Items.Add("Выход", null, (s, e) => ExitApp());
            tray.ContextMenuStrip = menu;

            Deactivated += (s, e) => { SetNoActivate(true); Store.SaveSoon(); };
            Closing += (s, e) => { if (!exiting) { e.Cancel = true; HideOverlay(); } };
        }

        // ------------------------------------------------------------ window behaviour
        void SetNoActivate(bool on)
        {
            if (hwnd == IntPtr.Zero || noActivate == on) return;
            long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
            ex = on ? (ex | Native.WS_EX_NOACTIVATE) : (ex & ~Native.WS_EX_NOACTIVATE);
            Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));
            noActivate = on;
        }

        static T FindParent<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                var t = d as T;
                if (t != null) return t;
                if (d is Visual || d is System.Windows.Media.Media3D.Visual3D) d = VisualTreeHelper.GetParent(d);
                else d = LogicalTreeHelper.GetParent(d);
            }
            return null;
        }

        // Clicks on text fields need keyboard focus, so the window turns activatable for the moment. Every other click leaves the game/app focused.
        void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var d = e.OriginalSource as DependencyObject;
            if (d != null && (FindParent<TextBoxBase>(d) != null || FindParent<ComboBox>(d) != null))
            {
                SetNoActivate(false);
                Activate();
            }
        }

        void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
        }

        void Place()
        {
            Settings s = Store.Settings;
            Rect wa = SystemParameters.WorkArea;
            if (s.WinWidth > 200 && s.WinHeight > 200) { Width = s.WinWidth; Height = s.WinHeight; }
            Height = Math.Min(Height, Math.Max(MinHeight, wa.Height - 40));
            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            double vr = vl + SystemParameters.VirtualScreenWidth, vb = vt + SystemParameters.VirtualScreenHeight;
            if (s.WinLeft != 0 || s.WinTop != 0)
            {
                Left = Math.Max(vl, Math.Min(s.WinLeft, vr - 80));
                Top = Math.Max(vt, Math.Min(s.WinTop, vb - 80));
            }
            else
            {
                Left = wa.Right - Width - 24;
                Top = wa.Bottom - Height - 24;
            }
            placed = true;
        }

        public void ShowOverlay()
        {
            if (!placed) Place();
            Topmost = false;
            Topmost = Store.Settings.Topmost;
            Show();
        }

        void HideOverlay()
        {
            Settings s = Store.Settings;
            s.WinLeft = Left; s.WinTop = Top; s.WinWidth = Width; s.WinHeight = Height;
            Hide();
            Store.SaveSoon();
        }

        void ToggleOverlay()
        {
            if (IsVisible) HideOverlay(); else ShowOverlay();
        }

        void Hide_Click(object sender, RoutedEventArgs e) { HideOverlay(); }

        void ApplySettings()
        {
            Settings s = Store.Settings;
            Topmost = s.Topmost;
            Opacity = Math.Max(0.4, Math.Min(1.0, s.Opacity));
        }

        void Settings_Changed(object sender, PropertyChangedEventArgs e)
        {
            ApplySettings();
            Store.SaveSoon();
        }

        void SetStatus(string text) { StatusText.Text = text; }

        // ------------------------------------------------------------ tabs
        void Tab_Click(object sender, RoutedEventArgs e)
        {
            ClipsPanel.Visibility = TabClips.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ScriptsPanel.Visibility = TabScripts.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            SettingsPanel.Visibility = TabSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        // ------------------------------------------------------------ clipboard history
        void OnClipboardChanged()
        {
            if (DateTime.UtcNow < ignoreClipUntil) return;
            if (Store.Settings.Paused) return;
            Dispatcher.BeginInvoke(new Action(async () => { await Task.Delay(40); await ReadClipboard(); }));
        }

        async Task ReadClipboard()
        {
            string text = null;
            for (int i = 0; i < 6 && text == null; i++)
            {
                try
                {
                    if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) return;   // password managers set this
                    if (!Clipboard.ContainsText()) return;
                    text = Clipboard.GetText();
                }
                catch { await Task.Delay(30); }
            }
            if (string.IsNullOrWhiteSpace(text) || text.Length > 20000) return;
            AddClip(text);
        }

        void AddClip(string text)
        {
            ClipItem existing = Store.Clips.FirstOrDefault(c => c.Text == text);
            if (existing != null)
            {
                existing.Time = DateTime.Now;
            }
            else
            {
                Store.Clips.Add(new ClipItem { Text = text, Time = DateTime.Now });
                int max = Math.Max(10, Store.Settings.MaxItems);
                List<ClipItem> extra = Store.Clips.Where(c => !c.Pinned).OrderByDescending(c => c.Time).Skip(max).ToList();
                foreach (ClipItem c in extra) Store.Clips.Remove(c);
            }
            Store.SaveSoon();
        }

        void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (clipView == null) return;
            clipSearch = SearchBox.Text ?? "";
            clipView.Refresh();
        }

        void ClipList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var d = e.OriginalSource as DependencyObject;
            if (d == null || FindParent<ButtonBase>(d) != null) return;
            var item = FindParent<ListBoxItem>(d);
            if (item == null) return;
            var clip = item.DataContext as ClipItem;
            ClipList.SelectedItem = null;
            if (clip != null) { var _ = InsertAsync(clip.Text); }
        }

        void Pin_Click(object sender, RoutedEventArgs e)
        {
            var c = (sender as FrameworkElement)?.DataContext as ClipItem;
            if (c == null) return;
            c.Pinned = !c.Pinned;
            clipView.Refresh();
            Store.SaveSoon();
        }

        void DeleteClip_Click(object sender, RoutedEventArgs e)
        {
            var c = (sender as FrameworkElement)?.DataContext as ClipItem;
            if (c == null) return;
            Store.Clips.Remove(c);
            Store.SaveSoon();
        }

        void ClearClips_Click(object sender, RoutedEventArgs e)
        {
            foreach (ClipItem c in Store.Clips.Where(x => !x.Pinned).ToList()) Store.Clips.Remove(c);
            Store.SaveSoon();
            SetStatus("История очищена");
        }

        bool SetClipboardText(string text)
        {
            for (int i = 0; i < 6; i++)
            {
                try { Clipboard.SetDataObject(text, true); return true; }
                catch { Thread.Sleep(25); }
            }
            return false;
        }

        async Task InsertAsync(string text)
        {
            if (busy) return;
            busy = true;
            Settings s = Store.Settings;
            try
            {
                if (s.TypeInstead)
                {
                    await InputSender.TypeTextAsync(text, s.ScanMode, s.HoldMs, CancellationToken.None);
                }
                else
                {
                    ignoreClipUntil = DateTime.UtcNow.AddMilliseconds(700);
                    if (!SetClipboardText(text)) { SetStatus("Буфер занят другой программой, попробуйте ещё раз"); return; }
                    await Task.Delay(50);
                    await InputSender.PressAsync("Ctrl+V", s.ScanMode, s.HoldMs);
                }
                if (s.AutoSend)
                {
                    await Task.Delay(90);
                    await InputSender.PressAsync("Enter", s.ScanMode, s.HoldMs);
                }
                if (s.HideAfter) HideOverlay();
            }
            catch (Exception ex) { SetStatus("Ошибка: " + ex.Message); }
            finally { busy = false; }
        }

        // ------------------------------------------------------------ hotkeys
        void RegisterHotkeys()
        {
            foreach (int id in registeredIds) msg.UnregisterHotkey(id);
            registeredIds.Clear();
            hotkeyActions.Clear();
            var failed = new List<string>();
            Settings s = Store.Settings;

            Action<int, string, Action, string> reg = (id, combo, action, label) =>
            {
                if (string.IsNullOrWhiteSpace(combo)) return;
                if (msg.RegisterHotkey(id, combo)) { registeredIds.Add(id); hotkeyActions[id] = action; }
                else failed.Add(label + " (" + combo + ")");
            };

            reg(IdOverlay, s.OverlayHotkey, ToggleOverlay, "показать окно");
            reg(IdStop, s.StopHotkey, () => runner.Stop(), "стоп");
            for (int i = 0; i < Store.Scripts.Count; i++)
            {
                Script sc = Store.Scripts[i];
                reg(IdScriptBase + i, sc.Hotkey, () => ToggleRun(sc), sc.Name);
            }
            HotkeyInfo.Text = failed.Count == 0 ? "" : "Не удалось занять: " + string.Join(", ", failed) + ". Эта комбинация уже используется другой программой.";
        }

        void OnHotkey(int id)
        {
            Action a;
            if (hotkeyActions.TryGetValue(id, out a)) a();
        }

        static string Compose(KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.ImeProcessed) key = e.ImeProcessedKey;
            if (key == Key.None || key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return null;
            var parts = new List<string>();
            ModifierKeys m = Keyboard.Modifiers;
            if ((m & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((m & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((m & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((m & ModifierKeys.Windows) != 0) parts.Add("Win");
            string name = key.ToString();
            if (key == Key.Return) name = "Enter";
            else if (key == Key.Escape) name = "Esc";
            else if (key == Key.Back) name = "Backspace";
            else if (key == Key.Next) name = "PageDown";
            else if (key == Key.Prior) name = "PageUp";
            else if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1])) name = name.Substring(1);
            parts.Add(name);
            return string.Join("+", parts);
        }

        void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var box = sender as TextBox;
            if (box == null) return;
            e.Handled = true;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            string combo;
            if ((key == Key.Back || key == Key.Delete) && Keyboard.Modifiers == ModifierKeys.None) combo = "";
            else
            {
                combo = Compose(e);
                if (combo == null) return;
            }
            string tag = box.Tag as string;
            if (tag == "overlay") Store.Settings.OverlayHotkey = combo;
            else if (tag == "stop") Store.Settings.StopHotkey = combo;
            else
            {
                var sc = ScriptList.SelectedItem as Script;
                if (sc != null) sc.Hotkey = combo;
            }
            RegisterHotkeys();
            Store.SaveSoon();
        }

        // ------------------------------------------------------------ scripts
        void ScriptList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Editor.DataContext = ScriptList.SelectedItem;
            Editor.IsEnabled = ScriptList.SelectedItem != null;
        }

        void Editor_LostFocus(object sender, KeyboardFocusChangedEventArgs e) { Store.SaveSoon(); }

        void NewScript_Click(object sender, RoutedEventArgs e)
        {
            var s = new Script { Name = "Скрипт " + (Store.Scripts.Count + 1) };
            Store.Scripts.Add(s);
            ScriptList.SelectedItem = s;
            RegisterHotkeys();
            Store.SaveSoon();
        }

        void DeleteScript_Click(object sender, RoutedEventArgs e)
        {
            var s = ScriptList.SelectedItem as Script;
            if (s == null) return;
            int idx = ScriptList.SelectedIndex;
            Store.Scripts.Remove(s);
            if (Store.Scripts.Count > 0) ScriptList.SelectedIndex = Math.Min(idx, Store.Scripts.Count - 1);
            RegisterHotkeys();
            Store.SaveSoon();
        }

        void AddStep(string type, string value)
        {
            var s = ScriptList.SelectedItem as Script;
            if (s == null) return;
            s.Steps.Add(new Step { Type = type, Value = value });
            Store.SaveSoon();
        }
        void AddKeyStep_Click(object sender, RoutedEventArgs e) { AddStep("Key", "Enter"); }
        void AddTextStep_Click(object sender, RoutedEventArgs e) { AddStep("Text", "Привет"); }
        void AddWaitStep_Click(object sender, RoutedEventArgs e) { AddStep("Wait", "500"); }

        void StepType_Click(object sender, RoutedEventArgs e)
        {
            var st = (sender as FrameworkElement)?.DataContext as Step;
            if (st != null) { st.NextType(); Store.SaveSoon(); }
        }

        void MoveStep(object sender, int delta)
        {
            var st = (sender as FrameworkElement)?.DataContext as Step;
            var sc = ScriptList.SelectedItem as Script;
            if (st == null || sc == null) return;
            int i = sc.Steps.IndexOf(st), j = i + delta;
            if (i < 0 || j < 0 || j >= sc.Steps.Count) return;
            sc.Steps.Move(i, j);
            Store.SaveSoon();
        }
        void StepUp_Click(object sender, RoutedEventArgs e) { MoveStep(sender, -1); }
        void StepDown_Click(object sender, RoutedEventArgs e) { MoveStep(sender, 1); }

        void StepDelete_Click(object sender, RoutedEventArgs e)
        {
            var st = (sender as FrameworkElement)?.DataContext as Step;
            var sc = ScriptList.SelectedItem as Script;
            if (st == null || sc == null) return;
            sc.Steps.Remove(st);
            Store.SaveSoon();
        }

        void RunScript_Click(object sender, RoutedEventArgs e)
        {
            var s = ScriptList.SelectedItem as Script;
            if (s != null && !runner.Running) { var _ = RunSafe(s); }
        }

        void StopScript_Click(object sender, RoutedEventArgs e) { runner.Stop(); }

        void ToggleRun(Script s)
        {
            if (runner.Running) { runner.Stop(); return; }
            var _ = RunSafe(s);
        }

        async Task RunSafe(Script s)
        {
            try
            {
                SetStatus("Идёт: " + s.Name + "  (стоп: " + Store.Settings.StopHotkey + ")");
                await runner.RunAsync(s, Store.Settings);
                SetStatus("Скрипт завершён");
            }
            catch (Exception ex) { SetStatus("Ошибка: " + ex.Message); }
        }

        // ------------------------------------------------------------ misc
        static bool GetAutostart()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("ClipDeck") != null;
        }

        static void SetAutostart(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue("ClipDeck", "\"" + Environment.ProcessPath + "\" --hidden");
                else k.DeleteValue("ClipDeck", false);
            }
        }

        void AutoStart_Click(object sender, RoutedEventArgs e)
        {
            try { SetAutostart(AutoStartBox.IsChecked == true); }
            catch (Exception ex) { SetStatus("Не удалось изменить автозапуск: " + ex.Message); }
        }

        void Exit_Click(object sender, RoutedEventArgs e) { ExitApp(); }

        void ExitApp()
        {
            exiting = true;
            runner.Stop();
            foreach (int id in registeredIds) msg.UnregisterHotkey(id);
            msg.Dispose();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            Store.Save();
            Application.Current.Shutdown();
        }
    }
}
