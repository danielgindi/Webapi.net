using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace Webapi.net
{
    public interface IRestHandlerSingleTarget
    {
        void ProcessRequest(HttpContext context, string path, params string[] pathParams);
    }
}
