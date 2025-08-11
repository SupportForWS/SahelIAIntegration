using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportScheduler.Jobs.ReportSchedulerJob.DTOs
{
    public class DownloadFileDTO
    {
        public string File { get; set; }
        public string ContentType { get; set; }
        public string FileName { get;  set; }
        public string FileExtension { get;  set; }
    }

    public class ResponseDto
    {
        public DownloadFileDTO DownloadFileDTO { get; set; }
        public DynamicReportResult DynamicReportResult { get; set; }
    }
    public class DynamicReportResult
    {
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, object>> Data { get; set; } = new();
        public string fileUrl { get; set; }
    }
}
