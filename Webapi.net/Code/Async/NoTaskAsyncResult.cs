using System;
using System.Threading;
using System.Threading.Tasks;

namespace dg.Utilities.WebApiServices
{
    internal sealed class NoTaskAsyncResult : IAsyncResult
    {
        internal NoTaskAsyncResult()
        {
        }

        public object AsyncState
        {
            get;
            private set;
        }
        
        public bool CompletedSynchronously
        {
            get { return true; }
        }

        public bool IsCompleted
        {
            get { return true; }
        }
        
        WaitHandle IAsyncResult.AsyncWaitHandle
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}
