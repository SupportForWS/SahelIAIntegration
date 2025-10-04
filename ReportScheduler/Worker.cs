using ReportScheduler.Jobs.ReportSchedulerJob;

namespace ReportScheduler
{
    public class Worker : BackgroundService
    {

        private readonly ReportSchedulerJob _job;
        private readonly ILogger<Worker> _logger;

        public Worker(ReportSchedulerJob job, ILogger<Worker> logger)
        {
            _job = job;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Starting report scheduler job execution at {Time}", DateTime.Now);
                }

                await _job.Run();

                await Task.Delay(1000, stoppingToken);
            }
        }


    }
}
