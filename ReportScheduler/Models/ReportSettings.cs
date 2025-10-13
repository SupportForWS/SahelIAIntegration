using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportScheduler.Models
{
    public class ReportSettings
    {
         public int RetryCount { get; set; } = 3;
        public string FolderPath { get; set; }
    }


    public class ReportStorageSettings
    {
        public bool UseRemoteServer { get; set; }
        public string LocalPath { get; set; }
        public string RemoteServerPath { get; set; }
        public string ApiUploadUri { get; set; }
        public string ApiUsername { get; set; }
        public string ApiOwnerOrgId { get; set; }
        public string ApiOwnerLocId { get; set; }
        public string ApiSaltKey { get; set; }
        public string ApiDocumentType { get; set; }
    }
}
