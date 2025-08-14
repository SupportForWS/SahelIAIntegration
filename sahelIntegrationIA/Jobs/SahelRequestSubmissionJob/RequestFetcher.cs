using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.IdentityEntities;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.OrganizationEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Jobs.Shared.Constants;
using sahelIntegrationIA.Models;
using System;
using System.Linq;

namespace sahelIntegrationIA.Jobs.SahelRequestSubmissionJob
{
    public interface IRequestFetcher
    {
        Task<(List<ServiceRequest> activeRequests, List<ServiceRequest> expiredRequests)> FetchAsync(string jobCycleId);
    }

    public class RequestFetcher : IRequestFetcher
    {
        private readonly eServicesContext _ctx;
        private readonly IRequestLogger _log;
        private readonly SahelConfigurations _sahelConfigurations;
        readonly DateTime currentDate = DateTime.Now;

        public RequestFetcher(eServicesContext ctx, IRequestLogger log, SahelConfigurations cfg)
        {
            _ctx = ctx;
            _log = log;
            _sahelConfigurations = cfg;
        }

        private static readonly int[] ServiceIds = new[]
        {
        (int)ServiceTypesEnum.NewImportLicenseRequest,
        (int)ServiceTypesEnum.ImportLicenseRenewalRequest,
        (int)ServiceTypesEnum.CommercialLicenseRenewalRequest,
        (int)ServiceTypesEnum.IndustrialLicenseRenewalRequest,
        (int)ServiceTypesEnum.AddNewAuthorizedSignatoryRequest,
        (int)ServiceTypesEnum.RenewAuthorizedSignatoryRequest,
        (int)ServiceTypesEnum.RemoveAuthorizedSignatoryRequest,
        (int)ServiceTypesEnum.OrgNameChangeReqServiceId,
        (int)ServiceTypesEnum.ChangeCommercialAddressRequest,
        (int)ServiceTypesEnum.ConsigneeUndertakingRequest
    };

        private static readonly int[] ServiceIdsForValidation = new[]
        {
        (int)ServiceTypesEnum.AddNewAuthorizedSignatoryRequest,
        (int)ServiceTypesEnum.RenewAuthorizedSignatoryRequest,
        (int)ServiceTypesEnum.OrgNameChangeReqServiceId
    };

        public async Task<(List<ServiceRequest> activeRequests, List<ServiceRequest> expiredRequests)> FetchAsync(string jobCycleId)
        {
            _log.LogInformation("{JobCycleId} - [Sahel] Starting fetch of pending service requests at {Time}", jobCycleId, DateTime.Now);

            var statusEnums = ServiceConstants.BuildStatusesForOrgVerficationService();

            var requestList = await GetRequestsForValidation(jobCycleId, statusEnums, ServiceIds, ServiceIdsForValidation, currentDate);
            _log.LogInformation("{JobCycleId} - [Sahel] Found {Count} service requests for validation", jobCycleId, requestList.Count);

            var organizationRequestList = await GetOrganizationRequestsForValidation(jobCycleId, statusEnums);
            _log.LogInformation("{JobCycleId} - [Sahel] Found {Count} organization requests for validation", jobCycleId, organizationRequestList.Count);

            requestList.AddRange(organizationRequestList);

            _log.LogInformation("{JobCycleId} - [Sahel] Total requests before KMID expiry check: {Count}", jobCycleId, requestList.Count);

            var expiredRequests = await HandleKMIDExpiryTokenAsync(jobCycleId, requestList);

            _log.LogInformation("{JobCycleId} - [Sahel] ActiveRequests={ActiveCount}, ExpiredRequests={ExpiredCount}",
                jobCycleId, requestList.Count - expiredRequests.Count, expiredRequests.Count);

            return (requestList.Except(expiredRequests).ToList(), expiredRequests);
        }

        private async Task<List<ServiceRequest>> GetRequestsForValidation(string jobCycleId, string[] statusEnums, int[] serviceIds,
            int[] serviceIdsForValidation, DateTime currentDate)
        {
            var list = await _ctx.Set<ServiceRequest>()
                .Include(p => p.ServiceRequestsDetail)
                .Where(p =>
                    statusEnums.Contains(p.StateId)
                    && p.RequestSource == RequestSourceEnum.Sahel.ToString()
                    && !string.IsNullOrEmpty(p.ServiceRequestsDetail.KMIDToken)
                    && (
                        serviceIdsForValidation.Contains(p.ServiceRequestsDetail.RequestServicesId.Value)
                            ? !string.IsNullOrEmpty(p.ServiceRequestsDetail.kmidTokenForAuthorizer)
                            : true
                    )
                    && serviceIds.Contains((int)p.ServiceId.Value)
                    && (
                        p.ServiceRequestsDetail.ReadyForSahelSubmission == "1"
                        || (
                            p.ServiceRequestsDetail.ReadyForSahelSubmission == "2"
                            && p.RequestSubmissionDateTime.HasValue
                            && p.RequestSubmissionDateTime.Value.AddMinutes(_sahelConfigurations.SahelSubmissionTimer) < currentDate
                        )
                    )
                )
                .AsNoTracking()
                .ToListAsync();

            _log.LogInformation("{JobCycleId} - [Sahel] Detailed request list from GetRequestsForValidation: {Requests}",
                jobCycleId, string.Join(",", list.Select(r => r.EserviceRequestId)));

            return list;
        }

        private async Task<List<ServiceRequest>> GetOrganizationRequestsForValidation(string jobCycleId, string[] statusEnums)
        {
            var list = await _ctx.Set<ServiceRequest>()
                .Include(p => p.OrganizationRequest)
                .Where(p =>
                    statusEnums.Contains(p.StateId)
                    && p.RequestSource == "Sahel"
                    && p.ServiceId == (int)ServiceTypesEnum.OrganizationRegistrationService
                    && !string.IsNullOrEmpty(p.OrganizationRequest.KMIDToken)
                    && p.OrganizationRequest.StateId != "OrganizationRequestForCreateState"
                    && p.OrganizationRequest.StateId != "OrganizationRequestForUpdateState"
                    && (
                        p.OrganizationRequest.ReadyForSahelSubmission == "1"
                        || (
                            p.OrganizationRequest.ReadyForSahelSubmission == "2"
                            && p.RequestSubmissionDateTime.HasValue
                            && p.RequestSubmissionDateTime.Value.AddMinutes(_sahelConfigurations.SahelSubmissionTimer) < currentDate
                        )
                    )
                )
                .AsNoTracking()
                .ToListAsync();

            _log.LogInformation("{JobCycleId} - [Sahel] Detailed organization request list: {Requests}",
                jobCycleId, string.Join(",", list.Select(r => r.EserviceRequestId)));

            return list;
        }

        public async Task<List<ServiceRequest>> HandleKMIDExpiryTokenAsync(string jobCycleId, List<ServiceRequest> requests)
        {
            var now = DateTime.Now;
            var tokens = requests.Select(GetToken).Where(t => !string.IsNullOrEmpty(t));

            var expiredTokenIds = await _ctx.Set<KGACPACIQueue>()
                .Where(q => tokens.Contains(q.KGACPACIQueueId.ToString())
                            && q.DateCreated.AddSeconds(_sahelConfigurations.OrganizationKMIDCallingTimer) < now)
                .Select(q => q.KGACPACIQueueId)
                .ToListAsync();

            if (!expiredTokenIds.Any())
            {
                _log.LogInformation("{JobCycleId} - [Sahel] No expired KMID tokens found", jobCycleId);
                return requests;
            }

            _log.LogInformation("{JobCycleId} - [Sahel] {Count} KMID tokens have expired", jobCycleId, expiredTokenIds.Count);

            var expiredRequests = requests
                .Where(r => expiredTokenIds.Contains(int.Parse(GetToken(r))))
                .ToList();

            _log.LogInformation("{JobCycleId} - Expired Request IDs: {Ids}",
                jobCycleId, string.Join(",", expiredRequests.Select(r => r.EserviceRequestId)));

            await ClearTokensAsync(jobCycleId, expiredRequests);

            return expiredRequests;
        }

        private static string GetToken(ServiceRequest request) =>
            request.ServiceId == (int)ServiceTypesEnum.OrganizationRegistrationService
                ? request.OrganizationRequest.KMIDToken
                : request.ServiceRequestsDetail.KMIDToken;

        private async Task ClearTokensAsync(string jobCycleId, List<ServiceRequest> expiredRequests)
        {
            var detailIds = expiredRequests
                .Where(r => r.ServiceId != (int)ServiceTypesEnum.OrganizationRegistrationService)
                .Select(r => r.ServiceRequestsDetail.EserviceRequestDetailsId);

            if (detailIds.Any())
            {
                _log.LogInformation("{JobCycleId} - [Sahel] Clearing KMIDToken for {Count} service requests",
                    jobCycleId, detailIds.Count());

                await _ctx.Set<ServiceRequestsDetail>()
                    .Where(d => detailIds.Contains(d.EserviceRequestDetailsId))
                    .ExecuteUpdateAsync(d => d.SetProperty(x => x.KMIDToken, ""));
            }

            var orgRequestNumbers = expiredRequests
                .Where(r => r.ServiceId == (int)ServiceTypesEnum.OrganizationRegistrationService)
                .Select(r => r.OrganizationRequest.EserviceRequestNumber);

            if (orgRequestNumbers.Any())
            {
                _log.LogInformation("{JobCycleId} - [Sahel] Clearing KMIDToken & resetting ReadyForSahelSubmission for {Count} organization requests",
                    jobCycleId, orgRequestNumbers.Count());

                await _ctx.Set<OrganizationRequests>()
                    .Where(o => orgRequestNumbers.Contains(o.RequestNumber))
                    .ExecuteUpdateAsync(o => o
                        .SetProperty(x => x.KMIDToken, "")
                        .SetProperty(x => x.ReadyForSahelSubmission, "0"));
            }
        }
    }

}
