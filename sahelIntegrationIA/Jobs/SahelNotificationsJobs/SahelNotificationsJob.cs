using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using Microsoft.EntityFrameworkCore;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Jobs.SahelNotificationsJobs
{
  
    public class SahelNotificationsJob 
    {
        private readonly eServicesContext _context;
        private readonly IRequestLogger _logger;
        private readonly SahelConfigurations _sahelConfigurations;
        private readonly INotificationFetcher _notificationFetcher;
        private readonly INotificationProcessor _notificationProcessor;
        private string _jobCycleId;

        public SahelNotificationsJob(eServicesContext context,
                                     IRequestLogger logger,
                                     SahelConfigurations sahelConfigurations,
                                      INotificationFetcher requestFetcher,
                                     INotificationProcessor notificationProcessor)
        {
            _context = context;
            _logger = logger;
            _sahelConfigurations = sahelConfigurations;
           // _jobCycleId = new Guid().ToString();
            _notificationFetcher = requestFetcher;
            _notificationProcessor = notificationProcessor;
        }
        public async Task SendNotificationsAsync()
        {
            var notifications = await _notificationFetcher.GetPendingNotificationsAsync();
            await _notificationProcessor.ProcessNotificationsAsync(notifications);
        }


    }
}
