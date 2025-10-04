using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportScheduler.Jobs.ReportSchedulerJob.Models
{
    public class ReportSettings
    {
        public string FolderPath { get; set; }
        public int RetryCount { get; set; } = 3;
        public string? FileServerUserName { get; internal set; }
    }

}
