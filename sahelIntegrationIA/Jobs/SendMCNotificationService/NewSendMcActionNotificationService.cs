using eServicesV2.Kernel.Core.Logging;
using sahelIntegrationIA.Jobs.SendMCNotificationService.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Jobs.SendMCNotificationService
{
    // 4. Orchestrator
    public class NewSendMcActionNotificationService
    {
        private readonly IServiceRequestProvider _provider;
        private readonly INotificationProcessor _processor;
        private readonly IRequestLogger _logger;

        public NewSendMcActionNotificationService(
            IServiceRequestProvider provider,
            INotificationProcessor processor,
            IRequestLogger logger)
        {
            _provider = provider;
            _processor = processor;
            _logger = logger;
        }

        //public async Task SendNotificationsAsync()
        //{
        //    _logger.LogInformation("Starting MC action notification cycle");
        //    var requests = await _provider.GetPendingRequestsAsync();
        //    await _processor.ProcessAsync(requests);
        //    _logger.LogInformation("Completed MC action notification cycle");
        //}

        public async Task SendNotificationsAsync()
        {
            _logger.LogInformation("Starting MC action notification cycle");
            var notifications = await _provider.GetPendingNotificationsAsync();
            await _processor.ProcessNotificatonsAsync(notifications);
            _logger.LogInformation("Completed MC action notification cycle");
        }

        //GetPendingNotificationsAsync
    }
}
