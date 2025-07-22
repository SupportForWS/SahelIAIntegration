using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.OrganizationEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using sahelIntegrationIA.Enums;
using sahelIntegrationIA.Helpers;
using sahelIntegrationIA.Jobs.Shared;
using sahelIntegrationIA.Models;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;

namespace sahelIntegrationIA.Jobs.SendMCNotificationJobs
{
    public interface INotificationProcessor
    {
        Task ProcessEServiceRequestAsync(IEnumerable<ServiceRequest> requests);
        Task ProcessNotificatonsAsync(IEnumerable<KGACSahelOutSyncQueue> notifications);
    }
    public class NotificationProcessor : INotificationProcessor
    {
        private readonly eServicesContext _context;
        private readonly IRequestLogger _logger;
        //  private readonly IServiceRequestProvider _provider;
        private readonly INotificationContentFactory _factory;
        private readonly SahelNotificationClient _sahelNotificationClient;

        public NotificationProcessor(
            eServicesContext context,
            IRequestLogger logger,
            INotificationContentFactory factory,
            SahelNotificationClient client)
        {
            _context = context;
            _logger = logger;
            _factory = factory;
            _sahelNotificationClient = client;
        }

        public async Task ProcessEServiceRequestAsync(IEnumerable<ServiceRequest> requests)
        {
            var userIds = requests.Select(r => r.RequesterUserId).Distinct().ToList();
            var civilMap = await _context.Set<eServicesV2.Kernel.Domain.Entities.IdentityEntities.User>()
                .Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.CivilId);

            foreach (var sr in requests)
            {
                try
                {
                    await ProcessSingleEServiceRequestAsync(sr, civilMap[(int)sr.RequesterUserId]);
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, $"Error processing {sr.EserviceRequestNumber}");
                }
            }
        }

        public async Task ProcessNotificatonsAsync(IEnumerable<KGACSahelOutSyncQueue> notifications)
        {
            foreach (var notification in notifications)
            {
                try
                {
                    await ProcessSingleNotificationAsync(notification);
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, $"Error processing {notification.KGACSahelOutSyncQueueId}");
                }
            }
        }

        private async Task ProcessSingleNotificationAsync(KGACSahelOutSyncQueue notification)
        {
            var notificationToSend = _factory.BuildNotificationFromQueueMessage(notification);

            bool isSent = _sahelNotificationClient.PostNotification(notificationToSend, notification.KGACSahelOutSyncQueueId.ToString(), SahelOptionsTypesEnum.Business);

            await MarkNotificationAsSentAsync(notification, isSent);
        }



        private async Task ProcessSingleEServiceRequestAsync(ServiceRequest sr, string civilId)
        {
            var (msgAr, msgEn) = _factory.BuildNotificationContentForServiceRequest(sr);
            var notification = new Notification
            {
                bodyAr = msgAr,
                bodyEn = msgEn,
                isForSubscriber = "true",
                notificationType = ((int)NotificationTypeMapper.GetNotificationType((ServiceTypesEnum)sr.ServiceId)).ToString(),
                subscriberCivilId = civilId
            };

            //will send it from new service
            // bool sent = _sahelNotificationClient.PostNotification(notification, sr.EserviceRequestNumber, SahelOptionsTypesEnum.Business);

            await MarkEServiceRequestAsSentAsync(sr);
            await new InsertDataService(_context).LogNotification(notification, false, SahelTypeEnum.B.ToString());

        }


        private async Task MarkEServiceRequestAsSentAsync(ServiceRequest sr)
        {
            if (sr.ServiceId != (int)ServiceTypesEnum.OrganizationRegistrationService)
            {
                await _context.Set<ServiceRequestsDetail>()
                    .Where(d => d.EserviceRequestId == sr.EserviceRequestId)
                    .ExecuteUpdateAsync(d => d.SetProperty(x => x.MCNotificationSent, true));
            }
            else
            {
                await _context.Set<OrganizationRequests>()
                    .Where(o => o.RequestNumber == sr.EserviceRequestNumber)
                    .ExecuteUpdateAsync(o => o.SetProperty(x => x.MCNotificationSent, true));
            }
        }


        private async Task MarkNotificationAsSentAsync(KGACSahelOutSyncQueue notification, bool isSent)
        {
            var sahelOption = notification.SahelType.ToLower() switch
            {
                "i" => SahelTypeEnum.Individual.ToString(),
                "b" => SahelTypeEnum.Business.ToString(),
                _ => string.Empty
            };

            if (isSent)
            {
                //  _logger.LogInformation("SahelNotificationService - notification sent successfully: {0}", notification.KGACSahelOutSyncQueueId);

                await _context.Set<KGACSahelOutSyncQueue>()
                                  .Where(a => a.KGACSahelOutSyncQueueId == notification.KGACSahelOutSyncQueueId)
                                  .ExecuteUpdateAsync(a => a
                                  .SetProperty(b => b.Sync, true)
                                  .SetProperty(b => b.TryCount, notification.TryCount + 1)
                                  .SetProperty(b => b.DateModified, DateTime.Now));
            }
            else
            {
                //  _logger.LogInformation("SahelNotificationService - notification sent faild: {0}", notification.KGACSahelOutSyncQueueId);

                await _context.Set<KGACSahelOutSyncQueue>()
                                  .Where(a => a.KGACSahelOutSyncQueueId == notification.KGACSahelOutSyncQueueId)
                                  .ExecuteUpdateAsync(a => a
                                  .SetProperty(b => b.TryCount, notification.TryCount + 1)
                                  .SetProperty(b => b.DateModified, DateTime.Now));
            }
        }





    }
}
