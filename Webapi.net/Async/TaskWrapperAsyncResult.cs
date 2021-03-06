﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Webapi.net
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

        internal void ForceCompletedSynchronously()
        {
            _forceCompletedSynchronously = true;
        }
    }
}
