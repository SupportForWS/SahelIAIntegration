using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportScheduler.Jobs.ReportSchedulerJob.Models
{
    public class ReportExecutionScheduleModel
    {
        public int Id { get; set; }
        public int ReportId { get; set; }
        public string ScheduleType { get; set; }
        public bool IsRecurring { get; set; }
        public DateTime ExecutionTime { get; set; }
        public int? DayOfMonth { get; set; }
        public string DaysOfWeek { get; set; }
        public string MonthsOfYear { get; set; }
        public string MonthOfYear { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public DateTime? StartDate { get; set; }
        public bool Enabled { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? LastExecutionDateTime { get; set; }

        public int ParameterHistoryId { get; set; }
        public string? SharedWith { get;  set; }
    }
}
