
namespace sahelIntegrationIA.Configurations
{
    public class SahelConfigurations
    {
        public IndividualAuthorizationConfiguration IndividualAuthorizationConfiguration { get; set; }
        public int OrganizationKMIDCallingTimer { get; set; }
        public EservicesUrlsConfigurations EservicesUrlsConfigurations { get; set; }
        public MCNotificationConfiguration MCNotificationConfiguration { get; set; }
        public int SahelSubmissionTimer { get; set; }
        public bool IsSahelNotificationServiceEnable { get; set; }
        public bool IsSendMcActionNotificationServiceEnable { get; set; }
        public int TryCountForMCNotification { get; set; }
        public int TryCountForeServiceNotification { get; set; }

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
        public string AddNewImportLicenseUrl { get; set; }
        public string ReNewImportLicenseUrl { get; set; }
        public string RenewComercialLicenseUrl { get; set; }
        public string RenewIndustrialLicenseUrl { get; set; }
        public string AddAuthorizedSignutryUrl { get; set; }
        public string RemoveAuthorizedSignutryUrl { get; set; }
        public string RenewAuthorizedSignutryUrl { get; set; }
        public string ChangeOrgNameUrl { get; set; }
        public string ChangeComercialAddressUrl { get; set; }
        public string UnderTakingRequestUrl { get; set; }
        public string EPaymentRequestUrl { get; set; }
        public string OrganizationRegistrationUrl { get; set; }


    }

    public class MCNotificationConfiguration
    {
        public string RejectNotificationAr { get; set; }
        public string ApproveNotificationAr { get; set; }
        public string FinalRejectNotificationAr { get; set; }
        public string AdditionalInfoNotificationAr { get; set; }
        public string VisiNotificationAr { get; set; }
        public string RejectNotificationEn { get; set; }
        public string ApproveNotificationEn { get; set; }
        public string FinalRejectNotificationEn { get; set; }
        public string AdditionalInfoNotificationEn { get; set; }
        public string VisiNotificationEn { get; set; }
        public string KmidExpiredAr { get; set; }
        public string KmidExpiredEn { get; set; }
    }
}
