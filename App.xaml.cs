using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace ClipDeck
{
    public partial class App : Application
    {
        static Mutex mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            bool created;
            mutex = new Mutex(true, "ClipDeck.SingleInstance.Mutex", out created);
            if (!created)
            {
                Shutdown();
                return;
            }

            DispatcherUnhandledException += (s, args) =>
            {
                Store.Log("Unhandled: " + args.Exception);
                args.Handled = true;
            };

            Store.Load();
            var w = new MainWindow();
            MainWindow = w;
            if (!e.Args.Contains("--hidden")) w.ShowOverlay();
        }
    }
}
