using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Uniso.Threading
{
    public class TaskParallelExecutor : List<Action>
    {
        public event EventHandler ProcessEvent;
        public List<Task> Tasks = new List<Task>();

        public void Start(int max, int timeOut)
        {
            if (Count == 0)
                return;

            var lcts = new LimitedConcurrencyLevelTaskScheduler(max);
            var factory = new TaskFactory(lcts);
            Exception exception = null;

            foreach (var action in this)
            {
                var task = factory.StartNew(action)
                    .ContinueWith(
                    c =>
                    {
                        var ae = c.Exception;
                        if (ae?.InnerException != null)
                            exception = ae.InnerException;
                    },
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously
                );
                Tasks.Add(task);
            }

            var sw = new Stopwatch();
            sw.Start();

            while (sw.ElapsedMilliseconds < timeOut || timeOut == 0)
            {
                if (Tasks.All(o => o.IsCompleted))
                    break;

                Thread.Sleep(200);

                ProcessEvent?.Invoke(this, EventArgs.Empty);
            }

            if (exception != null)
                throw exception;
        }
    }
}
