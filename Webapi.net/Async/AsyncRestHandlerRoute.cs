using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using System.Web;
using System.Threading.Tasks;

namespace Webapi.net
{
    public struct AsyncRestHandlerRoute
    {
        /// <param name="context"></param>
        /// <param name="path">The url path part, excluding the path prefix and the query string</param>
        /// <param name="pathParams">Parameters detected in the path, by their specified order</param>
        /// <returns>A task which represents an asynchronous operation to await or null if a synchronous operation already completed.</returns>
        public delegate Task Action(HttpContext context, string path, params string[] pathParams);

        public AsyncRestHandlerRoute(string route, Action target)
        {
            this.Pattern = RouteHelper.RouteToRegex(route);
            this.TargetAction = target;
            this.ITarget = null;
        }

        public AsyncRestHandlerRoute(Regex pattern, Action target)
        {
            this.Pattern = pattern;
            this.TargetAction = target;
            this.ITarget = null;
        }

        public AsyncRestHandlerRoute(string route, IAsyncRestHandlerTarget target)
        {
            this.Pattern = RouteHelper.RouteToRegex(route);
            this.TargetAction = null;
            this.ITarget = target;
        }

        public AsyncRestHandlerRoute(Regex pattern, IAsyncRestHandlerTarget target)
        {
            this.Pattern = pattern;
            this.TargetAction = null;
            this.ITarget = target;
        }

        public Regex Pattern;
        public Action TargetAction;
        public IAsyncRestHandlerTarget ITarget;
    }
}
