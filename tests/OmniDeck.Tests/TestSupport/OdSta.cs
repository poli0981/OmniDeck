using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace OmniDeck.Tests.TestSupport;

/// <summary>
/// Runs a test body on its own STA thread with a WPF dispatcher, as the shell does: views can be
/// created there, and every await resumes on that thread.
/// </summary>
public static class OdSta
{
    public static void Run(Func<Task> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var frame = new DispatcherFrame();
            _ = RunBodyAsync();
            Dispatcher.PushFrame(frame);
            dispatcher.InvokeShutdown();

            async Task RunBodyAsync()
            {
                try
                {
                    await body();
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    frame.Continue = false;
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
