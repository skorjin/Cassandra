using System.Collections.Concurrent;
using System.Windows;

namespace Cassandra.Presentation.Tests;

/// <summary>
/// WPF allows a single Application per process and binds it to its thread, so every UI test runs on one shared
/// STA thread that owns the Application and the theme resources.
/// </summary>
internal static class StaRunner
{
    private static readonly BlockingCollection<(Action Work, TaskCompletionSource Done)> Queue = [];

    static StaRunner()
    {
        var thread = new Thread(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Cassandra.Presentation;component/Themes/Overlay.xaml"),
            });

            foreach (var (work, done) in Queue.GetConsumingEnumerable())
            {
                try
                {
                    work();
                    done.SetResult();
                }
                catch (Exception ex)
                {
                    done.SetException(ex);
                }
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public static void Run(Action work)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue.Add((work, done));
        done.Task.GetAwaiter().GetResult();
    }
}
