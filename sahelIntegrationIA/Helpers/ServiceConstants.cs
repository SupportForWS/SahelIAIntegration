using eServicesV2.Kernel.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Helpers
{
    public static class ServiceConstants
    {
        public static string[] BuildStatuses() => new[]
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
    }
}
