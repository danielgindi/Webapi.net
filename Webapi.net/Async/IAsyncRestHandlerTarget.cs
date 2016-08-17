using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Webapi.net
{
    public interface IAsyncRestHandlerTarget
    {
        Task ProcessRequest(HttpContext context, string path, params string[] pathParams);
    }
}
