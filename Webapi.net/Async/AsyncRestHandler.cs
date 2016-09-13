using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Collections;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading.Tasks;

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
    public abstract class AsyncRestHandler : IHttpAsyncHandler
    {
        public AsyncRestHandler(Dictionary<string, List<AsyncRestHandlerRoute>> routes)
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

        public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
        {
            HttpRequest Request = context.Request;
            HttpResponse Response = context.Response;

            if (AutoDiscardDisconnectedClients && !Response.IsClientConnected)
            {
                return null;
            }

            Response.ContentEncoding = Encoding.UTF8;

            string httpMethod = Request.HttpMethod;

            IEnumerable<AsyncRestHandlerRoute> routes;
            if (this._Routes.ContainsKey(httpMethod))
            {
                routes = this._Routes[httpMethod];
            }
            else
            {
                Response.StatusCode = (int)_DefaultStatusCode;
                Response.End();
                return null;
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

            foreach (AsyncRestHandlerRoute route in routes)
            {
                match = route.Pattern.Match(path);
                if (!match.Success) continue;

                pathParams = new ArrayList();
                for (i = 1, len = match.Groups.Count; i < len; i++)
                {
                    if (match.Groups[i].Captures.Count == 0) continue;
                    pathParams.Add(HttpUtility.UrlDecode(match.Groups[i].Captures[0].Value));
                }

                if (route.TargetAction != null)
                {
                    var task = route.TargetAction(context, path, (string[])pathParams.ToArray(typeof(string)));
                    return BeginTask(task, cb, extraData, context, true);
                }
                else if (route.ITarget != null)
                {
                    var task = route.ITarget.ProcessRequest(context, path, (string[])pathParams.ToArray(typeof(string)));
                    return BeginTask(task, cb, extraData, context, true);
                }
            }

            Response.StatusCode = (int)_DefaultStatusCode;
            Response.End();
            return null;
        }

        private static NoTaskAsyncResult s_NoTaskAsyncResult = new NoTaskAsyncResult();

        private IAsyncResult BeginTask(Task task, AsyncCallback callback, object state, HttpContext context, bool fallbackToException)
        {
            if (task == null)
            {
                return s_NoTaskAsyncResult;
            }

            TaskWrapperAsyncResult resultToReturn = new TaskWrapperAsyncResult(task, state);
            
            bool actuallyCompletedSynchronously = task.IsCompleted;
            if (actuallyCompletedSynchronously)
            {
                resultToReturn.ForceCompletedSynchronously();
            }

            if (callback != null)
            {
                if (actuallyCompletedSynchronously)
                {
                    callback(resultToReturn);
                }
                else
                {
                    task.ContinueWith(t => {
                        if (t.IsFaulted && OnExceptionAction != null)
                        {
                            var exceptionTask = OnExceptionAction(context, t.Exception);
                            BeginTask(exceptionTask, callback, state, context, false);
                        }
                        else
                        {
                            callback(resultToReturn);
                        }
                    });
                }
            }

            return resultToReturn;
        }

        public void EndProcessRequest(IAsyncResult result)
        {
            if (result is Task)
            {
                ((Task)result).GetAwaiter().GetResult();
            }
        }

        #region Variables

        private Dictionary<string, List<AsyncRestHandlerRoute>> _Routes = new Dictionary<string, List<AsyncRestHandlerRoute>>();

        private HttpStatusCode _DefaultStatusCode = HttpStatusCode.NotImplemented;

        private string _PathPrefix = @"/"
;
        #endregion

        #region Properties

        public delegate Task ExceptionAction(HttpContext context, Exception ex);

        public ExceptionAction OnExceptionAction { get; set; }

        public Dictionary<string, List<AsyncRestHandlerRoute>> Routes
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

        public void AddRoute(string httpMethod, AsyncRestHandlerRoute Route)
        {
            List<AsyncRestHandlerRoute> routes;
            if (_Routes.ContainsKey(httpMethod.ToUpperInvariant()))
            {
                routes = _Routes[httpMethod.ToUpperInvariant()];
            }
            else
            {
                routes = new List<AsyncRestHandlerRoute>();
                _Routes[httpMethod.ToUpperInvariant()] = routes;
            }

            routes.Add(Route);
        }

        public void Get(string route, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"GET", new AsyncRestHandlerRoute(route, action));
        }

        public void Get(Regex routePattern, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"GET", new AsyncRestHandlerRoute(routePattern, action));
        }

        public void Get(string route, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"GET", new AsyncRestHandlerRoute(route, target));
        }

        public void Get(Regex routePattern, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"GET", new AsyncRestHandlerRoute(routePattern, target));
        }

        public void Post(string route, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"POST", new AsyncRestHandlerRoute(route, action));
        }

        public void Post(Regex routePattern, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"POST", new AsyncRestHandlerRoute(routePattern, action));
        }

        public void Post(string route, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"POST", new AsyncRestHandlerRoute(route, target));
        }

        public void Post(Regex routePattern, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"POST", new AsyncRestHandlerRoute(routePattern, target));
        }

        public void Put(string route, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"PUT", new AsyncRestHandlerRoute(route, action));
        }

        public void Put(Regex routePattern, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"PUT", new AsyncRestHandlerRoute(routePattern, action));
        }

        public void Put(string route, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"PUT", new AsyncRestHandlerRoute(route, target));
        }

        public void Put(Regex routePattern, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"PUT", new AsyncRestHandlerRoute(routePattern, target));
        }

        public void Delete(string route, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"DELETE", new AsyncRestHandlerRoute(route, action));
        }

        public void Delete(Regex routePattern, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"DELETE", new AsyncRestHandlerRoute(routePattern, action));
        }

        public void Delete(string route, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"DELETE", new AsyncRestHandlerRoute(route, target));
        }

        public void Delete(Regex routePattern, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"DELETE", new AsyncRestHandlerRoute(routePattern, target));
        }

        public void Head(string route, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"HEAD", new AsyncRestHandlerRoute(route, action));
        }

        public void Head(Regex routePattern, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"HEAD", new AsyncRestHandlerRoute(routePattern, action));
        }

        public void Head(string route, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"HEAD", new AsyncRestHandlerRoute(route, target));
        }

        public void Head(Regex routePattern, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"HEAD", new AsyncRestHandlerRoute(routePattern, target));
        }

        public void Options(string route, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"OPTIONS", new AsyncRestHandlerRoute(route, action));
        }

        public void Options(Regex routePattern, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"OPTIONS", new AsyncRestHandlerRoute(routePattern, action));
        }

        public void Options(string route, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"OPTIONS", new AsyncRestHandlerRoute(route, target));
        }

        public void Options(Regex routePattern, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"OPTIONS", new AsyncRestHandlerRoute(routePattern, target));
        }

        public void Patch(string route, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"PATCH", new AsyncRestHandlerRoute(route, action));
        }

        public void Patch(Regex routePattern, AsyncRestHandlerRoute.Action action)
        {
            AddRoute(@"PATCH", new AsyncRestHandlerRoute(routePattern, action));
        }

        public void Patch(string route, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"PATCH", new AsyncRestHandlerRoute(route, target));
        }

        public void Patch(Regex routePattern, IAsyncRestHandlerTarget target)
        {
            AddRoute(@"PATCH", new AsyncRestHandlerRoute(routePattern, target));
        }

        #endregion
    }
}
