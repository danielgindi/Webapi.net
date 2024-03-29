﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;
using System.Web;

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
    public abstract class RestHandler : IHttpHandler
    {
        public RestHandler(Dictionary<string, List<RestHandlerRoute>> routes)
        {
            this.Routes = routes;
        }

        public RestHandler()
        {
        }

        public bool IsReusable { get { return true; } }

        public virtual void ProcessRequest(System.Web.HttpContext context)
        {
            HttpRequest Request = context.Request;
            HttpResponse Response = context.Response;

            if (AutoDiscardDisconnectedClients && !Response.IsClientConnected)
            {
                return;
            }

            Response.ContentEncoding = Encoding.UTF8;

            string path = Request.Url.AbsolutePath;

            if (_PathPrefix != null)
            {
                if (!path.StartsWith(_PathPrefix))
                {
                    Response.StatusCode = (int)DefaultStatusCode;
                    Response.End();
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

            var result = HandleRoute(context, Request.HttpMethod, path, null);
            if (result)
                return;

            Response.StatusCode = (int)DefaultStatusCode;
            Response.End();
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
                    var val = HttpUtility.UrlDecode(g.Value);

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

                        if (route.Handler.HandleRoute(context, httpMethod, subPath, pathParams))
                            return true;
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

        public HttpStatusCode DefaultStatusCode { get; set; } = HttpStatusCode.NotImplemented;

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

        /// <summary>
        /// Should disconnected clients be discarded automatically?
        /// 
        /// When a request is aborted, the chunks that were already sent might come in as a request.
        /// And then files may be missing etc.
        /// </summary>
        public bool AutoDiscardDisconnectedClients { get; set; }

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

        public void Get(string route, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Get(Regex routePattern, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
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

        public void Post(string route, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Post(Regex routePattern, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
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

        public void Put(string route, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Put(Regex routePattern, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
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

        public void Delete(string route, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Delete(Regex routePattern, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
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

        public void Head(string route, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Head(Regex routePattern, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
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

        public void Options(string route, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Options(Regex routePattern, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
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

        public void Patch(string route, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Patch(Regex routePattern, RestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        #endregion
    }
}
