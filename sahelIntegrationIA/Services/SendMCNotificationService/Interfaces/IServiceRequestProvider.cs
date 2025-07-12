using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Services.SendMCNotificationService.Interfaces
{
    // 1. Query provider for service requests
    public interface IServiceRequestProvider
    {
        Task<List<ServiceRequest>> GetPendingeServiceRequestsAsync();

        public  Task<List<KGACSahelOutSyncQueue>> GetPendingNotificationsAsync();

    }

}
