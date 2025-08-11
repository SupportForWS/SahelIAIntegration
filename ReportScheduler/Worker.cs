using ReportScheduler.Jobs.ReportSchedulerJob;

namespace ReportScheduler
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ReportSchedulerJob _reportSchedulerJob;
        public Worker(ILogger<Worker> logger, ReportSchedulerJob reportSchedulerJob)
        {
            _logger = logger;
            _reportSchedulerJob = reportSchedulerJob;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }

                await _reportSchedulerJob.Run();

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
