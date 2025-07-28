using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using Microsoft.EntityFrameworkCore;
using sahelIntegrationIA.Jobs.Shared;
using sahelIntegrationIA.Models;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;

namespace sahelIntegrationIA.Jobs.SahelNotificationsJob
{
    public interface INotificationProcessor
    {
        Task ProcessNotificationsAsync(List<KGACSahelOutSyncQueue> notificationQueueItems);
    }
    public class NotificationProcessor: INotificationProcessor
    {
        private readonly eServicesContext _context;
        private readonly IRequestLogger _logger;
        private readonly SahelNotificationClient _notificationClient;

        public NotificationProcessor(
            eServicesContext context,
            IRequestLogger logger,
             SahelNotificationClient notificationClient)
        {
            _context = context;
            _logger = logger;
            _notificationClient = notificationClient;
        }


        public async Task ProcessNotificationsAsync(List<KGACSahelOutSyncQueue> notificationQueueItems)
        {
            var successfullySentNotifications = new List<Notification>();
            var successfullySentIds = new List<int>();

            foreach (var queueItem in notificationQueueItems)
            {
                try
                {
                    var notification = _notificationClient.BuildNotificationFromQueueMessage(queueItem);
                    bool isSent = _notificationClient.PostNotification(notification,
                        queueItem.KGACSahelOutSyncQueueId.ToString(),
                        SahelOptionsTypesEnum.Business);

                    if (isSent)
                    {
                        successfullySentNotifications.Add(notification);
                        successfullySentIds.Add(queueItem.KGACSahelOutSyncQueueId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Error processing notification queue item {0}", queueItem.KGACSahelOutSyncQueueId);
                }
            }

            await MarkQueueItemsAsProcessedAsync(notificationQueueItems, successfullySentIds);
        }

        private async Task MarkQueueItemsAsProcessedAsync(List<KGACSahelOutSyncQueue> queueList, List<int> successfullySentIds)
        {
            var currentTime = DateTime.Now;

            var sentItems = queueList.Where(q => successfullySentIds.Contains(q.KGACSahelOutSyncQueueId)).ToList();
            var failedItems = queueList.Except(sentItems).ToList();

            if (sentItems.Any())
            {
                var sentIds = sentItems.Select(q => q.KGACSahelOutSyncQueueId).ToList();

                await _context.Set<KGACSahelOutSyncQueue>()
                    .Where(q => sentIds.Contains(q.KGACSahelOutSyncQueueId))
                    .ExecuteUpdateAsync(q => q
                        .SetProperty(x => x.Sync, true)
                        .SetProperty(x => x.TryCount, x => x.TryCount + 1)
                        .SetProperty(x => x.DateModified, currentTime));
            }

            if (failedItems.Any())
            {
                var failedIds = failedItems.Select(q => q.KGACSahelOutSyncQueueId).ToList();

                await _context.Set<KGACSahelOutSyncQueue>()
                    .Where(q => failedIds.Contains(q.KGACSahelOutSyncQueueId))
                    .ExecuteUpdateAsync(q => q
                        .SetProperty(x => x.TryCount, x => x.TryCount + 1)
                        .SetProperty(x => x.DateModified, currentTime));
            }
        }


    }
}
