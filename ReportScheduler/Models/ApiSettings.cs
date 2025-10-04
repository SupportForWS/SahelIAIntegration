using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportScheduler.Models
{
    public class ApiSettings
    {
        public string BaseUrl { get; set; }
        public string ExternalLoginPath { get; set; }
        public string RefreshTokenPath { get; set; }
        public string ReportDetailsPath { get; set; }
        public string ExecuteReportPath { get; set; }
        public string DirectExecuteReportPath { get; set; }
        public string PendingSharesPath { get; set; }
        public string SaveSharesPath { get; set; }
        public string AccessReportPath { get; set; }
    }
}
