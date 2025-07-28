using eServicesV2.Kernel.Core.Logging;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Models;

namespace sahelIntegrationIA.Jobs.SahelNotificationsJob
{


    public class SahelSenderNotificationsJob
    {
        private readonly eServicesContext _context;
        private readonly IRequestLogger _logger;
        private readonly SahelConfigurations _sahelConfigurations;
        private readonly INotificationFetcher _notificationFetcher;
        private readonly INotificationProcessor _notificationProcessor;
        private string _jobCycleId;

        public SahelSenderNotificationsJob(eServicesContext context,
                                     IRequestLogger logger,
                                     SahelConfigurations sahelConfigurations,
                                      INotificationFetcher requestFetcher,
                                     INotificationProcessor notificationProcessor)
        {
            _context = context;
            _logger = logger;
            _sahelConfigurations = sahelConfigurations;
           // _jobCycleId = new Guid().ToString();
            _notificationFetcher = requestFetcher;
            _notificationProcessor = notificationProcessor;
        }
        public async Task SendNotificationsAsync()
        {
            var notifications = await _notificationFetcher.GetPendingNotificationsAsync();
            await _notificationProcessor.ProcessNotificationsAsync(notifications);
        }


    }
}
