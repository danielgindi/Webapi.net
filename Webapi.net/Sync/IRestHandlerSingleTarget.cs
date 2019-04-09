using System.Web;

namespace Webapi.net
{
    public interface IRestHandlerSingleTarget
    {
        void ProcessRequest(HttpContext context, string path, ParamsCollection pathParams);
    }
}
