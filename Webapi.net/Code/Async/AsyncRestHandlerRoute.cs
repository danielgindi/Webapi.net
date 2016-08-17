using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using System.Web;
using System.Threading.Tasks;

namespace dg.Utilities.WebApiServices
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
            this.Pattern = RestHandlerRoute.RouteToRegex(route);
            this.Target = null;
            this.TargetAction = target;
        }

        public AsyncRestHandlerRoute(Regex pattern, Action target)
        {
            this.Pattern = pattern;
            this.Target = null;
            this.TargetAction = target;
        }

        public AsyncRestHandlerRoute(string route, IAsyncRestHandlerTarget target)
        {
            this.Pattern = RestHandlerRoute.RouteToRegex(route);
            this.Target = target;
            this.TargetAction = null;
        }

        public AsyncRestHandlerRoute(Regex pattern, IAsyncRestHandlerTarget target)
        {
            this.Pattern = pattern;
            this.Target = target;
            this.TargetAction = null;
        }

        public Regex Pattern;
        public IAsyncRestHandlerTarget Target;
        public Action TargetAction;
    }
}
