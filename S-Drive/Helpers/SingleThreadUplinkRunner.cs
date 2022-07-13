using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace S_Drive.Helpers
{
    public class SingleThreadUplinkRunner
    {
        private static SingleThreadUplinkRunner _instance;

        //singleton pattern to restrict all the actions to be executed on a single thread only.
        public static SingleThreadUplinkRunner Instance => _instance ?? (_instance = new SingleThreadUplinkRunner());

        private readonly SemaphoreSlim _semaphore;
        private readonly AutoResetEvent _newTaskRunSignal;

        private TaskCompletionSource<object> _taskCompletionSource;
        private Func<object> _func;

        private SingleThreadUplinkRunner()
        {
            _semaphore = new SemaphoreSlim(1, 1);
            _newTaskRunSignal = new AutoResetEvent(false);
            var contextThread = new Thread(ThreadLooper)
            {
                Priority = ThreadPriority.Highest
            };
            contextThread.Start();
        }

        private void ThreadLooper()
        {
            while (true)
            {
                //wait till the next task signal is received.
                _newTaskRunSignal.WaitOne();

                //next task execution signal is received.
                try
                {
                    //try execute the task and get the result
                    var result = _func.Invoke();

                    //task executed successfully, set the result
                    _taskCompletionSource.SetResult(result);
                }
                catch (Exception ex)
                {
                    //task execution threw an exception, set the exception and continue with the looper
                    _taskCompletionSource.SetException(ex);
                }

            }
        }

        public async Task<TResult> Run<TResult>(Func<TResult> func, CancellationToken cancellationToken = default(CancellationToken))
        {
            //allows only one thread to run at a time.
            await _semaphore.WaitAsync(cancellationToken);

            //thread has acquired the semaphore and entered
            try
            {
                //create new task completion source to wait for func to get executed on the context thread
                _taskCompletionSource = new TaskCompletionSource<object>();

                //set the function to be executed by the context thread
                _func = () => func();

                //signal the waiting context thread that it is time to execute the task
                _newTaskRunSignal.Set();

                //wait and return the result till the task execution is finished on the context/looper thread.
                return (TResult)await _taskCompletionSource.Task;
            }
            finally
            {
                //release the semaphore to allow other threads to acquire it.
                _semaphore.Release();
            }
        }
    }
}
