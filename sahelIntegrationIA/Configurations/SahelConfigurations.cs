
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using sahelIntegrationIA.Configurations;

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
        public int BrokerKMIDCallingTimer { get; set; }
        public string ToWhomPrintableFormRedirectUrl { get; set; }
        public string ExamAttendanceRedirectUrl { get; set; }
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
        public string BrokerAffairsUrl { get; set; }
        public string BrokerExamUrl { get; set; }
        public string BrokerSharedUrl { get; set; }
        public string TransferServiceUrl { get; set; }
        public string SubmitBrokerSignUpUrl { get; set; }
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

        public string BrokerKmidExpiredAr { get; set; }
        public string BrokerKmidExpiredEn { get; set; }
        public string SignUpKmidExpiredEn { get; set; }
        public string SignUpKmidExpiredAr { get; set; }

        public string IdPrintedNotificationAr { get; set; }
        public string IdPrintedNotificationEn { get; set; }

        public string CompletedNotificationAr { get; set; }
        public string CompletedNotificationEn { get; set; }
        public string CompletedNotificationToWhomAr { get; set; }
        public string CompletedNotificationToWhomEn { get; set; }

        public string InitAcceptedNotificationAr { get; set; }
        public string InitAcceptedNotificationEn { get; set; }

        public string InitRejectedNotificationAr { get; set; }
        public string InitRejectedNotificationEn { get; set; }

        public string ConfirmExamAttendanceAr { get; set; }
        public string ConfirmExamAttendanceEn { get; set; }

        public string PassExamNotificationAr { get; set; }
        public string PassExamNotificationEn { get; set; }

        public string FailedExamNotificationEn { get; set; }
        public string FailedExamNotificationAr { get; set; }

        public string NotAttendExamNotificationAr { get; set; }
        public string NotAttendExamNotificationEn { get; set; }




    }
}
