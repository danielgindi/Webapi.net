#if NETCORE
using Microsoft.AspNetCore.Http;
#elif NET472
using System.Web;
#endif

namespace Webapi.net
{
    public interface IRestHandlerSingleTarget
    {
        void ProcessRequest(HttpContext context, string path, ParamsCollection pathParams);
    }
}
