﻿namespace Unosquare.PassCore.Web
{
    using Microsoft.AspNet.Builder;
    using Microsoft.AspNet.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Models;
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Represents this application's main class
    /// </summary>
    public class Startup
    {
        #region Constant Definitions

        private const string AppSettingsJsonFilename = "appsettings.json";
        private const string AppSettingsSectionName = "AppSettings";
        private const string LoggingSectionName = "Logging";
        private const string DevelopmentEnvironmentName = "Development";

        #endregion

        #region Properties

        public IConfigurationRoot Configuration { get; set; }

        #endregion

        #region Constructors and Initializers

        /// <summary>
        /// Application's entry point
        /// </summary>
        public static void Main(string[] args) => WebApplication.Run<Startup>(args);

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup" /> class.
        /// This class gets instantiatied by the Main method. The hosting environment gets provided via DI
        /// </summary>
        /// <param name="environment">The environment.</param>
        public Startup(IHostingEnvironment environment)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder().AddJsonFile(AppSettingsJsonFilename);

            if (environment.IsEnvironment(DevelopmentEnvironmentName))
            {
                // This will push telemetry data through Application Insights pipeline faster, allowing you to view results immediately.
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build().ReloadOnChanged(AppSettingsJsonFilename);
        }

        #endregion

        #region Methods

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// All arguments are provided through dependency injection
        /// </summary>
        /// <param name="services">The services.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();

            // TODO: wait for release version that has additional parameter trackChanges
            //services.Configure<AppSettings>(Configuration.GetSection(AppSettingsSectionName)); 

            services.AddSingleton<IConfigurationRoot>(sp => { return Configuration; });
            services.AddApplicationInsightsTelemetry(Configuration);
            services.AddMvc();
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// All arguments are provided through dependency injection
        /// </summary>
        /// <param name="application">The application.</param>
        /// <param name="environment">The environment.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        public void Configure(IApplicationBuilder application, IHostingEnvironment environment, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection(LoggingSectionName));
            loggerFactory.AddDebug();

            application.UseIISPlatformHandler();

            application.UseApplicationInsightsRequestTelemetry();

            application.UseApplicationInsightsExceptionTelemetry();

            application.Use(async (context, next) =>
            {
                var settings = new AppSettings();
                ConfigurationBinder.Bind(Configuration.GetSection(AppSettingsSectionName), settings);

                if (context.Request.IsHttps || Debugger.IsAttached || settings.EnableHttpsRedirect == false)
                {
                    await next();
                }
                else
                {
                    var secureRedirectUrl = $"{Uri.UriSchemeHttps}{Uri.SchemeDelimiter}{context.Request.Host}{context.Request.Path}";
                    context.Response.Redirect(secureRedirectUrl);
                }
            });

            application.UseDefaultFiles();
            application.UseStaticFiles();

            // The default route for all non-api routes is the Home controller which in turn, simply outputs the contents of the root 
            // index.html file. This makes the SPA always get back to the index route.
            application.UseMvc(options =>
            {
                options.MapRoute(name: "default", template: "{*url}", defaults: new { controller = "Home", action = "Index" });
            });
        }

        #endregion

    }
}
