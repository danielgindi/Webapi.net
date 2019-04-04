using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Collections;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;

namespace Webapi.net
{
    /// <summary>
    /// Supports Backbone-style with some additional sugar.
    /// 
    /// ::name_param (exact param name)
    /// :name_param (any character except /)
    /// #named_digits_param (digits)
    /// *wildcard_including_backslashes
    /// (optional_param)
    /// 
    /// Examples:
    /// 
    /// help => help
    /// search/:query => search/kiwis
    /// search/:query/p:page => search/kiwis/p7 and search/kiwis/plast
    /// search/:query/p#page => search/kiwis/p7
    /// file/*path => file/nested/folder/file.txt
    /// docs/:section(/:subsection) => docs/faq and docs/faq/installing
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

            string httpMethod = Request.HttpMethod;

            IEnumerable<RestHandlerRoute> routes;
            if (this._Routes.ContainsKey(httpMethod))
            {
                routes = this._Routes[httpMethod];
            }
            else
            {
                Response.StatusCode = (int)_DefaultStatusCode;
                Response.End();
                return;
            }

            string path = Request.Url.AbsolutePath;
            if (_PathPrefix != null && path.StartsWith(_PathPrefix))
            {
                path = path.Remove(0, _PathPrefix.Length);
            }

            // Strip query string
            int qIndex = path.IndexOf('?');
            if (qIndex > -1)
            {
                path = path.Remove(qIndex);
            }

            Match match;
            ArrayList pathParams;
            int i, len;

            foreach (RestHandlerRoute route in routes)
            {
                match = route.Pattern.Match(path);
                if (!match.Success) continue;

                pathParams = new ArrayList();
                for (i = 1, len = match.Groups.Count; i < len; i++)
                {
                    if (match.Groups[i].Captures.Count == 0) continue;
                    pathParams.Add(HttpUtility.UrlDecode(match.Groups[i].Captures[0].Value));
                }

                try
                {
                    if (route.TargetAction != null)
                    {
                        route.TargetAction(context, path, (string[])pathParams.ToArray(typeof(string)));
                        Response.End();
                        break; // FINISHED
                    }
                    else if (route.ITarget != null)
                    {
                        route.ITarget.ProcessRequest(context, path, (string[])pathParams.ToArray(typeof(string)));
                        break; // FINISHED
                    }
                    else if (route.LegacyTarget != null)
                    {
                        if (httpMethod == @"GET")
                        {
                            route.LegacyTarget.Get(Request, Response, (string[])pathParams.ToArray(typeof(string)));
                            Response.End();
                            break; // FINISHED
                        }
                        else if (httpMethod == @"POST")
                        {
                            route.LegacyTarget.Post(Request, Response, (string[])pathParams.ToArray(typeof(string)));
                            Response.End();
                            break; // FINISHED
                        }
                        else if (httpMethod == @"PUT")
                        {
                            route.LegacyTarget.Put(Request, Response, (string[])pathParams.ToArray(typeof(string)));
                            Response.End();
                            break; // FINISHED
                        }
                        else if (httpMethod == @"DELETE")
                        {
                            route.LegacyTarget.Delete(Request, Response, (string[])pathParams.ToArray(typeof(string)));
                            Response.End();
                            break; // FINISHED
                        }
                        else if (httpMethod == @"HEAD")
                        {
                            route.LegacyTarget.Head(Request, Response, (string[])pathParams.ToArray(typeof(string)));
                            Response.End();
                            break; // FINISHED
                        }
                        else if (httpMethod == @"OPTIONS")
                        {
                            route.LegacyTarget.Options(Request, Response, (string[])pathParams.ToArray(typeof(string)));
                            Response.End();
                            break; // FINISHED
                        }
                        else if (httpMethod == @"PATCH")
                        {
                            route.LegacyTarget.Patch(Request, Response, (string[])pathParams.ToArray(typeof(string)));
                            Response.End();
                            break; // FINISHED
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    // Ignore
                }
                catch (System.Exception ex)
                {
                    if (OnExceptionAction != null)
                    {
                        OnExceptionAction(context, ex);
                        Response.End();
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }

            Response.StatusCode = (int)_DefaultStatusCode;
            Response.End();
        }

        #region Variables

        private Dictionary<string, List<RestHandlerRoute>> _Routes = new Dictionary<string, List<RestHandlerRoute>>();

        private HttpStatusCode _DefaultStatusCode = HttpStatusCode.NotImplemented;

        private string _PathPrefix = @"/"
;
        #endregion

        #region Properties

        public delegate void ExceptionAction(HttpContext context, Exception ex);

        public ExceptionAction OnExceptionAction { get; set; }

        public Dictionary<string, List<RestHandlerRoute>> Routes
        {
            get { return _Routes; }
            set { _Routes = value; }
        }

        public HttpStatusCode DefaultStatusCode
        {
            get { return _DefaultStatusCode; }
            set { _DefaultStatusCode = value; }
        }

        public string PathPrefix
        {
            get { return _PathPrefix; }
            set
            {
                if (value == null || value.Length == 0)
                {
                    value = @"/";
                }
                _PathPrefix = value;
            }
        }

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
            if (_Routes.ContainsKey(httpMethod.ToUpperInvariant()))
            {
                routes = _Routes[httpMethod.ToUpperInvariant()];
            }
            else
            {
                routes = new List<RestHandlerRoute>();
                _Routes[httpMethod.ToUpperInvariant()] = routes;
            }

            routes.Add(Route);
        }

        public void Get(string route, RestHandlerRoute.Action action)
        {
            AddRoute(@"GET", new RestHandlerRoute(route, action));
        }

        public void Get(Regex routePattern, RestHandlerRoute.Action action)
        {
            AddRoute(@"GET", new RestHandlerRoute(routePattern, action));
        }

        public void Get(string route, IRestHandlerSingleTarget target)
        {
            AddRoute(@"GET", new RestHandlerRoute(route, target));
        }

        public void Get(Regex routePattern, IRestHandlerSingleTarget target)
        {
            AddRoute(@"GET", new RestHandlerRoute(routePattern, target));
        }

        public void Post(string route, RestHandlerRoute.Action action)
        {
            AddRoute(@"POST", new RestHandlerRoute(route, action));
        }

        public void Post(Regex routePattern, RestHandlerRoute.Action action)
        {
            AddRoute(@"POST", new RestHandlerRoute(routePattern, action));
        }

        public void Post(string route, IRestHandlerSingleTarget target)
        {
            AddRoute(@"POST", new RestHandlerRoute(route, target));
        }

        public void Post(Regex routePattern, IRestHandlerSingleTarget target)
        {
            AddRoute(@"POST", new RestHandlerRoute(routePattern, target));
        }

        public void Put(string route, RestHandlerRoute.Action action)
        {
            AddRoute(@"PUT", new RestHandlerRoute(route, action));
        }

        public void Put(Regex routePattern, RestHandlerRoute.Action action)
        {
            AddRoute(@"PUT", new RestHandlerRoute(routePattern, action));
        }

        public void Put(string route, IRestHandlerSingleTarget target)
        {
            AddRoute(@"PUT", new RestHandlerRoute(route, target));
        }

        public void Put(Regex routePattern, IRestHandlerSingleTarget target)
        {
            AddRoute(@"PUT", new RestHandlerRoute(routePattern, target));
        }

        public void Delete(string route, RestHandlerRoute.Action action)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(route, action));
        }

        public void Delete(Regex routePattern, RestHandlerRoute.Action action)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(routePattern, action));
        }

        public void Delete(string route, IRestHandlerSingleTarget target)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(route, target));
        }

        public void Delete(Regex routePattern, IRestHandlerSingleTarget target)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(routePattern, target));
        }

        public void Head(string route, RestHandlerRoute.Action action)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(route, action));
        }

        public void Head(Regex routePattern, RestHandlerRoute.Action action)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(routePattern, action));
        }

        public void Head(string route, IRestHandlerSingleTarget target)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(route, target));
        }

        public void Head(Regex routePattern, IRestHandlerSingleTarget target)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(routePattern, target));
        }

        public void Options(string route, RestHandlerRoute.Action action)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(route, action));
        }

        public void Options(Regex routePattern, RestHandlerRoute.Action action)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(routePattern, action));
        }

        public void Options(string route, IRestHandlerSingleTarget target)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(route, target));
        }

        public void Options(Regex routePattern, IRestHandlerSingleTarget target)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(routePattern, target));
        }

        public void Patch(string route, RestHandlerRoute.Action action)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(route, action));
        }

        public void Patch(Regex routePattern, RestHandlerRoute.Action action)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(routePattern, action));
        }

        public void Patch(string route, IRestHandlerSingleTarget target)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(route, target));
        }

        public void Patch(Regex routePattern, IRestHandlerSingleTarget target)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(routePattern, target));
        }

        #endregion

        #region AddRoute Legacy
        
        [Obsolete]
        public void Get(string route, IRestHandlerTarget target)
        {
            AddRoute(@"GET", new RestHandlerRoute(route, target));
        }

        [Obsolete]
        public void Get(Regex routePattern, IRestHandlerTarget target)
        {
            AddRoute(@"GET", new RestHandlerRoute(routePattern, target));
        }

        [Obsolete]
        public void Post(string route, IRestHandlerTarget target)
        {
            AddRoute(@"POST", new RestHandlerRoute(route, target));
        }

        [Obsolete]
        public void Post(Regex routePattern, IRestHandlerTarget target)
        {
            AddRoute(@"POST", new RestHandlerRoute(routePattern, target));
        }

        [Obsolete]
        public void Put(string route, IRestHandlerTarget target)
        {
            AddRoute(@"PUT", new RestHandlerRoute(route, target));
        }

        [Obsolete]
        public void Put(Regex routePattern, IRestHandlerTarget target)
        {
            AddRoute(@"PUT", new RestHandlerRoute(routePattern, target));
        }

        [Obsolete]
        public void Delete(string route, IRestHandlerTarget target)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(route, target));
        }

        [Obsolete]
        public void Delete(Regex routePattern, IRestHandlerTarget target)
        {
            AddRoute(@"DELETE", new RestHandlerRoute(routePattern, target));
        }

        [Obsolete]
        public void Head(string route, IRestHandlerTarget target)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(route, target));
        }

        [Obsolete]
        public void Head(Regex routePattern, IRestHandlerTarget target)
        {
            AddRoute(@"HEAD", new RestHandlerRoute(routePattern, target));
        }

        [Obsolete]
        public void Options(string route, IRestHandlerTarget target)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(route, target));
        }

        [Obsolete]
        public void Options(Regex routePattern, IRestHandlerTarget target)
        {
            AddRoute(@"OPTIONS", new RestHandlerRoute(routePattern, target));
        }

        [Obsolete]
        public void Patch(string route, IRestHandlerTarget target)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(route, target));
        }

        [Obsolete]
        public void Patch(Regex routePattern, IRestHandlerTarget target)
        {
            AddRoute(@"PATCH", new RestHandlerRoute(routePattern, target));
        }

        #endregion
    }
}
