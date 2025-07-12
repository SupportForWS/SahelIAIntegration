using sahelIntegrationIA.Services.SendMCNotificationService.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Services.SendMCNotificationService
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSahelIntegrationServices(this IServiceCollection services)
        {
            services.AddSingleton<IServiceRequestProvider, ServiceRequestProvider>();
            services.AddSingleton<INotificationContentFactory, NotificationContentFactory>();
            services.AddSingleton<INotificationProcessor, NotificationProcessor>();
            services.AddSingleton<SahelNotificationClient>();
            services.AddSingleton<OldSendMCNotificationRefactoring>();
            services.AddSingleton<NewSendMcActionNotificationService>();

            return services;
        }
    }
}
