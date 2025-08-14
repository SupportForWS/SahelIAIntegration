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
            _jobCycleId = Guid.NewGuid().ToString();
            _notificationFetcher = requestFetcher;
            _notificationProcessor = notificationProcessor;
        }
        public async Task SendNotificationsAsync()
        {
            _logger.LogInformation("{0} - Fetching pending notifications", _jobCycleId);

            var notifications = await _notificationFetcher.GetPendingNotificationsAsync();


            if (!notifications.Any())
                return;

            _logger.LogInformation("{0} - Found {1} pending notifications: {2}",
              _jobCycleId,
              notifications.Count,
              string.Join(",", notifications.Select(n => n.KGACSahelOutSyncQueueId)));

            await _notificationProcessor.ProcessNotificationsAsync(notifications, _jobCycleId);

            _logger.LogInformation("{0} - Completed processing notifications", _jobCycleId);
        }



    }
}
