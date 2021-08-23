using System.Threading.Tasks;
#if NETCORE
using Microsoft.AspNetCore.Http;
#elif NET472
using System.Web;
#endif

namespace Webapi.net
{
    public interface IAsyncRestHandlerTarget
    {
        Task ProcessRequest(HttpContext context, string path, ParamsCollection pathParams);
    }
}
