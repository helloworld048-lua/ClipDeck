using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;

namespace ClipDeck
{
    internal static class Store
    {
        public static ObservableCollection<ClipItem> Clips = new ObservableCollection<ClipItem>();
        public static ObservableCollection<Script> Scripts = new ObservableCollection<Script>();
        public static Settings Settings = new Settings();

        static DispatcherTimer timer;

        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipDeck"); }
        }
        static string FilePath { get { return Path.Combine(Dir, "data.json"); } }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) { SeedExample(); return; }
                DataFile df = JsonSerializer.Deserialize<DataFile>(File.ReadAllText(FilePath));
                if (df == null) return;
                if (df.Settings != null) Settings = df.Settings;
                foreach (ClipItem c in df.Clips) Clips.Add(c);
                foreach (Script s in df.Scripts) Scripts.Add(s);
            }
            catch (Exception ex)
            {
                try { File.Copy(FilePath, FilePath + ".bad", true); } catch { }
                Log("Load: " + ex.Message);
            }
        }

        static void SeedExample()
        {
            var s = new Script { Name = "Пример: написать в чат", Hotkey = "", Repeat = 1, StepDelayMs = 80 };
            s.Steps.Add(new Step { Type = "Key", Value = "T" });
            s.Steps.Add(new Step { Type = "Wait", Value = "150" });
            s.Steps.Add(new Step { Type = "Text", Value = "gg wp" });
            s.Steps.Add(new Step { Type = "Key", Value = "Enter" });
            Scripts.Add(s);
        }

        public static void SaveSoon()
        {
            if (timer == null)
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                timer.Tick += delegate { timer.Stop(); Save(); };
            }
            timer.Stop();
            timer.Start();
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var df = new DataFile();
                df.Settings = Settings;
                df.Clips = Clips.Where(c => Settings.SaveHistory || c.Pinned).ToList();
                df.Scripts = Scripts.ToList();
                string json = JsonSerializer.Serialize(df, new JsonSerializerOptions { WriteIndented = true });
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, FilePath, true);
            }
            catch (Exception ex) { Log("Save: " + ex.Message); }
        }

        public static void Log(string text)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.AppendAllText(Path.Combine(Dir, "error.log"), DateTime.Now.ToString("s") + " " + text + Environment.NewLine);
            }
            catch { }
        }
    }
}
