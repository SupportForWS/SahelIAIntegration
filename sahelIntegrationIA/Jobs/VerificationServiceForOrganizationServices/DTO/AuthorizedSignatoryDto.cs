using sahelIntegrationIA.Jobs.VerificationServiceForOrganizationServices.DTO;

namespace sahelIntegrationIA
{
 
        #region DTO

        public class AuthorizedSignatoryDto : IServiceDto
        {
            public string EServiceRequestId { get; set; }
            public string RequestNumber { get; set; }
            public string OrganizationId { get; set; }
            public string AuthPerson { get; set; }
            public string CivilId { get; set; }
            public DateTime AuthorizedSignatoryCivilIdExpiryDate { get; set; }
            public int? NationalityId { get; set; }
            public DateTime IssueDate { get; set; }
            public DateTime ExpiryDate { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }


        }
        #endregion
    


}
