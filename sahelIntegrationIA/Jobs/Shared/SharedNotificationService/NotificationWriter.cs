using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Enums;
using Newtonsoft.Json;
using sahelIntegrationIA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;

namespace sahelIntegrationIA.Jobs.Shared.SharedNotificationService
{
    public static class NotificationWriter
    {
        public static async Task<bool> InsertNotificationListAsync(eServicesContext context, IEnumerable<Notification> notification, string sahelType = "B")
        {
            if (notification == null || !notification.Any())
                return false;

            var queues = notification.Select(notification => new KGACSahelOutSyncQueue
            {
                CivilId = notification.subscriberCivilId,
                CreatedBy = notification.subscriberCivilId,
                NotificationId = int.Parse(notification.notificationType),
                SahelType = sahelType,
                MsgTableAr = JsonConvert.SerializeObject(notification.dataTableAr ?? new Dictionary<string, string>()),
                MsgTableEn = JsonConvert.SerializeObject(notification.dataTableEn ?? new Dictionary<string, string>()),
                MsgBodyAr = notification.bodyAr,
                MsgBodyEn = notification.bodyEn,
                DateCreated = DateTime.Now,
                Sync = false,
                TryCount = 1,
                Source = RequestSourceEnum.eService.ToString()
            }).ToList();

            await context.AddRangeAsync(queues);
            var result = await context.SaveChangesAsync();
            return result > 0;
        }

        public static async Task<bool> InsertNotificationAsync(eServicesContext context, Notification notification, bool sent, string sahelType = "B")
        {
            var queue = new KGACSahelOutSyncQueue
            {
                CivilId = notification.subscriberCivilId,
                CreatedBy = notification.subscriberCivilId,
                NotificationId = int.Parse(notification.notificationType),
                SahelType = sahelType,
                MsgTableAr = JsonConvert.SerializeObject(notification.dataTableAr ?? new Dictionary<string, string>()),
                MsgTableEn = JsonConvert.SerializeObject(notification.dataTableEn ?? new Dictionary<string, string>()),
                MsgBodyAr = notification.bodyAr,
                MsgBodyEn = notification.bodyEn,
                DateCreated = DateTime.Now,
                Sync = sent,
                TryCount = 1,
                Source = RequestSourceEnum.eService.ToString()
            };
            context.Add(queue);
            var result = await context.SaveChangesAsync();
            return result > 0;
        }



    }

}

