using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using Newtonsoft.Json;
using sahelIntegrationIA.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;

namespace sahelIntegrationIA.Jobs.SendMCNotificationJobs
{
    public interface INotificationContentFactory
    {
        (string msgAr, string msgEn) BuildNotificationContentForServiceRequest(ServiceRequest request);
        public Notification BuildNotificationFromQueueMessage(KGACSahelOutSyncQueue notification);
    }

    public class NotificationContentFactory : INotificationContentFactory
    {
        private readonly SahelConfigurations _config;

        public NotificationContentFactory(SahelConfigurations config)
        {
            _config = config;
        }

        public (string msgAr, string msgEn) BuildNotificationContentForServiceRequest(ServiceRequest sr)
        {
            var stateId = sr.ServiceId != (int)ServiceTypesEnum.OrganizationRegistrationService
                ? sr.StateId
                : sr.OrganizationRequest.StateId;

            string ar = string.Empty, en = string.Empty;
            switch (stateId)
            {
                case nameof(ServiceRequestStatesEnum.EServiceRequestORGForVisitState):
                    ar = string.Format(_config.MCNotificationConfiguration.VisiNotificationAr, sr.EserviceRequestNumber);
                    en = string.Format(_config.MCNotificationConfiguration.VisiNotificationEn, sr.EserviceRequestNumber);
                    break;
                case nameof(ServiceRequestStatesEnum.EServiceRequestORGForAdditionalInfo):
                case "OrganizationRequestedForAdditionalInfoState":
                    ar = string.Format(_config.MCNotificationConfiguration.AdditionalInfoNotificationAr, sr.EserviceRequestNumber);
                    en = string.Format(_config.MCNotificationConfiguration.AdditionalInfoNotificationEn, sr.EserviceRequestNumber);
                    break;
                case nameof(ServiceRequestStatesEnum.EServiceRequestORGRejectedState):
                case nameof(ServiceRequestStatesEnum.EServiceRequestRejectedState):
                case "OrganizationRequestRejectedState":
                case "EServiceRequestRejectState":
                    ar = string.Format(_config.MCNotificationConfiguration.RejectNotificationAr, sr.EserviceRequestNumber);
                    en = string.Format(_config.MCNotificationConfiguration.RejectNotificationEn, sr.EserviceRequestNumber);
                    break;
                case nameof(ServiceRequestStatesEnum.EServiceRequestFinalRejectedState):
                    ar = string.Format(_config.MCNotificationConfiguration.FinalRejectNotificationAr, sr.EserviceRequestNumber);
                    en = string.Format(_config.MCNotificationConfiguration.FinalRejectNotificationEn, sr.EserviceRequestNumber);
                    break;
                default:
                    ar = string.Format(_config.MCNotificationConfiguration.ApproveNotificationAr, sr.EserviceRequestNumber);
                    en = string.Format(_config.MCNotificationConfiguration.ApproveNotificationEn, sr.EserviceRequestNumber);
                    break;
            }

            //  var type = GetNotificationType((ServiceTypesEnum)sr.ServiceId);
            return (ar, en);
        }


        public Notification BuildNotificationFromQueueMessage(KGACSahelOutSyncQueue notification)
        {
            //var reqJson = JsonConvert.SerializeObject(notification, Formatting.None,
            //    new JsonSerializerSettings()
            //    {
            //        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            //    });

            //_logger.LogInformation("SahelNotificationService - start Notification creation process - {0}",
            //    propertyValues: new { reqJson });

            var notificationResponse = new Notification
            {
                bodyEn = notification.MsgBodyAr,
                bodyAr = notification.MsgBodyEn,
                isForSubscriber = "true",
                notificationType = notification.NotificationId.ToString(),
                subscriberCivilId = notification.CivilId
            };

            try
            {
                notificationResponse.dataTableEn = JsonConvert.DeserializeObject<Dictionary<string, string>>(notification.MsgTableEn);
                notificationResponse.dataTableAr = JsonConvert.DeserializeObject<Dictionary<string, string>>(notification.MsgTableAr);
            }
            catch (JsonException)
            {
                notificationResponse.dataTableEn = new Dictionary<string, string> { { "Header", notification.MsgTableEn } };
                notificationResponse.dataTableAr = new Dictionary<string, string> { { "عنوان", notification.MsgTableAr } };
            }

            return notificationResponse;
        }

    }
}
