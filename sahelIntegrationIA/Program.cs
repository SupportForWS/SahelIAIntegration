using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Core.Persistence;
using eServicesV2.Kernel.Data.Contexts;
using eServicesV2.Kernel.Infrastructure.Logging.Logging.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Serilog;
using sahelIntegrationIA.Models;
using RequestLogger = sahelIntegrationIA.Models.RequestLogger;
using eServicesContext = sahelIntegrationIA.Models.eServicesContext;
using sahelIntegrationIA.Configurations;
using IndividualAuthorizationSahelWorker;
using sahelIntegrationIA;

public partial class Program
{
    public static IConfigurationRoot _configuration { get; private set; }
    public static BaseConfiguration _baseConfiguration { get; private set; }
    public static SahelConfigurations _SahelConfiguration { get; private set; }

    static async Task Main(string[] args)
    {
        using IHost host = CreateHostBuilder(args).Build();

        host.Run();
    }


    static IHostBuilder CreateHostBuilder(string[] args) =>
              Host.CreateDefaultBuilder(args)
                .UseWindowsService(config =>
                {
                    config.ServiceName = "IA Sahel Worker Service";
                })
               .ConfigureAppConfiguration((hostingContext, configuration) =>
               {
                   configuration.Sources.Clear();

                   configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.shared.json"), optional: true, reloadOnChange: true);
                   configuration.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.{Environment}.json"), optional: true);
                   configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                   configuration.AddEnvironmentVariables();
                   _configuration = configuration.Build();

                   _baseConfiguration = _configuration.Get<BaseConfiguration>();
                   _SahelConfiguration = _configuration.Get<SahelConfigurations>();

                   configuration.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);

               })
               .UseSerilog((hostContext, services, configurations) =>
                {
                    configurations.ReadFrom.Configuration(_configuration);//check
                })
               .ConfigureServices((services) =>
               {
                   services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                   //services.AddHttpContextAccessor();
                   services.AddSingleton<IBaseConfiguration>(a => _baseConfiguration);
                   services.AddSingleton<SahelConfigurations>(a => _SahelConfiguration);

                   //services.AddSingleton<ILogger<Worker>, Logger<Worker>>();
                   services.AddSingleton<IRequestLogger, RequestLogger>();
                 services
               .AddDbContext<eServicesContext>(options =>
                    options.UseSqlServer(_baseConfiguration.ConnectionStrings.Default,
                    sqlServerOptionsAction: sqlOptions =>
                       {
                           sqlOptions.CommandTimeout(60);
                           sqlOptions.EnableRetryOnFailure();
                       }).EnableSensitiveDataLogging(true),
               ServiceLifetime.Singleton);

                   services.AddLocalization(o =>
                   {
                       // We will put our translations in a folder called Resources
                       o.ResourcesPath = "Resources";
                   });
                   services.AddSingleton<IStringLocalizerFactory, eServicesV2.Kernel.Host.API.Configurations.JsonStringLocalizerFactory>();
                   services.AddSingleton<IStringLocalizer, eServicesV2.Kernel.Host.API.Configurations.JsonStringLocalizer>();


                   services.AddSingleton<IDapper, eServicesV2.Kernel.Infrastructure.Persistence.Dapper.Dapper>();
                   services.AddSingleton<VarificationService>();
                   services.AddSingleton<SendMcActionNotificationService>();
                   services.AddSingleton<SendMCNotificationForSahelService>();
                   services.AddSingleton<VerificationServiceForOrganizationServices>();

                   services.AddHostedService<Worker>();
               });



}