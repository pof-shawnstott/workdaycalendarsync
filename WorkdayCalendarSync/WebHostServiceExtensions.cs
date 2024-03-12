using Microsoft.AspNetCore.Hosting;
using System.ServiceProcess;

namespace WorkdayCalendarSync
{
    public static class WebHostServiceExtensions
    {
        public static void RunAsCustomService(this IWebHost host)
        {
            var webHostService = new WorkdayCalendarSyncWebHostService(host);
            ServiceBase.Run(webHostService);
        }
    }
}
