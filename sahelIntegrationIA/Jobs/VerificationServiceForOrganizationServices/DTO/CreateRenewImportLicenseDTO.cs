using sahelIntegrationIA.Jobs.VerificationServiceForOrganizationServices.DTO;

namespace sahelIntegrationIA
{
  
        #region DTO

        public class CreateRenewImportLicenseDTO : IServiceDto

    {

            public string RequestNumber { get; set; }

            public string eServiceRequestId { get; set; }

            public string ImporterLicenseNo { get; set; }

            public DateTime LicenseIssueDate { get; set; }

            public DateTime LicenseExpiryDate { get; set; }

            public string TypeOfLicenseRequest { get; set; }

            public string IndustrialLicenseNo { get; set; }

            public string CommercialLicenseNo { get; set; }

            public string LicenseType { get; set; }

            public string LicenseTypeDesc { get; set; }

            public int ImporterLicenseType { get; set; }

            public string ImporterLicenseTypeDesc { get; set; }

            public bool IsFromSahel { get; set; }

            public string SelectedAuthorizerCivilId { get; set; }

        }
        #endregion
    


}
