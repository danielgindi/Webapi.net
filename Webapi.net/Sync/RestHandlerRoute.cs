using System.Collections.Generic;
using System.Text.RegularExpressions;
#if NETCORE || NETSTANDARD
using Microsoft.AspNetCore.Http;
#elif NET472
using System.Web;
#endif

namespace Webapi.net
{
    public struct RestHandlerRoute
    {
        private static PathToRegexUtil.PathToRegexOptions DEFAULT_PATH_TO_REGEX_OPTIONS = new PathToRegexUtil.PathToRegexOptions
        {
            Start = true,
            End = true,
            Delimiter = '/',
            Sensitive = false,
            Strict = false,
        };

        private static PathToRegexUtil.PathToRegexOptions DEFAULT_HANDLER_PATH_TO_REGEX_OPTIONS = new PathToRegexUtil.PathToRegexOptions
        {
            Start = true,
            End = false,
            Delimiter = '/',
            Sensitive = false,
            Strict = false,
        };

        /// <param name="context"></param>
        /// <param name="path">The url path part, excluding the path prefix and the query string</param>
        /// <param name="pathParams">Parameters detected in the path, either by indexes or keys</param>
        public delegate void Action(HttpContext context, string path, ParamsCollection pathParams);

        public RestHandlerRoute(string route, Action target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            this.PatternKeys = new List<PathToRegexUtil.Token>();
            this.Pattern = PathToRegexUtil.PathToRegex(
                route.StartsWith("/") ? route : ("/" + route), 
                ref this.PatternKeys, 
                options ?? DEFAULT_PATH_TO_REGEX_OPTIONS);

            this.TargetAction = target;
            this.ITarget = null;
            this.Handler = null;
        }

        public RestHandlerRoute(Regex pattern, Action target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            this.PatternKeys = new List<PathToRegexUtil.Token>();
            this.Pattern = PathToRegexUtil.PathToRegex(
                pattern,
                ref this.PatternKeys,
                options ?? DEFAULT_PATH_TO_REGEX_OPTIONS);

            this.TargetAction = target;
            this.ITarget = null;
            this.Handler = null;
        }

        public RestHandlerRoute(string route, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            this.PatternKeys = new List<PathToRegexUtil.Token>();
            this.Pattern = PathToRegexUtil.PathToRegex(
                route.StartsWith("/") ? route : ("/" + route), 
                ref this.PatternKeys,
                options ?? DEFAULT_PATH_TO_REGEX_OPTIONS);

            this.TargetAction = null;
            this.ITarget = target;
            this.Handler = null;
        }

        public RestHandlerRoute(Regex pattern, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            this.PatternKeys = new List<PathToRegexUtil.Token>();
            this.Pattern = PathToRegexUtil.PathToRegex(
                pattern,
                ref this.PatternKeys,
                options ?? DEFAULT_PATH_TO_REGEX_OPTIONS);

            this.TargetAction = null;
            this.ITarget = target;
            this.Handler = null;
        }

#if NETCORE || NETSTANDARD
        public RestHandlerRoute(string route, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
#elif NET472
        public RestHandlerRoute(string route, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
#endif
        {
            this.PatternKeys = new List<PathToRegexUtil.Token>();
            this.Pattern = PathToRegexUtil.PathToRegex(
                route.StartsWith("/") ? route : ("/" + route),
                ref this.PatternKeys,
                options ?? DEFAULT_HANDLER_PATH_TO_REGEX_OPTIONS);

            this.TargetAction = null;
            this.ITarget = null;
            this.Handler = handler;
        }

#if NETCORE || NETSTANDARD
        public RestHandlerRoute(Regex pattern, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
#elif NET472
        public RestHandlerRoute(Regex pattern, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
#endif
        {
            this.PatternKeys = new List<PathToRegexUtil.Token>();
            this.Pattern = PathToRegexUtil.PathToRegex(
                pattern,
                ref this.PatternKeys,
                options ?? DEFAULT_HANDLER_PATH_TO_REGEX_OPTIONS);

            this.TargetAction = null;
            this.ITarget = null;
            this.Handler = handler;
        }

        public Regex Pattern;
        public List<PathToRegexUtil.Token> PatternKeys;
        public Action TargetAction;
        public IRestHandlerSingleTarget ITarget;
#if NETCORE || NETSTANDARD
        public RestHandlerMiddleware Handler;
#elif NET472
        public RestHandler Handler;
#endif
    }
}
