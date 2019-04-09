﻿using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using System.Threading.Tasks;

namespace Webapi.net
{
    public struct AsyncRestHandlerRoute
    {
        private static PathToRegexUtil.PathToRegexOptions DEFAULT_PATH_TO_REGEX_OPTIONS = new PathToRegexUtil.PathToRegexOptions
        {
            Start = true,
            End = true,
            Delimiter = '/',
            Sensitive = false,
            Strict = false,
        };

        /// <param name="context"></param>
        /// <param name="path">The url path part, excluding the path prefix and the query string</param>
        /// <param name="pathParams">Parameters detected in the path, by their specified order</param>
        /// <returns>A task which represents an asynchronous operation to await or null if a synchronous operation already completed.</returns>
        public delegate Task Action(HttpContext context, string path, ParamsCollection pathParams);

        public AsyncRestHandlerRoute(string route, Action target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            this.PatternKeys = new List<PathToRegexUtil.Token>();
            this.Pattern = PathToRegexUtil.PathToRegex(
                route.StartsWith("/") ? route : ("/" + route),
                ref this.PatternKeys,
                options ?? DEFAULT_PATH_TO_REGEX_OPTIONS);

            this.TargetAction = target;
            this.ITarget = null;
        }

        public AsyncRestHandlerRoute(Regex pattern, Action target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            this.PatternKeys = new List<PathToRegexUtil.Token>();
            this.Pattern = PathToRegexUtil.PathToRegex(
                pattern,
                ref this.PatternKeys,
                options ?? DEFAULT_PATH_TO_REGEX_OPTIONS);

            this.TargetAction = target;
            this.ITarget = null;
        }

        public AsyncRestHandlerRoute(string route, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            this.PatternKeys = new List<PathToRegexUtil.Token>();
            this.Pattern = PathToRegexUtil.PathToRegex(
                route.StartsWith("/") ? route : ("/" + route), 
                ref this.PatternKeys,
                options ?? DEFAULT_PATH_TO_REGEX_OPTIONS);

            this.TargetAction = null;
            this.ITarget = target;
        }

        public AsyncRestHandlerRoute(Regex pattern, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            this.PatternKeys = new List<PathToRegexUtil.Token>();
            this.Pattern = PathToRegexUtil.PathToRegex(
                pattern, 
                ref this.PatternKeys,
                options ?? DEFAULT_PATH_TO_REGEX_OPTIONS);

            this.TargetAction = null;
            this.ITarget = target;
        }

        public Regex Pattern;
        public List<PathToRegexUtil.Token> PatternKeys;
        public Action TargetAction;
        public IAsyncRestHandlerTarget ITarget;
    }
}
