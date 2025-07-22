using sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs.DTO;

namespace sahelIntegrationIA
{

        #region DTO


        public class OrgCommercialAddressDTO : IServiceDto
    {
            public string RequestNumber { get; set; }
            public string OrganizationNameEnglish { get; set; }
            public string OrganizationNameArabic { get; set; }
            public string TradeLicenceNumber { get; set; }
            public string POBoxNo { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string PostalCode { get; set; }
            public string BusinessFaxNumber { get; set; }
            public string BusinessNumber { get; set; }
            public string MobileNumber { get; set; }
            public string ResidenceNumber { get; set; }
            public string Email { get; set; }
            public string WebPageAddress { get; set; }
            public string Block { get; set; }
            public string Street { get; set; }
            public string Floor { get; set; }
            public string ApartmentType { get; set; }
            public string ApartmentNumber { get; set; }
            public string EServicerequestId { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }


        }
        #endregion
    


}
