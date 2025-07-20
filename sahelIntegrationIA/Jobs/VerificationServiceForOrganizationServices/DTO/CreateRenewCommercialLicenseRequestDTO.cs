using sahelIntegrationIA.Jobs.VerificationServiceForOrganizationServices.DTO;

namespace sahelIntegrationIA
{
  
        #region DTO

        public class CreateRenewCommercialLicenseRequestDTO : IServiceDto
    {
            public string RequestNumber { get; set; }
            public string ImportLicenseNumber { get; set; }
            public string CommercialLicenseNumber { get; set; }

            public string IndustrialLicenseNumber { get; set; }

            public DateTime LicenseIssueDate { get; set; }
            public DateTime LicenseExpiryDate { get; set; }
            public string ImportLicenseType { get; set; }
            public string ImportLicenseTypeDesc { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }


        }
        #endregion
    


}
