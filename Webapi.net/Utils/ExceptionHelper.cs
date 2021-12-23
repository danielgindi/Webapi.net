using System;

namespace Webapi.net
{
    internal static class ExceptionHelper
    {
        public static bool IsExceptionOperationCanceled(Exception ex)
        {
            if (ex is OperationCanceledException) return true;

            if (ex is AggregateException aex)
            {
                while (aex.InnerExceptions.Count == 1)
                {
                    if (aex.InnerException is OperationCanceledException)
                        return true;

                    else if (aex.InnerException is AggregateException)
                        aex = aex.InnerException as AggregateException;

                    else break;
                }
            }

            return false;
        }

        public static Exception GetFlattenedException(Exception ex)
        {
            return (ex is AggregateException aex) ? aex.Flatten() : ex;
        }
    }
}
