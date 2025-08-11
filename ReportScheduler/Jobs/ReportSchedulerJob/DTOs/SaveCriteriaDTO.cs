using ReportScheduler.Jobs.ReportSchedulerJob.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ReportScheduler.Jobs.ReportSchedulerJob.Enums.Types;

namespace ReportScheduler.Jobs.ReportSchedulerJob.DTOs
{
    public class SaveCriteriaDTO
    {
        public int? UserID { get; set; }
        public string? Name { get; set; }
        public bool Override { get; set; }
        public int? Serial { get; set; }
        public ReportParametersEnum? Flag { get; set; }
        // if 1 means Detailed (تفصيلي) if 2 means summary (تجميعي)
        public int? ReportType { get; set; }

        public int? TradeType { get; set; }
        public int? Year { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? CustomsCenter { get; set; }
        public string? DeclarationType { get; set; }
        public string? DeclarationNumber1 { get; set; }
        public string? DeclarationNumber2 { get; set; }
        public string? DeclarationNumberSet { get; set; }
        public string? SourceCountry { get; set; }
        public string? DestinationCountry { get; set; }
        public string? OriginCountry { get; set; }
        public string? CountryGroup { get; set; }
        public string? CountryGroupFlag { get; set; }
        public string? ExpCompany1 { get; set; }
        public string? ExpCompany2 { get; set; }
        public string? ExpCompany3 { get; set; }
        public TextSearchCriterias? ExpCompanyTextSearchCriteria { get; set; }
        public string? Broker1 { get; set; }
        public string? Broker2 { get; set; }
        public string? Broker3 { get; set; }
        public TextSearchCriterias? BrokerTextSearchCriteria { get; set; }
        public string? BrokerLicense1 { get; set; }
        public string? BrokerLicense2 { get; set; }
        public string? BrokerLicense3 { get; set; }
        public TextSearchCriterias? BrokerLicenseTextSearchCriteria { get; set; }
        public string? Section { get; set; }
        public string? Chapter { get; set; }
        public string? HSCodeFrom { get; set; }
        public string? HSCodeTo { get; set; }
        public string? HSCode { get; set; }
        public string? ExcludeHSCode { get; set; }
        public string? HSCodeDesc1 { get; set; }
        public string? HSCodeDesc2 { get; set; }
        public string? HSCodeDesc3 { get; set; }
        public TextSearchCriterias? HSCodeDescriptionTextSearchCriteria { get; set; }
        public string? DataEntryDesc1 { get; set; }
        public string? DataEntryDesc2 { get; set; }
        public string? DataEntryDesc3 { get; set; }
        public TextSearchCriterias? DataEntryDescriptionSearchTextCriteria { get; set; }
        public string? CompanyLicenseNo1 { get; set; }
        public string? CompanyLicenseNo2 { get; set; }
        public string? CompanyLicenseNo3 { get; set; }
        public TextSearchCriterias? CompanyLicenseNoTextSearchCriteria { get; set; }
        public string? ExportedTo1 { get; set; }
        public string? ExportedTo2 { get; set; }
        public string? ExportedTo3 { get; set; }
        public TextSearchCriterias? ExportedToTextSearchCriteria { get; set; }
        public string? ExcludeSection { get; set; }
        public string? Manufacturer1 { get; set; }
        public string? Manufacturer2 { get; set; }
        public string? Manufacturer3 { get; set; }
        public TextSearchCriterias? ManufacturerTextSearchCriteria { get; set; }
        public string? TempDeclarationNo1 { get; set; }
        public string? TempDeclarationNo2 { get; set; }
        public string? TempDeclarationNo3 { get; set; }
        public TextSearchCriterias? TempDeclarationNoTextSearchCriteria { get; set; }
        public string? BilOfLading1 { get; set; }
        public string? BilOfLading2 { get; set; }
        public string? BilOfLading3 { get; set; }
        public TextSearchCriterias? BilOfLadingTextSearchCriteria { get; set; }
        public string? InvoiceNumber1 { get; set; }
        public string? InvoiceNumber2 { get; set; }
        public string? InvoiceNumber3 { get; set; }
        public TextSearchCriterias? InvoiceNumberTextSearchCriteria { get; set; }



        /// <summary>
        /// /
        /// </summary>
        public int? StatisticalReportType { get; set; }
        public string? DetailedReportOutput { get; set; }
        public bool WeightPerPiece { get; set; }
        public bool PricePerKilo { get; set; }
        public bool PricePerPiece { get; set; }
        public string? SummaryReportGroupBy { get; set; }
        public string? SummeryReportAggregate { get; set; }
        public string? DeclarationChoice { get; set; }

        /// <summary>
        ///
        /// </summary>
        public bool IsSampleData { get; set; }


        /// <summary>
        ///
        /// </summary>
        public bool IsShareCriteriaEnabled { get; set; }
        public string? SharedWith { get; set; }
        public bool IsScheduled { get; set; }
        public ScheduleTypeEnum Schedule { get; set; } //ScheduleType
        public List<WeekDayEnum> SelectedWeekDays { get; set; } = new();
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Time { get; set; }
        public List<MonthEnum> SelectedMonths { get; set; } = new();


    }

}
