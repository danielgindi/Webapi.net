﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;

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
    public abstract class AsyncRestHandler : IHttpAsyncHandler
    {
        public AsyncRestHandler(List<(string method, AsyncRestHandlerRoute route)> routes)
        {
            this.Routes = routes;
        }

        public AsyncRestHandler()
        {
        }

        public bool IsReusable { get { return true; } }

        public virtual void ProcessRequest(System.Web.HttpContext context)
        {
            throw new NotSupportedException();
        }

        public virtual IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
        {
            HttpRequest Request = context.Request;
            HttpResponse Response = context.Response;

            if (AutoDiscardDisconnectedClients && !Response.IsClientConnected)
            {
                return null;
            }

            Response.ContentEncoding = Encoding.UTF8;

            string path = Request.Url.AbsolutePath;

            if (_PathPrefix != null)
            {
                if (!path.StartsWith(_PathPrefix))
                {
                    Response.StatusCode = (int)DefaultStatusCode;
                    Response.End();
                    return null;
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

            var result = HandleRoute(context, Request.HttpMethod, path, null, cb, extraData);
            if (result != null)
                return result;

            Response.StatusCode = (int)DefaultStatusCode;
            Response.End();
            return null;
        }

        internal IAsyncResult HandleRoute(
            HttpContext context, string httpMethod, string path, ParamsCollection pathParams,
            AsyncCallback cb, object extraData)
        {
            var routes = GetRoutesForMethod(httpMethod);
            if (routes.Count == 0) return null;

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

                if (route.TargetAction != null)
                {
                    Task task;
                    bool fallbackToExHandler = true;

                    try
                    {
                        task = route.TargetAction(context, path, pathParams);
                    }
                    catch (Exception ex) when (CatchOperationCanceledExceptions || !ExceptionHelper.IsExceptionOperationCanceled(ex))
                    {
                        task = OnException(context, ExceptionHelper.GetFlattenedException(ex));
                        fallbackToExHandler = false;
                    }

                    return BeginTask(task, cb, extraData, context, fallbackToExHandler);
                }
                else if (route.ITarget != null)
                {
                    Task task;
                    bool fallbackToExHandler = true;

                    try
                    {
                        task = route.ITarget.ProcessRequest(context, path, pathParams);
                    }
                    catch (Exception ex) when (CatchOperationCanceledExceptions || !ExceptionHelper.IsExceptionOperationCanceled(ex))
                    {
                        task = OnException(context, ExceptionHelper.GetFlattenedException(ex));
                        fallbackToExHandler = false;
                    }

                    return BeginTask(task, cb, extraData, context, fallbackToExHandler);
                }
                else if (route.Handler != null)
                {
                    var subPath = path.Remove(0, match.Value.Length);
                    if (!subPath.StartsWith("/"))
                        subPath = "/" + subPath;

                    var result = route.Handler.HandleRoute(context, httpMethod, subPath, pathParams, cb, extraData);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }

        private static NoTaskAsyncResult s_NoTaskAsyncResult = new NoTaskAsyncResult();

        private IAsyncResult BeginTask(Task task, AsyncCallback callback, object state, HttpContext context, bool fallbackToExHandler)
        {
            if (task == null)
            {
                return s_NoTaskAsyncResult;
            }

            if (task.IsCompleted)
            {
                if (task.IsFaulted &&
                    fallbackToExHandler &&
                    (CatchOperationCanceledExceptions || !ExceptionHelper.IsExceptionOperationCanceled(task.Exception)))
                {
                    var exceptionTask = OnException(context, ExceptionHelper.GetFlattenedException(task.Exception));
                    return BeginTask(exceptionTask, callback, state, context, false);
                }
                else
                {
                    var resultToReturn = new TaskWrapperAsyncResult(task, state);
                    resultToReturn.ForceCompletedSynchronously();
                    callback?.Invoke(resultToReturn);
                    return resultToReturn;
                }
            }
            else
            {
                var resultToReturn = new TaskWrapperAsyncResult(task, state);

                task.ContinueWith(t => {
                    if (t.IsFaulted &&
                        fallbackToExHandler &&
                        (CatchOperationCanceledExceptions || !ExceptionHelper.IsExceptionOperationCanceled(t.Exception)))
                    {
                        task
                            .ContinueWith(async tt =>
                            {
                                await OnException(context, ExceptionHelper.GetFlattenedException(t.Exception));
                            })
                            .ContinueWith(tt =>
                            {
                                callback?.Invoke(resultToReturn);
                            });
                    }
                    else
                    {
                        callback?.Invoke(resultToReturn);
                    }
                });

                return resultToReturn;
            }
        }

        public void EndProcessRequest(IAsyncResult result)
        {
            if (result is Task)
            {
                ((Task)result).GetAwaiter().GetResult();
            }
        }

        public virtual Task OnException(HttpContext context, Exception ex)
        {
#pragma warning disable CS0612 // Type or member is obsolete
            if (OnExceptionAction != null)
            {
                return OnExceptionAction.Invoke(context, ex);
            }
#pragma warning restore CS0612 // Type or member is obsolete

            // Do not swallow the errors. Override this method to handle the exception.
            return Task.FromException(ex);
        }

        #region Variables

        private List<(string method, AsyncRestHandlerRoute route)> _AllRoutes = new List<(string method, AsyncRestHandlerRoute route)>();
        private ConcurrentDictionary<string, List<AsyncRestHandlerRoute>> _RoutesByMethodCache = new ConcurrentDictionary<string, List<AsyncRestHandlerRoute>>();

        private string _PathPrefix = null;

        #endregion

        #region Properties

        [Obsolete]
        public delegate Task ExceptionAction(HttpContext context, Exception ex);

        [Obsolete]
        public ExceptionAction OnExceptionAction { get; set; }

        public bool CatchOperationCanceledExceptions = true;


        public List<(string method, AsyncRestHandlerRoute route)> Routes
        {
            get { return _AllRoutes; }
            set { _AllRoutes = value; }
        }

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

        private List<AsyncRestHandlerRoute> GetRoutesForMethod(string httpMethod)
        {
            var routesCache = this._RoutesByMethodCache;

            if (routesCache.TryGetValue(httpMethod, out var routes))
                return routes;

            routes = _AllRoutes.Where(x => x.method == httpMethod || x.method == "").Select(x => x.route).ToList();

            routesCache[httpMethod] = routes;

            return routes;
        }

        private void ClearRoutesCache()
        {
            if (_RoutesByMethodCache.Count > 0)
                _RoutesByMethodCache.Clear();
        }

        public void AddRoute(string httpMethod, AsyncRestHandlerRoute route)
        {
            httpMethod = (httpMethod ?? "").ToUpperInvariant();
            if (httpMethod == "*")
                httpMethod = "";

            _AllRoutes.Add((method: httpMethod.ToUpperInvariant(), route: route));

            ClearRoutesCache();
        }

        public void Get(string route, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new AsyncRestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Get(Regex routePattern, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new AsyncRestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Get(string route, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new AsyncRestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Get(Regex routePattern, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new AsyncRestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Get(string route, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new AsyncRestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Get(Regex routePattern, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"GET", new AsyncRestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Post(string route, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new AsyncRestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Post(Regex routePattern, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new AsyncRestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Post(string route, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new AsyncRestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Post(Regex routePattern, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new AsyncRestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Post(string route, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new AsyncRestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Post(Regex routePattern, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"POST", new AsyncRestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Put(string route, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new AsyncRestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Put(Regex routePattern, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new AsyncRestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Put(string route, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new AsyncRestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Put(Regex routePattern, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new AsyncRestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Put(string route, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new AsyncRestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Put(Regex routePattern, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PUT", new AsyncRestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Delete(string route, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new AsyncRestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Delete(Regex routePattern, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new AsyncRestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Delete(string route, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new AsyncRestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Delete(Regex routePattern, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new AsyncRestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Delete(string route, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new AsyncRestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Delete(Regex routePattern, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"DELETE", new AsyncRestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Head(string route, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new AsyncRestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Head(Regex routePattern, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new AsyncRestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Head(string route, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new AsyncRestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Head(Regex routePattern, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new AsyncRestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Head(string route, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new AsyncRestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Head(Regex routePattern, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"HEAD", new AsyncRestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Options(string route, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new AsyncRestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Options(Regex routePattern, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new AsyncRestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Options(string route, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new AsyncRestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Options(Regex routePattern, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new AsyncRestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Options(string route, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new AsyncRestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Options(Regex routePattern, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"OPTIONS", new AsyncRestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Patch(string route, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new AsyncRestHandlerRoute(route, action, options ?? DefaultRouteOptions));
        }

        public void Patch(Regex routePattern, AsyncRestHandlerRoute.Action action, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new AsyncRestHandlerRoute(routePattern, action, options ?? DefaultRouteOptions));
        }

        public void Patch(string route, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new AsyncRestHandlerRoute(route, target, options ?? DefaultRouteOptions));
        }

        public void Patch(Regex routePattern, IAsyncRestHandlerTarget target, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new AsyncRestHandlerRoute(routePattern, target, options ?? DefaultRouteOptions));
        }

        public void Patch(string route, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new AsyncRestHandlerRoute(route, handler, options ?? DefaultHandlerRouteOptions));
        }

        public void Patch(Regex routePattern, AsyncRestHandler handler, PathToRegexUtil.PathToRegexOptions options = null)
        {
            AddRoute(@"PATCH", new AsyncRestHandlerRoute(routePattern, handler, options ?? DefaultHandlerRouteOptions));
        }

        #endregion
    }
}
