using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;

namespace sahelIntegrationIA.Services.SendMCNotificationService.Interfaces
{
    // 2. Factory for notification content
    public interface INotificationContentFactory
    {
        (string msgAr, string msgEn) BuildNotificationContentForServiceRequest(ServiceRequest request);
        public Notification BuildNotificationFromQueueMessage(KGACSahelOutSyncQueue notification);
     }

}
