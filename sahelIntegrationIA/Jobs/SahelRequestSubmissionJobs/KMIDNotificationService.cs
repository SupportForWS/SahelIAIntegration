using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Jobs.Shared.Constants;
using sahelIntegrationIA.Jobs.Shared.SharedNotificationService;
using sahelIntegrationIA.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;

namespace sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs
{
    public interface IKMIDNotificationService
    {
        Task NotifyExpiredKmidRequestsAsync(List<ServiceRequest> expiredRequests, Dictionary<int, string> civilIdLookup);
    }

    public class KMIDNotificationService : IKMIDNotificationService
    {
        private readonly IRequestLogger _log;
        private readonly eServicesContext _context;
        private readonly SahelConfigurations _sahelConfigurations;

        public KMIDNotificationService(IRequestLogger log, eServicesContext ctx, SahelConfigurations cfg)
        { _log = log; _context = ctx; _sahelConfigurations = cfg; }


        public async Task NotifyExpiredKmidRequestsAsync(List<ServiceRequest> expiredRequests, Dictionary<int, string> civilIdLookup)
        {
            if (!expiredRequests.Any()) return;

            var notifications = new List<Notification>();

            foreach (var request in expiredRequests)
            {
                notifications.Add(new Notification
                {
                    bodyAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.KmidExpiredAr, request.EserviceRequestNumber),
                    bodyEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.KmidExpiredEn, request.EserviceRequestNumber),
                    isForSubscriber = "true",
                    subscriberCivilId = civilIdLookup[(int)request.RequesterUserId],
                    notificationType = ((int)NotificationTypeMapper.GetNotificationType((ServiceTypesEnum)request.ServiceId)).ToString()
                });
            }

            var requestIds = expiredRequests.Select(x => x.EserviceRequestId).ToList();
            await _context.Set<ServiceRequestsDetail>()
                .Where(a => requestIds.Contains(a.EserviceRequestId))
                              .ExecuteUpdateAsync(a => a.SetProperty(b => b.MCNotificationSent, true));

            await NotificationWriter.InsertNotificationListAsync(_context, notifications);
        }

    }
}
