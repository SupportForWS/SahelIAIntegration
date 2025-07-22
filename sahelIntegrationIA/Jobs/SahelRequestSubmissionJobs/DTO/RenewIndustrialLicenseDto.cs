using sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs.DTO;

namespace sahelIntegrationIA
{

        #region DTO

        public class RenewIndustrialLicenseDto : IServiceDto
    {
            public string RequestNumber { get; set; }
            public string LicenseNumber { get; set; }
            public string ImportLicenseNumber { get; set; }
            public string CommercialLicenseNumber { get; set; }
            public DateTime LicenseIssueDate { get; set; }
            public DateTime LicenseExpiryDate { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }

        }
        #endregion
    


}
