using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using Microsoft.EntityFrameworkCore;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Jobs.Shared.Enums;
using sahelIntegrationIA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Jobs.SahelNotificationsJobs
{
    public interface INotificationFetcher
    {
        public Task<List<KGACSahelOutSyncQueue>> GetPendingNotificationsAsync();

    }
    public class NotificationFetcher: INotificationFetcher
    {
        private readonly eServicesContext _context;
        private readonly IRequestLogger _logger;
        private readonly SahelConfigurations _sahelConfigurations;
        private string _jobCycleId;

        public NotificationFetcher(eServicesContext context, IRequestLogger logger, SahelConfigurations sahelConfigurations)
        {
            _context = context;
            _logger = logger;
            _sahelConfigurations = sahelConfigurations;
         }

        public async Task<List<KGACSahelOutSyncQueue>> GetPendingNotificationsAsync()
        {
            return await _context.Set<KGACSahelOutSyncQueue>()
                                .Where(x => (x.Source == NotificationSource.MC.ToString()
                                                   && x.Sync.Value != true
                                                   && x.TryCount.Value <= _sahelConfigurations.TryCountForMCNotification)
                                            || (x.Source == NotificationSource.eService.ToString()
                                                && x.Sync != true
                                                && x.TryCount.Value <= _sahelConfigurations.TryCountForeServiceNotification))
                                .AsNoTracking()
                                .ToListAsync();
        }
    }
}
