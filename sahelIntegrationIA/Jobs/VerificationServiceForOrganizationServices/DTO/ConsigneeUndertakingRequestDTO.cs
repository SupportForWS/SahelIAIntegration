using sahelIntegrationIA.Jobs.VerificationServiceForOrganizationServices.DTO;

namespace sahelIntegrationIA
{
  
        #region DTO

        public class ConsigneeUndertakingRequestDTO : IServiceDto
    {
            public string RequestNo { get; set; }
            public string Eservicerequestid { get; set; }
            public string TradeLicenceNumber { get; set; }
            public string ConsigneeName { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }
        }
        #endregion
    


}
