
namespace sahelIntegrationIA.Configurations
{
    public class SahelConfigurations
    {
        public IndividualAuthorizationConfiguration IndividualAuthorizationConfiguration { get; set; }
        public int OrganizationKMIDCallingTimer { get; set; }
        public EservicesUrlsConfigurations EservicesUrlsConfigurations { get; set; }


    }
    public class IndividualAuthorizationConfiguration
    {
        public string QRReportConfigId { get; set; }
        public string ReferenceType { get; set; }
        public string IndividualQRReportUrl { get; set; }

        public string NotificationTypeForIA { get; set; }
        public TimeSpan IndividualAuthorizationKMIDCallingTimer { get; set; }
    }
    public class EservicesUrlsConfigurations
    {
        public string AddNewImportLicenseUrl { get; set;}
        public string ReNewImportLicenseUrl { get; set; }

    }

}
