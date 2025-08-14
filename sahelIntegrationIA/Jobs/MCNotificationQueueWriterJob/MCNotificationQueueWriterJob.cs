using eServicesV2.Kernel.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Jobs.MCNotificationQueueWriterJob
{
    public class MCNotificationQueueWriterJob
    {
        private readonly IServiceRequestProvider _provider;
        private readonly IMCNotificationProcessor _processor;
        private readonly IRequestLogger _logger;
        private readonly string _jobCycleId;

        public MCNotificationQueueWriterJob(
            IServiceRequestProvider provider,
            IMCNotificationProcessor processor,
            IRequestLogger logger)
        {
            _provider = provider;
            _processor = processor;
            _logger = logger;
            _jobCycleId = Guid.NewGuid().ToString();

        }

        public async Task InsertNotificationsAsync()
        {
            _logger.LogInformation("{0} - Starting MC action notification cycle", _jobCycleId);
            var requests = await _provider.GetPendingeServiceRequestsAsync(_jobCycleId);
            await _processor.ProcessEServiceRequestsAsync(requests, _jobCycleId);
            _logger.LogInformation("{0} - Completed MC action notification cycle", _jobCycleId);
        }
    }
}
