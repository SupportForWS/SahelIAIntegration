using Microsoft.Extensions.Hosting;
using StatisticalReportScheduler.Jobs.StatisticalReportSchedulerJob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatisticalReportScheduler
{
    internal class Worker : BackgroundService
    {

        private TimeSpan period;
        private readonly StatisticalReportSchedulerJob _statisticalReportSchedulerJob;

        public Worker(StatisticalReportSchedulerJob statisticalReportSchedulerJob)
        {
            _statisticalReportSchedulerJob = statisticalReportSchedulerJob;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            period = TimeSpan.FromSeconds(1_000);
            using PeriodicTimer timer = new PeriodicTimer(period);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                // _logger.LogInformation("New Worker running at: {time}", DateTimeOffset.Now);
                Console.WriteLine("Statistical Report Scheduler Job running at: {time}" + DateTimeOffset.Now);

                await _statisticalReportSchedulerJob.Run();
            }


        }
    }
}
