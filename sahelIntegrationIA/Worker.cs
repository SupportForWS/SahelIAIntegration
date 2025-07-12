using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using sahelIntegrationIA;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Services.SendMCNotificationService;

namespace IndividualAuthorizationSahelWorker
{
    public class Worker : BackgroundService
    {
        private readonly IRequestLogger _logger;
        private readonly VerificationServiceForOrganizationServices verificationServiceForOrganizationServices;
        private readonly VarificationService varificationService;
        private readonly SendMcActionNotificationService sendMcActionNotificationService;
        private readonly SahelNotificationService sahelNotificationService;
        private readonly SahelConfigurations _sahelConfigurations;
        private readonly OldSendMCNotificationRefactoring _oldSendMCNotificationRefactoring;
        private readonly NewSendMcActionNotificationService _newSendMcActionNotificationService;
        private TimeSpan period;
        IBaseConfiguration _configuration;
        //private readonly SendMCNotificationForSahelService sendMCNotificationForSahelService;
        //private readonly SahelConfigurations _sahelConfigurations;

        /*public Worker(IRequestLogger logger,VarificationService varificationService,SendMcActionNotificationService sendMcActionNotificationService, IBaseConfiguration configuration,VerificationServiceForOrganizationServices verificationServiceForOrganizationServices)
        {
            _logger = logger;
            this.verificationServiceForOrganizationServices = verificationServiceForOrganizationServices;
            this.varificationService = varificationService;
            this.sendMcActionNotificationService = sendMcActionNotificationService;
            _configuration = configuration;
        }*/

        public Worker(
            IRequestLogger logger,
            VarificationService varificationService,
            SendMcActionNotificationService sendMcActionNotificationService,
            IBaseConfiguration configuration,
            VerificationServiceForOrganizationServices verificationServiceForOrganizationServices,
            SahelNotificationService sahelNotificationService,
            SahelConfigurations sahelConfigurations,
            OldSendMCNotificationRefactoring oldSendMcActionNotificationService,
            NewSendMcActionNotificationService newSendMcActionNotificationService)
        {
            _logger = logger;
            this.verificationServiceForOrganizationServices = verificationServiceForOrganizationServices;
            this.varificationService = varificationService;
            this.sendMcActionNotificationService = sendMcActionNotificationService;
            _configuration = configuration;
            this.sahelNotificationService = sahelNotificationService;
            _sahelConfigurations = sahelConfigurations;
            _oldSendMCNotificationRefactoring = oldSendMcActionNotificationService;
            _newSendMcActionNotificationService = newSendMcActionNotificationService;
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
               // await varificationService.VarifyRequests();
              //  await verificationServiceForOrganizationServices.CreateRequestObjectDTO();
                //await sendMcActionNotificationService.SendNotification();

                if (_sahelConfigurations.IsSendMcActionNotificationServiceEnable)
                {
                   // await sendMcActionNotificationService.SendNotification();
                }

                if (_sahelConfigurations.IsSahelNotificationServiceEnable)
                {
                   // await sahelNotificationService.SendNotification();
                }

                await _newSendMcActionNotificationService.SendNotificationsAsync();
                await _oldSendMCNotificationRefactoring.InsertNotificationsAsync();
            }


        }
    }
}
