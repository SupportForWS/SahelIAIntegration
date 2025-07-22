using sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs.DTO;

namespace sahelIntegrationIA
{
    
        #region DTO
        public class AvailableImportLicenseDTO : IServiceDto
    {
            public string LicenseNumber { get; set; }
            public bool isSelected { get; set; }
            public bool isValid { get; set; }
        }
        #endregion
    


}
