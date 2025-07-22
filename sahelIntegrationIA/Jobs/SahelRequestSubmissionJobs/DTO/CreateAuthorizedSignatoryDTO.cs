using sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs.DTO;

namespace sahelIntegrationIA
{
 
        #region DTO
        public class CreateAuthorizedSignatoryDTO : IServiceDto
    {
            public string Name { get; set; }
            public string CivilId { get; set; }
            public DateTime CivilIdExpiryDate { get; set; }
            public DateTime IssueDate { get; set; }
            public DateTime ExpiryDate { get; set; }
        }
        #endregion
    


}
