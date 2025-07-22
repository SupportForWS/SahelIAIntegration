using sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs.DTO;

namespace sahelIntegrationIA
{
    public class CreateAddNewImportLicenseDTO : IServiceDto
    {
        public string RequestNumber { get; set; }
        public string ImporterLicenseNo { get; set; }
        public string SelectedAuthorizerCivilId { get; set; }
        public bool IsFromSahel { get; set; }
    }
}
