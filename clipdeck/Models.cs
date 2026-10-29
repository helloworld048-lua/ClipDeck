using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ClipDeck
{
    public class Observable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Raise([CallerMemberName] string name = null)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }

    public class ClipItem : Observable
    {
        string _text = "";
        bool _pinned;
        DateTime _time = DateTime.Now;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get { return _text; } set { Set(ref _text, value); } }
        public bool Pinned { get { return _pinned; } set { if (Set(ref _pinned, value)) Raise("PinGlyph"); } }
        public DateTime Time { get { return _time; } set { if (Set(ref _time, value)) Raise("TimeText"); } }

        [JsonIgnore] public string PinGlyph { get { return _pinned ? "★" : "☆"; } }
        [JsonIgnore] public string TimeText { get { return _time.ToString("dd.MM HH:mm"); } }
    }

    public class Step : Observable
    {
        static readonly string[] TypeOrder = { "Key", "Text", "Wait" };
        string _type = "Key";
        string _value = "";

        public string Type { get { return _type; } set { if (Set(ref _type, value)) Raise("TypeTitle"); } }
        public string Value { get { return _value; } set { Set(ref _value, value); } }

        [JsonIgnore]
        public string TypeTitle { get { return _type == "Text" ? "Текст" : _type == "Wait" ? "Пауза" : "Клавиша"; } }

        public void NextType()
        {
            int i = Array.IndexOf(TypeOrder, _type);
            Type = TypeOrder[(i + 1) % TypeOrder.Length];
        }
    }

    public class Script : Observable
    {
        string _name = "Новый скрипт";
        string _hotkey = "";
        int _repeat = 1;
        int _stepDelay = 60;

        public string Name { get { return _name; } set { Set(ref _name, value); } }
        public string Hotkey { get { return _hotkey; } set { if (Set(ref _hotkey, value)) Raise("HotkeyText"); } }
        public int Repeat { get { return _repeat; } set { Set(ref _repeat, value); } }
        public int StepDelayMs { get { return _stepDelay; } set { Set(ref _stepDelay, value); } }
        public ObservableCollection<Step> Steps { get; set; } = new ObservableCollection<Step>();

        [JsonIgnore] public string HotkeyText { get { return string.IsNullOrEmpty(_hotkey) ? "без клавиши" : _hotkey; } }
    }

    public class Settings : Observable
    {
        string _overlayHotkey = "Ctrl+Alt+V";
        string _stopHotkey = "Ctrl+Alt+End";
        bool _autoSend, _typeInstead, _hideAfter = true, _scanMode, _paused, _topmost = true, _saveHistory;
        double _opacity = 0.97;
        int _maxItems = 200, _holdMs = 35;

        public string OverlayHotkey { get { return _overlayHotkey; } set { Set(ref _overlayHotkey, value); } }
        public string StopHotkey { get { return _stopHotkey; } set { Set(ref _stopHotkey, value); } }
        public bool AutoSend { get { return _autoSend; } set { Set(ref _autoSend, value); } }
        public bool TypeInstead { get { return _typeInstead; } set { Set(ref _typeInstead, value); } }
        public bool HideAfter { get { return _hideAfter; } set { Set(ref _hideAfter, value); } }
        public bool ScanMode { get { return _scanMode; } set { Set(ref _scanMode, value); } }
        public bool Paused { get { return _paused; } set { Set(ref _paused, value); } }
        public bool Topmost { get { return _topmost; } set { Set(ref _topmost, value); } }
        public bool SaveHistory { get { return _saveHistory; } set { Set(ref _saveHistory, value); } }
        public double Opacity { get { return _opacity; } set { Set(ref _opacity, value); } }
        public int MaxItems { get { return _maxItems; } set { Set(ref _maxItems, value); } }
        public int HoldMs { get { return _holdMs; } set { Set(ref _holdMs, value); } }

        public double WinLeft { get; set; }
        public double WinTop { get; set; }
        public double WinWidth { get; set; }
        public double WinHeight { get; set; }
    }

    public class DataFile
    {
        public List<ClipItem> Clips { get; set; } = new List<ClipItem>();
        public List<Script> Scripts { get; set; } = new List<Script>();
        public Settings Settings { get; set; } = new Settings();
    }
}
