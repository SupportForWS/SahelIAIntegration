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

namespace sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs
{
    public interface IRequestFetcher
    {
        Task<(List<ServiceRequest> activeRequests, List<ServiceRequest> expiredRequests)> FetchAsync();
    }

    public class RequestFetcher : IRequestFetcher
    {
        private readonly eServicesContext _ctx;
        private readonly IRequestLogger _log;
        private readonly SahelConfigurations _sahelConfigurations;
        DateTime currentDate = DateTime.Now;

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

        public async Task<(List<ServiceRequest> activeRequests, List<ServiceRequest> expiredRequests)> FetchAsync()
        {
            _log.LogInformation("[Sahel] Starting fetch of pending service requests at {Time}", DateTime.Now);

            var statusEnums = ServiceConstants.BuildStatusesForOrgVerficationService();
            var requestList = await GetRequestsForValidation(statusEnums, ServiceIds, ServiceIdsForValidation, currentDate);
            var organizationRequestList = await GetOrganizationRequestsForValidation(statusEnums);

            requestList.AddRange(organizationRequestList);

            var expiredRequests = await HandleKMIDExpiryTokenAsync(requestList);

            return (requestList.Except(expiredRequests).ToList(), expiredRequests);
        }

        private async Task<List<ServiceRequest>> GetRequestsForValidation(string[] statusEnums, int[] serviceIds,
            int[] serviceIdsForValidation, DateTime currentDate)
        {
            return await _ctx.Set<ServiceRequest>()
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
        }

        private async Task<List<ServiceRequest>> GetOrganizationRequestsForValidation(string[] statusEnums)
        {
            return await _ctx.Set<ServiceRequest>()
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
        }

        public async Task<List<ServiceRequest>> HandleKMIDExpiryTokenAsync(List<ServiceRequest> requests)
        {
            var now = DateTime.Now;
            var tokens = requests.Select(GetToken).Where(t => !string.IsNullOrEmpty(t));

            var expiredTokenIds = await _ctx.Set<KGACPACIQueue>()
                .Where(q => tokens.Contains(q.KGACPACIQueueId.ToString())
                            && q.DateCreated.AddSeconds(_sahelConfigurations.OrganizationKMIDCallingTimer) < now)
                .Select(q => q.KGACPACIQueueId)
                .ToListAsync();

            if (!expiredTokenIds.Any()) return requests;

            _log.LogInformation("[Sahel] {Count} KMID tokens have expired.", expiredTokenIds.Count);

            var expiredRequests = requests
                .Where(r => expiredTokenIds.Contains(int.Parse(GetToken(r))))
                .ToList();

            await ClearTokensAsync(expiredRequests);

            return expiredRequests;
        }

        private static string GetToken(ServiceRequest request) =>
            request.ServiceId == (int)ServiceTypesEnum.OrganizationRegistrationService
                ? request.OrganizationRequest.KMIDToken
                : request.ServiceRequestsDetail.KMIDToken;

        private async Task ClearTokensAsync(List<ServiceRequest> expiredRequests)
        {
            var detailIds = expiredRequests
                .Where(r => r.ServiceId != (int)ServiceTypesEnum.OrganizationRegistrationService)
                .Select(r => r.ServiceRequestsDetail.EserviceRequestDetailsId);

            if (detailIds.Any())
            {
                await _ctx.Set<ServiceRequestsDetail>()
                    .Where(d => detailIds.Contains(d.EserviceRequestDetailsId))
                    .ExecuteUpdateAsync(d => d.SetProperty(x => x.KMIDToken, ""));
            }

            var orgRequestNumbers = expiredRequests
                .Where(r => r.ServiceId == (int)ServiceTypesEnum.OrganizationRegistrationService)
                .Select(r => r.OrganizationRequest.EserviceRequestNumber);

            if (orgRequestNumbers.Any())
            {
                await _ctx.Set<OrganizationRequests>()
                    .Where(o => orgRequestNumbers.Contains(o.RequestNumber))
                    .ExecuteUpdateAsync(o => o
                        .SetProperty(x => x.KMIDToken, "")
                        .SetProperty(x => x.ReadyForSahelSubmission, "0"));
            }
        }
    }

}
