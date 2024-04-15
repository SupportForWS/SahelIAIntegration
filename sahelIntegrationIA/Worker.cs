using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using sahelIntegrationIA;

namespace IndividualAuthorizationSahelWorker
{
    public class Worker : BackgroundService
    {
        private readonly IRequestLogger _logger;
        private readonly VerificationServiceForOrganizationServices verificationServiceForOrganizationServices;
        private readonly VarificationService varificationService;
        private readonly SendMcActionNotificationService sendMcActionNotificationService;
        private TimeSpan period;
        IBaseConfiguration _configuration;

        public Worker(IRequestLogger logger,VarificationService varificationService,SendMcActionNotificationService sendMcActionNotificationService, IBaseConfiguration configuration,VerificationServiceForOrganizationServices verificationServiceForOrganizationServices)
        {
            _logger = logger;
            this.verificationServiceForOrganizationServices = verificationServiceForOrganizationServices;
            this.varificationService = varificationService;
            this.sendMcActionNotificationService = sendMcActionNotificationService;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            period = TimeSpan.FromSeconds(_configuration.IndividualAuthorizationSahelConfiguration.TimerIntervalInSeconds);
            using PeriodicTimer timer = new PeriodicTimer(period);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                _logger.LogInformation("New Worker running at: {time}", DateTimeOffset.Now);
                Console.WriteLine("Sahel integration with individual authorization Worker running at: {time}" + DateTimeOffset.Now);
                //Console.WriteLine("ssss");
                await varificationService.VarifyRequests();
                await verificationServiceForOrganizationServices.CreateRequestObjectDTO();
                await sendMcActionNotificationService.SendNotification();

            }
        }
    }
}
