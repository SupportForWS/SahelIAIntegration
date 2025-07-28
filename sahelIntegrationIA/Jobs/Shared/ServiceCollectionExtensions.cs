

using sahelIntegrationIA.Jobs.MCNotificationQueueWriterJob;
using sahelIntegrationIA.Jobs.SahelNotificationsJob;
using sahelIntegrationIA.Jobs.SahelRequestSubmissionJob;

namespace sahelIntegrationIA.Jobs.Shared
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSahelIntegrationServicesForMCNotificationQueueWriterJob(this IServiceCollection services)
        {
            services.AddSingleton<IServiceRequestProvider, ServiceRequestProvider>();
            services.AddSingleton<IMCNotificationContentFactory, MCNotificationContentFactory>();
            services.AddSingleton<IMCNotificationProcessor, MCNotificationProcessor>();
            services.AddSingleton<sahelIntegrationIA.Jobs.MCNotificationQueueWriterJob.MCNotificationQueueWriterJob>();

            return services;
        }

        public static IServiceCollection AddSahelIntegrationServicesForSaheRequestSubmissionJob(this IServiceCollection services)
        {
            services.AddSingleton<sahelIntegrationIA.Jobs.SahelRequestSubmissionJobs.SahelRequestSubmissionJob>();
            services.AddSingleton<IRequestFetcher, RequestFetcher>();
             services.AddSingleton<IRequestStatusUpdater, RequestStatusUpdater>();
            services.AddSingleton<IKMIDNotificationService, KMIDNotificationService>();
            services.AddSingleton<ISahelApiClient, SahelApiClient>();

            return services;
        }

        public static IServiceCollection AddSahelIntegrationServicesForSahelNotificationsJob(this IServiceCollection services)
        {
            services.AddSingleton<SahelNotificationsJob.SahelSenderNotificationsJob>();
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
