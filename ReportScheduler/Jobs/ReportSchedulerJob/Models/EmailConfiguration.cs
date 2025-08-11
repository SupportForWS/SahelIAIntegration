using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportScheduler.Jobs.ReportSchedulerJob.Models
{
    public class EmailConfiguration
    {
        public string ExchangeServer { get; set; }
        public int Port { get; set; }
        public string FromEmail { get; set; }
        public string FromPassword { get; set; }
        public bool EnableSendingMail { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string DomainName { get; set; }
        public int MaxAttempts { get; set; }
    }

}
