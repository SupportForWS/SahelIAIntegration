using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.OrganizationEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using sahelIntegrationIA.Jobs.Shared;
using sahelIntegrationIA.Jobs.Shared.Constants;
using sahelIntegrationIA.Jobs.Shared.SharedNotificationService;
using sahelIntegrationIA.Jobs.Shared.SharedUserService;
using sahelIntegrationIA.Models;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;

namespace sahelIntegrationIA.Jobs.MCNotificationQueueWriterJob
{
    public interface IMCNotificationProcessor
    {
        Task ProcessEServiceRequestsAsync(List<ServiceRequest> serviceRequests, string jobCycleId);
    }

    public class MCNotificationProcessor : IMCNotificationProcessor
    {
        private readonly eServicesContext _context;
        private readonly IRequestLogger _logger;
        private readonly IMCNotificationContentFactory _notificationFactory;
        private readonly SahelNotificationClient _notificationClient;

        public MCNotificationProcessor(
            eServicesContext context,
            IRequestLogger logger,
            IMCNotificationContentFactory notificationFactory,
            SahelNotificationClient notificationClient)
        {
            _context = context;
            _logger = logger;
            _notificationFactory = notificationFactory;
            _notificationClient = notificationClient;
        }

        public async Task ProcessEServiceRequestsAsync(List<ServiceRequest> serviceRequests, string jobCycleId)
        {
            _logger.LogInformation("{0} - Starting MC action notification cycle for {1} service requests",
                jobCycleId, serviceRequests.Count);

            if (!serviceRequests.Any())
            {
                _logger.LogInformation("{0} - No service requests found. Exiting.", jobCycleId);
                return;
            }

            var civilIdByUserId = await BuildCivilIdMap(serviceRequests);

            var pendingNotifications = new List<Notification>();

            foreach (var request in serviceRequests)
            {
                string civilId = civilIdByUserId[(int)request.RequesterUserId];
                var (msgAr, msgEn) = _notificationFactory.BuildNotificationContentForServiceRequest(request);

                pendingNotifications.Add(new Notification
                {
                    bodyAr = msgAr,
                    bodyEn = msgEn,
                    isForSubscriber = "true",
                    notificationType = ((int)NotificationTypeMapper.GetNotificationType((ServiceTypesEnum)request.ServiceId)).ToString(),
                    subscriberCivilId = civilId
                });

                _logger.LogInformation("{0} - Prepared notification for RequestId: {1}, ServiceId: {2}, CivilId: {3}",
                    jobCycleId, request.EserviceRequestId, request.ServiceId, civilId);
            }

            await MarkEServiceRequestsAsSentAsync(serviceRequests);

            await NotificationWriter.InsertNotificationListAsync(_context, pendingNotifications);
        }


        private async Task MarkEServiceRequestsAsSentAsync(List<ServiceRequest> serviceRequestsList)
        {

            var nonOrgRequests = serviceRequestsList
                .Where(r => r.ServiceId != (int)ServiceTypesEnum.OrganizationRegistrationService)
                .ToList();

            var orgRequests = serviceRequestsList
                .Where(r => r.ServiceId == (int)ServiceTypesEnum.OrganizationRegistrationService)
                .ToList();

            if (nonOrgRequests.Any())
            {
                var ids = nonOrgRequests.Select(r => r.EserviceRequestId).ToList();

                await _context.Set<ServiceRequestsDetail>()
                    .Where(d => ids.Contains(d.EserviceRequestId))
                    .ExecuteUpdateAsync(d => d.SetProperty(x => x.MCNotificationSent, true));
            }

            if (orgRequests.Any())
            {
                var requestNumbers = orgRequests.Select(r => r.EserviceRequestNumber).ToList();

                await _context.Set<OrganizationRequests>()
                    .Where(o => requestNumbers.Contains(o.RequestNumber))
                    .ExecuteUpdateAsync(o => o.SetProperty(x => x.MCNotificationSent, true));
            }
        }

        private async Task<Dictionary<int, string>> BuildCivilIdMap(List<ServiceRequest> requests)
        {
            var requesterIds = requests.Select(r => (int)r.RequesterUserId).Distinct().ToList();

            return await SharedUserService.GetCivilIdMapAsync(_context, requesterIds);
        }
    }
}
