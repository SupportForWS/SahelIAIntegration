
namespace sahelIntegrationIA.Configurations
{
    public class SahelConfigurations
    {
        public IndividualAuthorizationConfiguration IndividualAuthorizationConfiguration { get; set; }
        public int OrganizationKMIDCallingTimer { get; set; }

    }
    public class IndividualAuthorizationConfiguration
    {
        public string QRReportConfigId { get; set; }
        public string ReferenceType { get; set; }
        public string IndividualQRReportUrl { get; set; }

        public string NotificationTypeForIA { get; set; }
        public TimeSpan IndividualAuthorizationKMIDCallingTimer { get; set; }
    }


}
