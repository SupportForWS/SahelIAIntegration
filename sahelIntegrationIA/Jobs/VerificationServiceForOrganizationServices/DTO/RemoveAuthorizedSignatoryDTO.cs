using sahelIntegrationIA.Jobs.VerificationServiceForOrganizationServices.DTO;

namespace sahelIntegrationIA
{
  
        #region DTO

        public class RemoveAuthorizedSignatoryDTO : IServiceDto
    {
            public string RequestNumber { get; set; }
            public string OrganizationNameEnglish { get; set; }
            public string OrganizationNameArabic { get; set; }
            public AssociatedPersonDetails AssociatedAuthorizedSignatories { get; set; }
            public string EServicerequestId { get; set; }
            public string TradeLicenceNumber { get; set; }   //not used 
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }


        }
        #endregion
    


}
