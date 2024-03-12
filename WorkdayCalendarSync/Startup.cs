using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pof.Logger.Splunk;
using WorkdayCalendarSync.ExtractAndUpdate;
using WorkdayCalendarSync.Services;

namespace WorkdayCalendarSync
{
    public class Startup
    {
        private ISyncingService _SyncingService;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile($"appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("workdaySecret.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public static IConfigurationRoot Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Pof packages
            services.AddSplunk(Configuration);

            SetDependency(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory, ISplunkLoggerService splunkLoggerService)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddSplunk(Configuration.GetSection("Logging"), splunkLoggerService);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                loggerFactory.AddDebug();
            }
            
            _SyncingService = app.ApplicationServices.GetService<ISyncingService>();

            applicationLifetime.ApplicationStarted.Register(Started);
            applicationLifetime.ApplicationStarted.Register(Stopped);
        }

        private static void SetDependency(IServiceCollection services)
        {
            services.AddSingleton<IGoogleCalendarAPI, GoogleCalendarAPI>();
            services.AddSingleton<IWorkdayExtractor, WorkdayExtractor>();
            services.AddSingleton<ISyncingService, SyncingService>();
            services.AddSingleton<IConfiguration>(Configuration);
        }

        private void Started()
        {
            _SyncingService.Start();
        }

        private void Stopped()
        {
            _SyncingService.StopProcessing();
        }
    }
}
