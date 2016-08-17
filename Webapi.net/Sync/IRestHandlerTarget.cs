using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace Webapi.net
{
    /// <summary>
    /// This is for legacy support of the old library in dg.Utilities
    /// </summary>
    [Obsolete]
    public interface IRestHandlerTarget
    {
        void Get(HttpRequest Request, HttpResponse Response, params string[] PathParams);
        void Post(HttpRequest Request, HttpResponse Response, params string[] PathParams);
        void Put(HttpRequest Request, HttpResponse Response, params string[] PathParams);
        void Delete(HttpRequest Request, HttpResponse Response, params string[] PathParams);
        void Head(HttpRequest Request, HttpResponse Response, params string[] PathParams);
        void Options(HttpRequest Request, HttpResponse Response, params string[] PathParams);
        void Patch(HttpRequest Request, HttpResponse Response, params string[] PathParams);
    }
}
