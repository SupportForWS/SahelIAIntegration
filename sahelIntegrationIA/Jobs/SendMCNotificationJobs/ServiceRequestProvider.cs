using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Enums;
using sahelIntegrationIA.Helpers;
using sahelIntegrationIA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Jobs.SendMCNotificationJobs
{
    public interface IServiceRequestProvider
    {
        Task<List<ServiceRequest>> GetPendingeServiceRequestsAsync();

        public Task<List<KGACSahelOutSyncQueue>> GetPendingNotificationsAsync();

    }

    public class ServiceRequestProvider : IServiceRequestProvider
    {
        private readonly eServicesContext _context;
        private readonly IRequestLogger _logger;
        private readonly SahelConfigurations _sahelConfigurations;
        private string _jobCycleId;

        public ServiceRequestProvider(
            eServicesContext context,
            IRequestLogger logger,
            SahelConfigurations sahelConfigurations)
        {
            _context = context;
            _logger = logger;
            _jobCycleId = Guid.NewGuid().ToString();
            _sahelConfigurations = sahelConfigurations;
        }

        public async Task<List<ServiceRequest>> GetPendingeServiceRequestsAsync()
        {
            _jobCycleId = Guid.NewGuid().ToString();
            var statuses = ServiceConstants.BuildStatusesForSendMCService();
            var services = ServiceConstants.BuildServiceIds();

            LogStart(statuses, services);
            var list = await FetchServiceRequestsAsync(statuses, services);
            list.AddRange(await FetchOrganizationRequestsAsync(statuses));
            LogEnd(list);
            return list;
        }
        public async Task<List<KGACSahelOutSyncQueue>> GetPendingNotificationsAsync()
        {
            //_logger.LogInformation("SahelNotificationService - start Notifications For Sahel");

            var notificationList = await FetchNotificationAsync();

            //string log = JsonConvert.SerializeObject(notificationList, Formatting.None,
            //            new JsonSerializerSettings()
            //            {
            //                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            //            });

            //   _logger.LogInformation(message: $"SahelNotificationService - start Notifications For Sahel {0}", log);


            return notificationList;
        }


        private void LogStart(string[] statuses, int[] services)
        {
            var statusJson = JsonConvert.SerializeObject(statuses);
            var serviceJson = JsonConvert.SerializeObject(services);
            _logger.LogInformation("{2} - Fetching pending requests - statuses: {0}, services: {1}",
                statusJson, serviceJson, _jobCycleId);
        }

        private async Task<List<ServiceRequest>> FetchServiceRequestsAsync(string[] statuses, int[] services)
        {
            return await _context.Set<ServiceRequest>()
                .Include(r => r.ServiceRequestsDetail)
                .Where(r => statuses.Contains(r.StateId)
                            && r.RequestSource == "eServices"//todo add enum
                            && services.Contains((int)r.ServiceId.Value)
                            && r.ServiceRequestsDetail.ReadyForSahelSubmission == "0"
                            && !r.ServiceRequestsDetail.MCNotificationSent.Value)
                .AsNoTracking()
                .ToListAsync();
        }

        private async Task<List<ServiceRequest>> FetchOrganizationRequestsAsync(string[] statuses)
        {
            return await _context.Set<ServiceRequest>()
                .Include(r => r.OrganizationRequest)
                .Where(r => statuses.Contains(r.OrganizationRequest.StateId)
                            && r.RequestSource == "eServices"
                            && r.ServiceId == (int)ServiceTypesEnum.OrganizationRegistrationService
                            && r.OrganizationRequest.ReadyForSahelSubmission == "0"
                            && !r.OrganizationRequest.MCNotificationSent.Value)
                .AsNoTracking()
                .ToListAsync();
        }

        private async Task<List<KGACSahelOutSyncQueue>> FetchNotificationAsync()
        {
            return await _context.Set<KGACSahelOutSyncQueue>()
                                .Where(x => (x.Source == NotificationSource.MC.ToString()
                                                   && x.Sync.Value != true
                                                   && x.TryCount.Value <= _sahelConfigurations.TryCountForMCNotification)
                                            || (x.Source == NotificationSource.eService.ToString()
                                                && x.Sync != true
                                                && x.TryCount.Value <= _sahelConfigurations.TryCountForeServiceNotification))
                                .AsNoTracking()
                                .ToListAsync();
        }

        private void LogEnd(List<ServiceRequest> list)
        {
            var log = JsonConvert.SerializeObject(list,
                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            _logger.LogInformation("{1} - Retrieved {0} requests", list.Count, _jobCycleId);
        }
    }
}
