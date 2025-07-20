using eServicesV2.Kernel.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Helpers
{
    //move to Shared/Constants

    public class Service
    {
        public static Service EServiceRequestORGForVisitState => new Service(ServiceRequestStatesEnum.EServiceRequestORGForVisitState, "notification id");

        public ServiceRequestStatesEnum ServiceName { get; set; }
        public string NotificationKey { get; set; }
        public int Id => (int)ServiceName;

        private Service(ServiceRequestStatesEnum service, string notificationKey)
        {
            ServiceName = service;
            NotificationKey = notificationKey;
        }
    }
    public static class ServiceConstants
    {
        public static string[] BuildStatusesForSendMCService() => new[]
        {
            nameof(ServiceRequestStatesEnum.EServiceRequestORGForVisitState),
            nameof(ServiceRequestStatesEnum.EServiceRequestORGForAdditionalInfo),
            nameof(ServiceRequestStatesEnum.EServiceRequestORGRejectedState),
            nameof(ServiceRequestStatesEnum.EServiceRequestRejectedState),
            nameof(ServiceRequestStatesEnum.EServiceRequestORGApprovedState),
            nameof(ServiceRequestStatesEnum.EServiceRequestFinalRejectedState),
            "EServiceRequestApprovedState",
            "EServiceRequestRejectState",
            "OrganizationRequestRejectedState",
            "OrganizationRequestedForAdditionalInfoState",
            "OrganizationRequestApprovedForUpdate",
            "OrganizationRequestApprovedForCreate"
        };

        public static int[] BuildServiceIds() => new[]
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

        public static string[] BuildStatusesForOrgVerficationService() => new[]
     {
            nameof(ServiceRequestStatesEnum.EServiceRequestORGCreatedState),
            nameof(ServiceRequestStatesEnum.EServiceRequestORGForAdditionalInfo),
            nameof(ServiceRequestStatesEnum.EServiceRequestORGRejectedState),
            nameof(ServiceRequestStatesEnum.EServiceRequestCreatedState),
            nameof(ServiceRequestStatesEnum.EServiceRequestRejectedState),
            nameof(ServiceRequestStatesEnum.EServiceOrganizationRequestCreatedState),
            "OrganizationRequestCreatedState","OrganizationRequestRejectedState","OrganizationRequestedForAdditionalInfoState"
        };
    }
}
