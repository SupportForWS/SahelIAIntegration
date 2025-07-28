using sahelIntegrationIA.Jobs.SahelRequestSubmissionJob.DTO;

namespace sahelIntegrationIA
{
  
        #region DTO

        public class OrganizationChangeNameDTO : IServiceDto
    {
            public string OrganizationNewArabicName { get; set; }
            public string OrganizationNewEnglishName { get; set; }
            public string RequestNumber { get; set; }
            public string eServiceRequestId { get; set; }
            public string OrganizationOldEnglishName { get; set; }
            public string OrganizationOldArabicName { get; set; }
            public AvailableImportLicenseDTO availableImportLicenses { get; set; }
            public bool AddNewImportLicenseRequest { get; set; }
            public string LicenseNo { get; set; }
            public int ImportLicenseType { get; set; }
            public DateTime IssueDate { get; set; }
            public DateTime ExpiryDate { get; set; }
            public string TradingLicenseNo { get; set; }
            public string CommercialLicenseNumber { get; set; }
            public string organizationName { get; set; }
            public string ImporterLicenseNo { get; set; }
            public int ImporterLicenseType { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }


        }
        #endregion
    


}
