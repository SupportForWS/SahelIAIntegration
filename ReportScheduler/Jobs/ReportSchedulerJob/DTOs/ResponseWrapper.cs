using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportScheduler.Jobs.ReportSchedulerJob.DTOs
{
    public class ResponseWrapper<T>
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }

    }
}
