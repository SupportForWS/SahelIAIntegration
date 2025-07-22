using sahelIntegrationIA.Jobs.SahelRequestSubmissionJobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs;
using sahelIntegrationIA.Jobs.SendMCNotificationJobs;

namespace sahelIntegrationIA.Jobs.Shared
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSahelIntegrationServicesForSendMCNotification(this IServiceCollection services)
        {
            services.AddSingleton<IServiceRequestProvider, ServiceRequestProvider>();
            services.AddSingleton<INotificationContentFactory, NotificationContentFactory>();
            services.AddSingleton<INotificationProcessor, NotificationProcessor>();
            services.AddSingleton<SahelNotificationClient>();
            services.AddSingleton<OldSendMCNotificationRefactoring>();
            services.AddSingleton<NewSendMcActionNotificationService>();

            return services;
        }

        public static IServiceCollection AddSahelIntegrationServicesForOrgVerficiationService(this IServiceCollection services)
        {
            services.AddSingleton<SahelRequestSubmissionJob>();
            services.AddSingleton<IRequestFetcher, RequestFetcher>();
             services.AddSingleton<IRequestStatusUpdater, RequestStatusUpdater>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<ISahelApiClient, SahelApiClient>();

            return services;
        }
    }
}
