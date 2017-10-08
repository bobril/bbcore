using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Lib.WebServer
{
    public interface ILongPollingServer
    {
        Task Handle(HttpContext context);
    }
}
