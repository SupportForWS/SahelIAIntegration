


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

public partial class Program
{
    public static IConfigurationRoot _configuration { get; private set; }

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

           }) .ConfigureServices((services) =>
           {
               services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
           });

}