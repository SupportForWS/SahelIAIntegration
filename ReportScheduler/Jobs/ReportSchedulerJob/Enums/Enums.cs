using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportScheduler.Jobs.ReportSchedulerJob.Enums
{
    public class Types
    {
        public enum SearchSubject
        {
            FromHeader = 0,
            ToHeader = 1,
            FromHSCode = 2,
            ToHSCode = 3,
            FromSection = 4,
            ToSection = 5,
            DeclarationType = 6,
            Country = 7,
            CustomsCenter = 8,
            Consignee = 9,
            Users = 10,
            Groups = 11,
            CountryGroups = 12,
            CustomsCentersTransitImportBills = 13,
            Duties = 14,
            TransitImportBillsDeclarationTypes = 15
        }

        public enum ReportParametersEnum
        {
            SearchCriteria = 1,
            ResultFields = 2,
            All = 3
        }

        public enum ReportTypesEnum
        {

        }
        public enum TextSearchCriterias
        {
            Contains = 1,
            Equal = 2
        }
        public enum ReportTypes
        {
            DetailedReport = 1,
            SummaryReport = 2
        }

        ////////

        public enum StatisticalReportType
        {
            None = 0,
            DeclarationDetailsReport = 1,
            ImportExportReportByHSCode = 2,
            ImportExportReportByCustomsCenter = 3,
            TradeExchangeReport = 4,
            DeclarationDetailsCounts = 5,
            ImpExpByHSCodeCounts = 6,
            ImpExpByCustomsCenterCounts = 7,
            TradeExchangeCounts = 8,
            GeneralDetailedStatisticalReport = 9,
            GeneralDetailedStatisticalReportCounts = 10,
            GeneralSummaryStatisticalReport = 11,
            GeneralSummaryStatisticalReportCounts = 12
        }

        public enum Screens //these can be sync with the database screens ids
        {
            TradeExchangeReport = 1,
            ImportExportReportByHSCode = 2,
            ImportExportReportByCustomsCenter = 3,
            DeclarationDetailsReport = 4,
            ChangePassword = 5,
            AddEditUser = 6,
            SearchForm = 7,
            UserLoginReport = 8,
            UserActivityReport = 9,
            ChangeUserPermissions = 10,
            ChangeUserSearchPermissions = 11,
            Main = 12,
            ScheduleReports = 13,
            SQLServerReports = 14,
            MyScheduledReports = 15,
            CountryGroups = 15,
            EnableCustomsSystemUsers = 16,
            GeneralStatisticalReport = 17,
            None = 0
        }


        //1 & 3 are the actual values for Trade Direction used to get the declaration Types list in "Declaration Types" lookup. It is different from enum :TradeDirection
        /// <summary>
        /// Stores Import Export Direction values for Declaration Types
        /// </summary>
        public enum BayanTypeDirection
        {
            Import = 1,
            Export = 3,
            None = 0
        }

        public enum SearchType
        {
            Like = 1,
            StartsWith = 2
        }

        public enum ReportSortOnCountry
        {
            CountryOfOrigin = 1,
            CountryOfExport = 2,
            None = 0
        }

        public enum StatsForCountry
        {
            AllCountries = 1,
            AllCountriesExceptGCC = 2,
            GCCCountries = 3,
            SpecificCountry = 4,
            GroupOfCountries = 5
        }

        public enum HSCodeRangeControlParent
        {
            ReportPage = 1,
            HSCodePermissions = 2
        }

        public enum ReportGroupedOnTariff
        {
            Section = 1,
            Header = 2,
            HSCode = 3,
            None = 0
        }

        //1 & 2 are the actual values for Trade Direction (Import and Export). This is used in Stored procedures also so cant use enum:BayanTypeDirection for this case.
        /// <summary>
        /// Stores Import Export Direction values for General Use.
        /// </summary>
        public enum TradeDirection
        {
            Import = 1,
            Export = 2
        }

        public enum ReportOptionalField
        {
            Month = 1,
            CountryOfExport = 2,
            CountryOfOrigin = 3,
            HSCodeDescription = 4,
            DeclarationType = 5
        }

        public enum UserReports
        {
            UserLoginReport = 1,
            UserActivityReport = 2
        }

        public enum ShowLookup
        {
            Key = 1,
            Text = 2
        }

        public enum UserActions
        {
            NONE = 0,
            OpenScreen = 1,
            SaveAndPrint = 2,
            Save = 3,
            Print = 4,
            EditVehicleInfo = 5,
            EditDriverInfo = 6,
            DeleteRecord = 7,
            Edit = 8,
            DeleteRow = 9,
            Retrieve = 10,
            RePrint = 11,
            AddDriverFromComeInScreen = 12,
            AddVehicleFromComeInScreen = 13,
            //SaveAndPrint = 14,
            //SaveAndPrint = 15,
            Search_I = 16,
            Search_E = 17,
            LogIn = 18,
            LogOut = 19,
            RetrieveQuery_I = 20,
            RetrieveQuery_E = 21,
            Schedule = 22
        }

        public enum SchedulingType
        {
            MonthlyAbsolute = 1,
            MonthlyRelative = 2
        }

        public enum RelativeDateRange
        {
            CurrentYear = 1,
            LastMonth = 2,
            CurrentQuarter = 3,
            LastYear = 4,
            ReportDates = 5
        }

        public enum SavedParametersFetching
        {
            FromUserLog = 1,
            FromScheduledReports = 2
        }

    }


    public enum ScheduleTypeEnum
    {
        [Description("Once")]
        O = 0,

        [Description("Daily")]
        D = 1,

        [Description("Weekly")]
        W = 2,

        [Description("Monthly")]
        M = 3,

        [Description("Yearly")]
        Y = 4
    }
    public enum WeekDayEnum
    {
        Sunday = 1,
        Monday = 2,
        Tuesday = 3,
        Wednesday = 4,
        Thursday = 5,
        Friday = 6,
        Saturday = 7
    }
    public enum MonthEnum
    {
        January = 1,
        February = 2,
        March = 3,
        April = 4,
        May = 5,
        June = 6,
        July = 7,
        August = 8,
        September = 9,
        October = 10,
        November = 11,
        December = 12
    }
}
