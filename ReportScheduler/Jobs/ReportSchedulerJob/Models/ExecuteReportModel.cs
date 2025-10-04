using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportScheduler.Jobs.ReportSchedulerJob.Models
{
    public class ExecuteReportModel
    {
        public bool IsSampleData { get; set; }
        public int ReportID { get; set; }
        public bool AutoUpdateDates { get; set; } // New flag

    }
}
