using sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs.DTO;

namespace sahelIntegrationIA
{

        #region DTO

        public class ReNewAuthorizedSignatoryDTO : IServiceDto
    {
            public string RequestNumber { get; set; }
            public string EServiceRequestId { get; set; }
            public CreateAuthorizedSignatoryDTO AuthorizedSignatory { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }
        }
        #endregion
    


}
