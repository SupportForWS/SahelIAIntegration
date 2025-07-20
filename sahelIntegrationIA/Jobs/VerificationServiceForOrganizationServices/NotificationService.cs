using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Helpers;
using sahelIntegrationIA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;

namespace sahelIntegrationIA.Jobs.VerificationServiceForOrganizationServices
{
    public interface INotificationService
    {
        Task SendExpiredAsync(List<ServiceRequest> expired, Dictionary<int, string> civilMap);
    }

    public class NotificationService : INotificationService
    {
        private readonly IRequestLogger _log;
        private readonly eServicesContext _ctx;
        private readonly SahelConfigurations _sahelConfigurations;

        public NotificationService(IRequestLogger log, eServicesContext ctx, SahelConfigurations cfg)
        { _log = log; _ctx = ctx; _sahelConfigurations = cfg; }

        public async Task SendExpiredAsync(List<ServiceRequest> expired, Dictionary<int, string> civilMap)
        {
            _log.LogInformation("Action=SendExpiredAsync; Sending expired notifications count={Count}", expired.Count);

            foreach (var req in expired)
            {
                var civId = civilMap[(int)req.RequesterUserId];
                var msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.KmidExpiredEn, req.EserviceRequestNumber);
                var msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.KmidExpiredAr, req.EserviceRequestNumber);

                var notification = new Notification
                {
                    bodyEn = msgEn,
                    bodyAr = msgAr,
                    isForSubscriber = "true",
                    subscriberCivilId = civId,
                    notificationType = ((int)NotificationTypeMapper.GetNotificationType((ServiceTypesEnum)req.ServiceId)).ToString()
                };

                //add noti to noti table.

                _log.LogInformation("Action=SendExpiredAsync; Sending notification for request={RequestNumber}", req.EserviceRequestNumber);
                await _ctx.Set<ServiceRequestsDetail>()
                    .Where(d => d.EserviceRequestId == req.EserviceRequestId)
                    .ExecuteUpdateAsync(d => d.SetProperty(x => x.MCNotificationSent, true));
            }
        }
    }
}
