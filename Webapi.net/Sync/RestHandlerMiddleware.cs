using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Threading;

namespace Webapi.net
{
    /// <summary>
    /// Supports express-style routes.
    /// https://github.com/pillarjs/path-to-regexp
    /// Only exception to the defaults of express, is that here the `end=true` by default (matching to end of path)
    /// (Except for adding subrouters, where the default for the parent is `end=false`
    /// 
    /// Examples:
    /// 
    /// /foo/:bar            => "/foo/test"                 : bar => "test"
    /// /:foo/:bar           => "/test/route"               : foo => "test", bar => "route"
    /// /:foo/:bar?          => "/test/route", "/test"      : foo => "test", bar => "route" || n/a
    /// /:foo*               => "/", "/bar", "/bar/baz"     : foo => n/a, foo => "bar", foo => "bar/baz"
    /// /:foo+               => "/bar", "/bar/baz"          : foo => "bar", foo => "bar/baz"
    /// /:foo/(.*)           => "/test/route"               : foo => "test", 0 => "route"
    /// /icon-:foo(\\d+).png => "/icon-12345.png"           : foo => "12345"
    /// /(user|u)            => "/user", "/u"               : 0 => "user" || "u"
    /// 
    /// /help => help
    /// /search/:query => search/kiwis
    /// /search/:query/p:page => search/kiwis/p7 and search/kiwis/plast
    /// /search/:query/p:page(\\d+) => search/kiwis/p7
    /// /file/:path(.*) => file/nested/folder/file.txt
    /// /docs/:section/:subsection? => docs/faq and docs/faq/installing
    /// </summary>
    public abstract class RestHandlerMiddleware
    {
        private readonly RequestDelegate _Next;

        /// <summary>
        /// Relevant in nested middlewares only.
        /// If `false`, the parent middleware behave as if no route was found when this middleware has no matches.
        /// </summary>
        public bool FallbackToNextRouteInParent = true;

        public RestHandlerMiddleware(RequestDelegate next)
        {
            _Next = next;
        }

        public RestHandlerMiddleware(Dictionary<string, List<RestHandlerRoute>> routes)
        {
            this.Routes = routes;
        }

        public RestHandlerMiddleware()
        {
        }

        public async Task Invoke(HttpContext context)
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            //response.Headers[HeaderNames.ContentEncoding] = "utf8";

            string path = request.Path.Value;

            if (_PathPrefix != null)
            {
                if (!path.StartsWith(_PathPrefix))
                {
                    await _Next.Invoke(context).ConfigureAwait(false);
                    return;
                }

                path = path.Remove(0, _PathPrefix.Length);
            }

            if (path.Length > 0 && path[0] != '/')
            {
                path = "/" + path;
            }

            // Strip query string
            int qIndex = path.IndexOf('?');
            if (qIndex > -1)
            {
                path = path.Remove(qIndex);
            }

            var result = HandleRoute(context, request.Method, path, null);
            if (!result && _Next != null)
            {
                await _Next.Invoke(context).ConfigureAwait(false);
                return;
            }
        }

        internal bool HandleRoute(HttpContext context, string httpMethod, string path, ParamsCollection pathParams)
        {
            if (!this.Routes.TryGetValue(httpMethod, out var routes))
                return false;

            foreach (var route in routes)
            {
                var match = route.Pattern.Match(path);
                if (!match.Success) continue;

                if (pathParams == null)
                    pathParams = new ParamsCollection();

                for (int i = 1; i < match.Groups.Count; i++)
                {
                    var g = match.Groups[i];
                    if (!g.Success)
                        continue;

                    var key = route.PatternKeys[i - 1];
                    var val = WebUtility.UrlDecode(g.Value);

                    if (key.Name != null)
                    {
                        pathParams.Add(key.Name, val);
                    }

                    if (key.Index != null)
                    {
                        pathParams.Set(key.Index.Value, val);
                    }
                }

                try
                {
                    if (route.TargetAction != null)
                    {
                        route.TargetAction(context, path, pathParams);
                        return true;
                    }
                    else if (route.ITarget != null)
                    {
                        route.ITarget.ProcessRequest(context, path, pathParams);
                        return true;
                    }
                    else if (route.Handler != null)
                    {
                        var subPath = path.Remove(0, match.Value.Length);
                        if (!subPath.StartsWith("/"))
                            subPath = "/" + subPath;

                        var result = route.Handler.HandleRoute(context, httpMethod, subPath, pathParams);
                        if (!result && route.Handler.FallbackToNextRouteInParent)
                            continue;

                        return result;
                    }
                }
                catch (ThreadAbortException)
                {
                    // Ignore
                    return true;
                }
                catch (System.Exception ex)
                {
                    if (OnExceptionAction != null)
                    {
                        OnExceptionAction(context, ex);
                        return true;
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return false;
        }

        #region Variables

        private string _PathPrefix = null;

        #endregion

        #region Properties

        public delegate void ExceptionAction(HttpContext context, Exception ex);

        public ExceptionAction OnExceptionAction { get; set; }

        public Dictionary<string, List<RestHandlerRoute>> Routes { get; set; } = new Dictionary<string, List<RestHandlerRoute>>();

        public string PathPrefix
        {
            get { return _PathPrefix; }
            set { _PathPrefix = value; }
        }

        public static PathToRegexUtil.PathToRegexOptions DefaultRouteOptions = new PathToRegexUtil.PathToRegexOptions
        {
            Start = true,
            End = true,
            Delimiter = '/',
            Sensitive = false,
            Strict = false,
        };

        public static PathToRegexUtil.PathToRegexOptions DefaultHandlerRouteOptions = new PathToRegexUtil.PathToRegexOptions
        {
            Start = true,
            End = false,
            Delimiter = '/',
            Sensitive = false,
            Strict = false,
        };

        #endregion

        #region AddRoute

        public void AddRoute(string httpMethod, RestHandlerRoute Route)
        {
            List<RestHandlerRoute> routes;
            if (Routes.ContainsKey(httpMethod.ToUpperInvariant()))
            {
                routes = Routes[httpMethod.ToUpperInvariant()];
            }
            else
            {
                routes = new List<RestHandlerRoute>();
                Routes[httpMethod.ToUpperInvariant()] = routes;
            }

            routes.Add(Route);
        }

        public void Get(string route, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new RestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Get(Regex routePattern, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new RestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Get(string route, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new RestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Get(Regex routePattern, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new RestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Get(string route, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Get(Regex routePattern, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new RestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Post(string route, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new RestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Post(Regex routePattern, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new RestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Post(string route, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new RestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Post(Regex routePattern, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new RestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Post(string route, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Post(Regex routePattern, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new RestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Put(string route, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new RestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Put(Regex routePattern, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new RestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Put(string route, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new RestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Put(Regex routePattern, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new RestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Put(string route, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Put(Regex routePattern, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new RestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Delete(string route, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Delete(Regex routePattern, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Delete(string route, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Delete(Regex routePattern, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Delete(string route, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Delete(Regex routePattern, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Head(string route, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Head(Regex routePattern, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Head(string route, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Head(Regex routePattern, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Head(string route, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Head(Regex routePattern, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Options(string route, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Options(Regex routePattern, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Options(string route, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Options(Regex routePattern, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Options(string route, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Options(Regex routePattern, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Patch(string route, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Patch(Regex routePattern, RestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Patch(string route, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Patch(Regex routePattern, IRestHandlerSingleTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Patch(string route, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Patch(Regex routePattern, RestHandlerMiddleware handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        #endregion
    }
}
