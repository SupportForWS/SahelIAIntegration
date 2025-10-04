using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportScheduler.Jobs.ReportSchedulerJob.DTOs
{

    public class ReportShareDTO
    {
        public int Id { get; set; }
        public int ReportParameterId { get; set; }
        public string Token { get; set; }
        public string Email { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AccessedAt { get; set; }
        public bool IsRevoked { get; set; }
    }
}
