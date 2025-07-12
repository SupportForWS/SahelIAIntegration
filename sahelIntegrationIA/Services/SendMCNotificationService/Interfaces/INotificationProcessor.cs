using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Services.SendMCNotificationService.Interfaces
{
    // 3. Processor to send and persist notifications
    public interface INotificationProcessor
    {
        Task ProcessEServiceRequestAsync(IEnumerable<ServiceRequest> requests);
        Task ProcessNotificatonsAsync(IEnumerable<KGACSahelOutSyncQueue> notifications);
    }

}
