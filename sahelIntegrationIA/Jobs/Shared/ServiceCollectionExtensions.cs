using sahelIntegrationIA.Jobs.SahelRequestSubmissionJobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs;
using sahelIntegrationIA.Jobs.SahelNotificationsJobs;
using sahelIntegrationIA.Jobs.MCNotificationQueueWriterJobs;

namespace sahelIntegrationIA.Jobs.Shared
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSahelIntegrationServicesForMCNotificationQueueWriterJob(this IServiceCollection services)
        {
            services.AddSingleton<IServiceRequestProvider, ServiceRequestProvider>();
            services.AddSingleton<IMCNotificationContentFactory, MCNotificationContentFactory>();
            services.AddSingleton<IMCNotificationProcessor, MCNotificationProcessor>();
            services.AddSingleton<MCNotificationQueueWriterJob>();
 
            return services;
        }

        public static IServiceCollection AddSahelIntegrationServicesForSaheRequestSubmissionJob(this IServiceCollection services)
        {
            services.AddSingleton<SahelRequestSubmissionJob>();
            services.AddSingleton<IRequestFetcher, RequestFetcher>();
             services.AddSingleton<IRequestStatusUpdater, RequestStatusUpdater>();
            services.AddSingleton<IKMIDNotificationService, KMIDNotificationService>();
            services.AddSingleton<ISahelApiClient, SahelApiClient>();

            return services;
        }

        public static IServiceCollection AddSahelIntegrationServicesForSahelNotificationsJob(this IServiceCollection services)
        {
            services.AddSingleton<SahelNotificationsJob>();
            services.AddSingleton<INotificationFetcher, NotificationFetcher>();
            services.AddSingleton<INotificationProcessor, NotificationProcessor>();

            return services;
        }

        public static IServiceCollection AddSharedSahelIntegrationServices(this IServiceCollection services)
        {
            services.AddSingleton<SahelNotificationClient>();
            return services;
        }

    }
}
