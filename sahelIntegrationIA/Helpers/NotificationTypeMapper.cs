using eServicesV2.Kernel.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Helpers
{
    public static class NotificationTypeMapper
    {
        public static SahelNotficationTypesEnum GetNotificationType(ServiceTypesEnum service) => service switch
        {
            ServiceTypesEnum.AddNewAuthorizedSignatoryRequest => SahelNotficationTypesEnum.AddNewAuthorizedSignatory,
            ServiceTypesEnum.RemoveAuthorizedSignatoryRequest => SahelNotficationTypesEnum.RemoveAuthorizedSignatory,
            ServiceTypesEnum.RenewAuthorizedSignatoryRequest => SahelNotficationTypesEnum.RenewAuthorizedSignatory,
            ServiceTypesEnum.ImportLicenseRenewalRequest => SahelNotficationTypesEnum.RenewImportLicense,
            ServiceTypesEnum.NewImportLicenseRequest => SahelNotficationTypesEnum.AddNewImportLicense,
            ServiceTypesEnum.ChangeCommercialAddressRequest => SahelNotficationTypesEnum.ChangeCommercialAddress,
            ServiceTypesEnum.IndustrialLicenseRenewalRequest => SahelNotficationTypesEnum.RenewIndustrialLicense,
            ServiceTypesEnum.CommercialLicenseRenewalRequest => SahelNotficationTypesEnum.RenewCommercialLicense,
            ServiceTypesEnum.OrgNameChangeReqServiceId => SahelNotficationTypesEnum.OrganizationNameChange,
            ServiceTypesEnum.ConsigneeUndertakingRequest => SahelNotficationTypesEnum.UnderTakingConsigneeRequest,
            ServiceTypesEnum.OrganizationRegistrationService => SahelNotficationTypesEnum.OrganizationRegistrationService,
            _ => SahelNotficationTypesEnum.RenewImportLicense
        };
    }
}
