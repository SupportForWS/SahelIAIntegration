using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Jobs.SahelRequestSubmissionJob;
using sahelIntegrationIA.Jobs.Shared.Constants;
using sahelIntegrationIA.Jobs.Shared.SharedNotificationService;
using sahelIntegrationIA.Jobs.Shared.SharedUserService;
using sahelIntegrationIA.Models;
using Notification = eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels.Notification;

namespace sahelIntegrationIA.Jobs.SahelRequestSubmissionJobs
{
    /// <summary>
    /// Responsible for submitting Sahel requests to MC
    /// </summary>
    public class SahelRequestSubmissionJob
    {
        private readonly IRequestFetcher _requestFetcher;
        private readonly IRequestStatusUpdater _statusUpdater;
        private readonly ISahelApiClient _sahelApiClient;
        private readonly SahelConfigurations _config;
        private readonly IRequestLogger _logger;
        private readonly eServicesContext _context;
        private readonly IKMIDNotificationService _KMIDNotificationService;
        private Dictionary<int, string> _civilIdLookup;

        public SahelRequestSubmissionJob(
            IRequestFetcher requestFetcher,
            IRequestStatusUpdater statusUpdater,
            ISahelApiClient sahelApiClient,
            SahelConfigurations config,
            IRequestLogger logger,
            eServicesContext context,
            IKMIDNotificationService kMIDNotificationService)
        {
            _requestFetcher = requestFetcher;
            _statusUpdater = statusUpdater;
            _sahelApiClient = sahelApiClient;
            _config = config;
            _logger = logger;
            _context = context;
            _KMIDNotificationService = kMIDNotificationService;
        }

        public async Task ExecuteAsync()
        {
            var fetchedRequests = await _requestFetcher.FetchAsync();
            var activeRequests = fetchedRequests.activeRequests;
            var expiredRequests = fetchedRequests.expiredRequests;

            var validRequestIds = activeRequests
                .Select(r => r.ServiceRequestsDetail?.EserviceRequestDetailsId ?? 0)
                .Where(id => id > 0);

            var organizationRegistrationNumbers = activeRequests
                .Where(r => r.ServiceId == (int)ServiceTypesEnum.OrganizationRegistrationService)
                .Select(r => r.OrganizationRequest.EserviceRequestNumber);

            await _statusUpdater.UpdateAsync(validRequestIds, organizationRegistrationNumbers);

            _civilIdLookup = await BuildCivilIdMap(activeRequests.Concat(expiredRequests).ToList());

            await _KMIDNotificationService.NotifyExpiredKmidRequestsAsync(expiredRequests, _civilIdLookup);
            await SubmitActiveKmidRequestsAsync(activeRequests);
        }



        private async Task SubmitActiveKmidRequestsAsync(List<ServiceRequest> activeRequests)
        {
            var submissionTasks = activeRequests.Select(ProcessRequestAsync);
            var results = await Task.WhenAll(submissionTasks);
            await NotificationWriter.InsertNotificationListAsync(_context, results.ToList());
        }

        private async Task<Notification> ProcessRequestAsync(ServiceRequest request)
        {
            try
            {
                var dto = SahelDtoFactory.CreateDto(request);
                var endpoint = GetServiceUrl(request.ServiceId.Value);
                return await _sahelApiClient.CallAsync(dto, endpoint);
            }
            catch (Exception ex)
            {
                return new Notification
                {
                    bodyAr = "حدث خطأ تقني أثناء تنفيذ الطلب. الرجاء إعادة إرسال الطلب لاحقًا.",
                    bodyEn = "A technical error occurred while processing your request. Please try again later.",
                    isForSubscriber = "true",
                    subscriberCivilId = _civilIdLookup[(int)request.RequesterUserId],
                    notificationType = ((int)NotificationTypeMapper.GetNotificationType((ServiceTypesEnum)request.ServiceId.Value)).ToString()
                };
            }
        }

        private string GetServiceUrl(long serviceId) => serviceId switch
        {
            (int)ServiceTypesEnum.NewImportLicenseRequest => _config.EservicesUrlsConfigurations.AddNewImportLicenseUrl,
            (int)ServiceTypesEnum.ImportLicenseRenewalRequest => _config.EservicesUrlsConfigurations.ReNewImportLicenseUrl,
            (int)ServiceTypesEnum.AddNewAuthorizedSignatoryRequest => _config.EservicesUrlsConfigurations.AddAuthorizedSignutryUrl,
            (int)ServiceTypesEnum.RenewAuthorizedSignatoryRequest => _config.EservicesUrlsConfigurations.RenewAuthorizedSignutryUrl,
            (int)ServiceTypesEnum.RemoveAuthorizedSignatoryRequest => _config.EservicesUrlsConfigurations.RemoveAuthorizedSignutryUrl,
            (int)ServiceTypesEnum.CommercialLicenseRenewalRequest => _config.EservicesUrlsConfigurations.RenewComercialLicenseUrl,
            (int)ServiceTypesEnum.IndustrialLicenseRenewalRequest => _config.EservicesUrlsConfigurations.RenewIndustrialLicenseUrl,
            (int)ServiceTypesEnum.ChangeCommercialAddressRequest => _config.EservicesUrlsConfigurations.ChangeComercialAddressUrl,
            (int)ServiceTypesEnum.OrgNameChangeReqServiceId => _config.EservicesUrlsConfigurations.ChangeOrgNameUrl,
            (int)ServiceTypesEnum.ConsigneeUndertakingRequest => _config.EservicesUrlsConfigurations.UnderTakingRequestUrl,
            (int)ServiceTypesEnum.OrganizationRegistrationService => _config.EservicesUrlsConfigurations.OrganizationRegistrationUrl,
            _ => throw new ArgumentException($"Invalid service ID: {serviceId}")
        };

        private async Task<Dictionary<int, string>> BuildCivilIdMap(List<ServiceRequest> requests)
        {
            var requesterIds = requests.Select(r => (int)r.RequesterUserId).Distinct().ToList();
            return await SharedUserService.GetCivilIdMapAsync(_context, requesterIds);
        }
    }
}
