using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClipDeck
{
    internal sealed class ScriptRunner
    {
        CancellationTokenSource cts;

        public bool Running { get; private set; }

        public void Stop()
        {
            CancellationTokenSource c = cts;
            if (c != null) c.Cancel();
        }

        public async Task RunAsync(Script script, Settings st)
        {
            if (Running) return;
            Running = true;
            cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            try
            {
                // The hotkey that started the script may still be held down; wait so it doesn't mix into the keys we send.
                for (int i = 0; i < 100 && InputSender.AnyModifierDown(); i++) await Task.Delay(20, ct);

                bool infinite = script.Repeat <= 0;
                for (int round = 0; (infinite || round < script.Repeat) && !ct.IsCancellationRequested; round++)
                {
                    var steps = script.Steps.ToList();
                    if (steps.Count == 0) break;
                    foreach (Step step in steps)
                    {
                        ct.ThrowIfCancellationRequested();
                        await RunStep(step, st, ct);
                        if (script.StepDelayMs > 0) await Task.Delay(script.StepDelayMs, ct);
                    }
                    await Task.Delay(1, ct);   // never spin without yielding
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                Running = false;
            }
        }

        static async Task RunStep(Step step, Settings st, CancellationToken ct)
        {
            string v = step.Value ?? "";
            if (step.Type == "Key")
            {
                if (!await InputSender.PressAsync(v, st.ScanMode, st.HoldMs))
                    throw new InvalidOperationException("Не понял клавишу «" + v + "»");
            }
            else if (step.Type == "Text")
            {
                await InputSender.TypeTextAsync(v, st.ScanMode, st.HoldMs, ct);
            }
            else if (step.Type == "Wait")
            {
                int ms;
                if (!int.TryParse(v.Trim(), out ms)) ms = 0;
                if (ms < 0) ms = 0;
                if (ms > 600000) ms = 600000;
                await Task.Delay(ms, ct);
            }
        }
    }
}
