using sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs.DTO;

namespace sahelIntegrationIA
{
   
        #region DTO

        public class SubmitOrganizationRequestDTO : IServiceDto
    {
            public string OrganizationRequestId { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }

        }
        #endregion
    


}
