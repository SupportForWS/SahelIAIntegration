using eServicesV2.Kernel.Core.Logging;
using sahelIntegrationIA.Jobs.SendMCNotificationService.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Jobs.SendMCNotificationService
{
    //rename queuing
    public class OldSendMCNotificationRefactoring
    {
        private readonly IServiceRequestProvider _provider;
        private readonly INotificationProcessor _processor;
        private readonly IRequestLogger _logger;

        public OldSendMCNotificationRefactoring(
            IServiceRequestProvider provider,
            INotificationProcessor processor,
            IRequestLogger logger)
        {
            _provider = provider;
            _processor = processor;
            _logger = logger;
        }

        public async Task InsertNotificationsAsync()
        {
            _logger.LogInformation("Starting MC action notification cycle");
            var requests = await _provider.GetPendingeServiceRequestsAsync();
            await _processor.ProcessEServiceRequestAsync(requests);
            _logger.LogInformation("Completed MC action notification cycle");
        }
    }
}
