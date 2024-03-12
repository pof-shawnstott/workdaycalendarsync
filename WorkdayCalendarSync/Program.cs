using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using WorkdayCalendarSync.Settings;

namespace WorkdayCalendarSync
{
    public class Program
    {
        public static bool isService;

        public static void Main(string[] args)
        {
            isService = !(Debugger.IsAttached || args.Contains("--console"));
            var pathToContentRoot = Directory.GetCurrentDirectory();

            if (isService)
            {
                var pathToExe = Process.GetCurrentProcess().MainModule.FileName;
                pathToContentRoot = Path.GetDirectoryName(pathToExe);
            }

            var environmentValue = GetEnvironmentValue(pathToContentRoot);
            HostSettings hostSettings = GetHostSettings(pathToContentRoot, environmentValue);

            var host = WebHost.CreateDefaultBuilder(args)
                .UseContentRoot(pathToContentRoot)
                .UseStartup<Startup>()
                .UseHttpSys(options =>
                {
                    // The following options are set to default values.
                    options.Authentication.Schemes = AuthenticationSchemes.None;
                    options.Authentication.AllowAnonymous = true;
                    options.MaxConnections = null;
                    options.MaxRequestBodySize = 30000000;
                })
                .UseUrls($"{hostSettings.BaseUrl}:{hostSettings.PortNumber}")
                .Build();

            if (isService)
            {
                host.RunAsCustomService();
            }
            else
            {
                host.Run();
            }
        }

        private static string GetEnvironmentValue(string pathToContentRoot)
        {
            var envConfig = new ConfigurationBuilder()
                .SetBasePath(pathToContentRoot)
                .AddJsonFile("envsettings.json")
                .Build();

            var environmentValue = envConfig.GetValue<string>("ASPNETCORE_ENVIRONMENT");
            return environmentValue;
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args).UseStartup<Startup>();

        private static HostSettings GetHostSettings(string pathToContentRoot, string enviromentValue)
        {
            var hostSettings = new HostSettings();
            var configuration = new ConfigurationBuilder()
                .SetBasePath(pathToContentRoot)
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{enviromentValue}.json", true, true)
                .AddEnvironmentVariables()
                .Build();

            configuration.GetSection("host").Bind(hostSettings);

            return hostSettings;
        }
    }
}
