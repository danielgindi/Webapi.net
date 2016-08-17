using System;
using System.Threading;
using System.Threading.Tasks;

namespace dg.Utilities.WebApiServices
{
    /// <summary>
    /// Borrowed from Microsoft, used to wrap an async result for a task
    /// </summary>
    internal sealed class TaskWrapperAsyncResult : IAsyncResult
    {
        private bool _forceCompletedSynchronously;

        internal TaskWrapperAsyncResult(Task task, object asyncState)
        {
            Task = task;
            AsyncState = asyncState;
        }

        public object AsyncState
        {
            get;
            private set;
        }

        public WaitHandle AsyncWaitHandle
        {
            get { return ((IAsyncResult)Task).AsyncWaitHandle; }
        }

        public bool CompletedSynchronously
        {
            get { return _forceCompletedSynchronously || ((IAsyncResult)Task).CompletedSynchronously; }
        }

        public bool IsCompleted
        {
            get { return ((IAsyncResult)Task).IsCompleted; }
        }

        internal Task Task
        {
            get;
            private set;
        }

        WaitHandle IAsyncResult.AsyncWaitHandle
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        internal void ForceCompletedSynchronously()
        {
            _forceCompletedSynchronously = true;
        }
    }
}
