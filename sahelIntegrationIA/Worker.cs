using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using sahelIntegrationIA;
using sahelIntegrationIA.Configurations;

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
        private readonly VerificationServiceForBrokerServices _verificationServiceForBrokerServices;
        private readonly VerificationServiceForCivilIdValidation _verificationServiceForCivilIdValidation;
        private readonly InspectionAppointmentsSchedulingService _inspectionAppointmentsSchedulingService;
        private TimeSpan period;
        IBaseConfiguration _configuration;
        private readonly VerificationServiceForSignUp _verificationServiceForSignUp;

        public Worker(
            IRequestLogger logger,
            VarificationService varificationService,
            SendMcActionNotificationService sendMcActionNotificationService,
            IBaseConfiguration configuration,
            VerificationServiceForSignUp verificationServiceForSignUp,
            VerificationServiceForOrganizationServices verificationServiceForOrganizationServices,
            SahelNotificationService sahelNotificationService,
            SahelConfigurations sahelConfigurations,
            VerificationServiceForBrokerServices verificationServiceForBrokerServices,
            InspectionAppointmentsSchedulingService inspectionAppointmentsSchedulingService
            /*,VerificationServiceForCivilIdValidation verificationServiceForCivilIdValidation*/)
        {
            _logger = logger;
            this.verificationServiceForOrganizationServices = verificationServiceForOrganizationServices;
            this.varificationService = varificationService;
            this.sendMcActionNotificationService = sendMcActionNotificationService;
            _configuration = configuration;
            _verificationServiceForSignUp = verificationServiceForSignUp;
            this.sahelNotificationService = sahelNotificationService;
            _sahelConfigurations = sahelConfigurations;
            _verificationServiceForBrokerServices = verificationServiceForBrokerServices;
            _inspectionAppointmentsSchedulingService = inspectionAppointmentsSchedulingService;

            //_verificationServiceForCivilIdValidation = verificationServiceForCivilIdValidation;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            period = TimeSpan.FromSeconds(_configuration.IndividualAuthorizationSahelConfiguration.TimerIntervalInSeconds);
            using PeriodicTimer timer = new PeriodicTimer(period);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                _logger.LogInformation("New Worker running at: {time}", DateTimeOffset.Now);
                Console.WriteLine("Sahel integration with individual authorization Worker running at: {time}" + DateTimeOffset.Now);
                Console.WriteLine("ssss");
                //await varificationService.VarifyRequests();
                //await verificationServiceForOrganizationServices.CreateRequestObjectDTO();
                //await _verificationServiceForBrokerServices.CheckBrokerRequests();
                //await _verificationServiceForSignUp.CheckBrokerRequests();
                await _inspectionAppointmentsSchedulingService.ProcessInspectionAppointmentsQueue();
                //await _verificationServiceForCivilIdValidation.CreateRequestObjectDTO();
                if (_sahelConfigurations.IsSendMcActionNotificationServiceEnable)
                {
                    await sendMcActionNotificationService.SendNotification();
                }

                if (_sahelConfigurations.IsSahelNotificationServiceEnable)
                {
                    await sahelNotificationService.SendNotification();
                }


            }
        }
    }
}
