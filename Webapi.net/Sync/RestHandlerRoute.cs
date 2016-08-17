using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using System.Web;
using System.Threading.Tasks;

namespace Webapi.net
{
    public struct RestHandlerRoute
    {
        /// <param name="context"></param>
        /// <param name="path">The url path part, excluding the path prefix and the query string</param>
        /// <param name="pathParams">Parameters detected in the path, by their specified order</param>
        public delegate void Action(HttpContext context, string path, params string[] pathParams);

        /// <param name="context"></param>
        /// <param name="path">The url path part, excluding the path prefix and the query string</param>
        /// <param name="pathParams">Parameters detected in the path, by their specified order</param>
        /// <returns>A task which represents an asynchronous operation to await or null if a synchronous operation already completed.</returns>
        public delegate Task AsyncAction(HttpContext context, string path, params string[] pathParams);

        public RestHandlerRoute(string route, Action target)
        {
            this.Pattern = RouteHelper.RouteToRegex(route);
            this.TargetAction = target;
            this.ITarget = null;

#pragma warning disable CS0612 // Type or member is obsolete
            this.LegacyTarget = null;
#pragma warning restore CS0612 // Type or member is obsolete
        }

        public RestHandlerRoute(Regex pattern, Action target)
        {
            this.Pattern = pattern;
            this.TargetAction = target;
            this.ITarget = null;

#pragma warning disable CS0612 // Type or member is obsolete
            this.LegacyTarget = null;
#pragma warning restore CS0612 // Type or member is obsolete
        }

        public RestHandlerRoute(string route, IRestHandlerSingleTarget target)
        {
            this.Pattern = RouteHelper.RouteToRegex(route);
            this.TargetAction = null;
            this.ITarget = target;

#pragma warning disable CS0612 // Type or member is obsolete
            this.LegacyTarget = null;
#pragma warning restore CS0612 // Type or member is obsolete
        }

        public RestHandlerRoute(Regex pattern, IRestHandlerSingleTarget target)
        {
            this.Pattern = pattern;
            this.TargetAction = null;
            this.ITarget = target;

#pragma warning disable CS0612 // Type or member is obsolete
            this.LegacyTarget = null;
#pragma warning restore CS0612 // Type or member is obsolete
        }

        [Obsolete]
        public RestHandlerRoute(string route, IRestHandlerTarget target)
        {
            this.Pattern = RouteHelper.RouteToRegex(route);
            this.TargetAction = null;
            this.LegacyTarget = target;
            this.ITarget = null;
        }

        [Obsolete]
        public RestHandlerRoute(Regex pattern, IRestHandlerTarget target)
        {
            this.Pattern = pattern;
            this.TargetAction = null;
            this.LegacyTarget = target;
            this.ITarget = null;
        }
        
        public Regex Pattern;
        public Action TargetAction;
        public IRestHandlerSingleTarget ITarget;

        [Obsolete]
        public IRestHandlerTarget LegacyTarget;
    }
}
