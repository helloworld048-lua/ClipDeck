using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace ClipDeck
{
    public partial class App : Application
    {
        static Mutex mutex;

        static void Fail(string where, Exception ex)
        {
            Store.Log(where + ": " + ex);
            try
            {
                MessageBox.Show("ClipDeck: ошибка (" + where + ")\n\n" + ex.Message + "\n\nПодробности: %AppData%\\ClipDeck\\error.log",
                                "ClipDeck", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (s, args) => { Fail("UI", args.Exception); args.Handled = true; };
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                Fail("Fatal", args.ExceptionObject as Exception ?? new Exception("unknown"));

            bool created;
            mutex = new Mutex(true, "ClipDeck.SingleInstance.Mutex", out created);
            if (!created) { Shutdown(); return; }

            try
            {
                Store.Load();
                var w = new MainWindow();
                MainWindow = w;
                if (!e.Args.Contains("--hidden")) w.ShowOverlay();
            }
            catch (Exception ex)
            {
                Fail("start", ex);
                Shutdown();
            }
        }
    }
}
