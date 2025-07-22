using eServices.APIs.UserApp.OldApplication.Models;
using eServicesV2.Kernel.Domain.Entities.OrganizationEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using sahelIntegrationIA.Models;
using sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 
namespace sahelIntegrationIA.Jobs.sahelIntegrationIA.Jobs
{
    public static class SahelDtoFactory
    {
        public static IServiceDto CreateDto(ServiceRequest r) => r.ServiceId switch
        {
            (int)ServiceTypesEnum.NewImportLicenseRequest => GetAddLicenseLicenseDTO(r),
            (int)ServiceTypesEnum.ImportLicenseRenewalRequest => GetRenewLicenseDTO(r),
            (int)ServiceTypesEnum.AddNewAuthorizedSignatoryRequest => GetAuthorizedSignatoryDto(r),
            (int)ServiceTypesEnum.RenewAuthorizedSignatoryRequest => GetReNewAuthorizedSignatoryDTO(r),
            (int)ServiceTypesEnum.RemoveAuthorizedSignatoryRequest => GetRemoveAuthorizedSignatoryDTO(r),
            (int)ServiceTypesEnum.CommercialLicenseRenewalRequest => GetCreateRenewCommercialLicenseRequestDTO(r),
            (int)ServiceTypesEnum.IndustrialLicenseRenewalRequest => GetRenewIndustrialLicenseDto(r),
            (int)ServiceTypesEnum.ChangeCommercialAddressRequest => GetOrgCommercialAddressDTO(r),
            (int)ServiceTypesEnum.OrgNameChangeReqServiceId => GetOrganizationChangeNameDTO(r),
            (int)ServiceTypesEnum.ConsigneeUndertakingRequest => GetConsigneeUndertakingRequestDTO(r),
            (int)ServiceTypesEnum.OrganizationRegistrationService => GetOrganizationRegistrationRequestDTO(r),
            _ => throw new ArgumentException($"Invalid service ID: {r.ServiceId}", nameof(r.ServiceId))
        };


        #region DTOs

        private static  CreateRenewImportLicenseDTO GetRenewLicenseDTO(ServiceRequest serviceRequest)
        {
            return new CreateRenewImportLicenseDTO
            {
                CommercialLicenseNo = serviceRequest.ServiceRequestsDetail.CommercialLicenseNo,
                eServiceRequestId = serviceRequest.EserviceRequestId.ToString(),
                IndustrialLicenseNo = serviceRequest.ServiceRequestsDetail.IndustrialLicenseNo,
                LicenseExpiryDate = serviceRequest.ServiceRequestsDetail.LicenseExpiryDate.Value,
                LicenseIssueDate = serviceRequest.ServiceRequestsDetail.LicenseIssueDate.Value,
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer,

                //TODO: check this
                ImporterLicenseTypeDesc = serviceRequest.ServiceRequestsDetail.ImporterLicenseTypeDesc,

                ImporterLicenseNo = serviceRequest.ServiceRequestsDetail.ImporterLicenseNo,
                ImporterLicenseType =
                int.Parse(serviceRequest.ServiceRequestsDetail.ImporterLicenseType),
                LicenseType = serviceRequest.ServiceRequestsDetail.ImporterLicenseType,
                LicenseTypeDesc = serviceRequest.ServiceRequestsDetail.ImporterLicenseTypeDesc,
                //TypeOfLicenseRequest = serviceRequest.ServiceRequestsDetail.type
            };

        }

        private static  CreateAddNewImportLicenseDTO GetAddLicenseLicenseDTO(ServiceRequest serviceRequest)
        {
            return new CreateAddNewImportLicenseDTO
            {
                ImporterLicenseNo = serviceRequest.ServiceRequestsDetail.ImporterLicenseNo,
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }

        private static  RenewIndustrialLicenseDto GetRenewIndustrialLicenseDto(ServiceRequest serviceRequest)
        {
            return new RenewIndustrialLicenseDto
            {
                CommercialLicenseNumber = serviceRequest.ServiceRequestsDetail.CommercialLicenseNo,
                ImportLicenseNumber = serviceRequest.ServiceRequestsDetail.ImporterLicenseNo,
                LicenseExpiryDate = serviceRequest.ServiceRequestsDetail.LicenseExpiryDate.Value,
                LicenseIssueDate = serviceRequest.ServiceRequestsDetail.LicenseExpiryDate.Value,
                //todo: check
                LicenseNumber = serviceRequest.ServiceRequestsDetail.IndustrialLicenseNo,
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }

        private static  CreateRenewCommercialLicenseRequestDTO GetCreateRenewCommercialLicenseRequestDTO(ServiceRequest serviceRequest)
        {
            return new CreateRenewCommercialLicenseRequestDTO
            {
                CommercialLicenseNumber = serviceRequest.ServiceRequestsDetail.CommercialLicenseNo,
                ImportLicenseNumber = serviceRequest.ServiceRequestsDetail.ImporterLicenseNo,
                LicenseExpiryDate = serviceRequest.ServiceRequestsDetail.LicenseExpiryDate.Value,
                LicenseIssueDate = serviceRequest.ServiceRequestsDetail.LicenseExpiryDate.Value,
                IndustrialLicenseNumber = serviceRequest.ServiceRequestsDetail.IndustrialLicenseNo,
                ImportLicenseType = serviceRequest.ServiceRequestsDetail.ImporterLicenseType,
                ImportLicenseTypeDesc = serviceRequest.ServiceRequestsDetail.ImporterLicenseTypeDesc,
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer

            };

        }

        private static  AuthorizedSignatoryDto GetAuthorizedSignatoryDto(ServiceRequest serviceRequest)
        {
            return new AuthorizedSignatoryDto
            {
                AuthorizedSignatoryCivilIdExpiryDate = serviceRequest.ServiceRequestsDetail.AuthorizedSignatoryCivilIdExpiryDate.HasValue ? serviceRequest.ServiceRequestsDetail.AuthorizedSignatoryCivilIdExpiryDate.Value : DateTime.Now,
                CivilId = serviceRequest.ServiceRequestsDetail.CivilId,
                EServiceRequestId = serviceRequest.EserviceRequestId.ToString(),
                AuthPerson = serviceRequest.ServiceRequestsDetail.AuthorizedPerson,
                ExpiryDate = serviceRequest.ServiceRequestsDetail.ExpiryDate.Value,
                IssueDate = serviceRequest.ServiceRequestsDetail.IssueDate.Value,
                NationalityId = serviceRequest.ServiceRequestsDetail.Nationality,
                OrganizationId = serviceRequest.ServiceRequestsDetail.OrganizationId.Value.ToString(),
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }

        private static  RemoveAuthorizedSignatoryDTO GetRemoveAuthorizedSignatoryDTO(ServiceRequest serviceRequest)
        {
            return new RemoveAuthorizedSignatoryDTO
            {
                AssociatedAuthorizedSignatories = new()
                {
                    AssociatedPersonName = serviceRequest.ServiceRequestsDetail.AuthorizedPerson,
                    CivilIdNo = serviceRequest.ServiceRequestsDetail.CivilId
                },
                EServicerequestId = serviceRequest.EserviceRequestId.ToString(),
                OrganizationNameArabic = serviceRequest.ServiceRequestsDetail.OldOrgAraName,
                OrganizationNameEnglish = serviceRequest.ServiceRequestsDetail.OldOrgEngName,
                //todo: check
                TradeLicenceNumber = serviceRequest.ServiceRequestsDetail.LicenseNumber,
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }

        private static  ReNewAuthorizedSignatoryDTO GetReNewAuthorizedSignatoryDTO(ServiceRequest serviceRequest)
        {
            return new ReNewAuthorizedSignatoryDTO
            {
                AuthorizedSignatory = new()
                {
                    //TODO: check
                    CivilId = serviceRequest.ServiceRequestsDetail.CivilId,
                    CivilIdExpiryDate = serviceRequest.ServiceRequestsDetail.AuthorizedSignatoryCivilIdExpiryDate.HasValue ? serviceRequest.ServiceRequestsDetail.AuthorizedSignatoryCivilIdExpiryDate.Value : DateTime.Now,
                    //serviceRequest.ServiceRequestsDetail.AuthorizedSignatoryCivilIdExpiryDate.Value,
                    ExpiryDate = serviceRequest.ServiceRequestsDetail.ExpiryDate.Value,
                    IssueDate = serviceRequest.ServiceRequestsDetail.IssueDate.Value,
                    Name = serviceRequest.ServiceRequestsDetail.AuthorizedPerson
                },
                EServiceRequestId = serviceRequest.EserviceRequestId.ToString(),
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }

        private static  OrgCommercialAddressDTO GetOrgCommercialAddressDTO(ServiceRequest serviceRequest)
        {
            return new OrgCommercialAddressDTO
            {
                Address = serviceRequest.ServiceRequestsDetail.Address,
                ApartmentNumber = serviceRequest.ServiceRequestsDetail.ApartmentNumber,
                ApartmentType = serviceRequest.ServiceRequestsDetail.ApartmentType,
                Block = serviceRequest.ServiceRequestsDetail.Block,
                BusinessFaxNumber = serviceRequest.ServiceRequestsDetail.BusiFaxNo,
                BusinessNumber = serviceRequest.ServiceRequestsDetail.BusiNo,
                City = serviceRequest.ServiceRequestsDetail.City,
                Email = serviceRequest.ServiceRequestsDetail.Email,
                EServicerequestId = serviceRequest.EserviceRequestId.ToString(),
                Floor = serviceRequest.ServiceRequestsDetail.Floor,
                MobileNumber = serviceRequest.ServiceRequestsDetail.MobileNo,
                OrganizationNameArabic = serviceRequest.ServiceRequestsDetail.OldOrgAraName,
                OrganizationNameEnglish = serviceRequest.ServiceRequestsDetail.OldOrgEngName,
                POBoxNo = serviceRequest.ServiceRequestsDetail.PoboxNo,
                PostalCode = serviceRequest.ServiceRequestsDetail.PostalCode,
                ResidenceNumber = serviceRequest.ServiceRequestsDetail.ResidenceNo,
                State = serviceRequest.ServiceRequestsDetail.State,
                Street = serviceRequest.ServiceRequestsDetail.Street,
                TradeLicenceNumber = serviceRequest.ServiceRequestsDetail.LicenseNumber,
                WebPageAddress = serviceRequest.ServiceRequestsDetail.WebPageAddress,
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }

        private static  OrganizationChangeNameDTO GetOrganizationChangeNameDTO(ServiceRequest serviceRequest)
        {
            var obj = new OrganizationChangeNameDTO
            {
                //todo: check
                AddNewImportLicenseRequest = false,

                CommercialLicenseNumber = serviceRequest.ServiceRequestsDetail.CommercialLicenseNo,
                eServiceRequestId = serviceRequest.EserviceRequestId.ToString(),
                ImporterLicenseNo = serviceRequest.ServiceRequestsDetail.ImporterLicenseNo,


                //not used
                //  organizationName =,

                OrganizationNewArabicName = serviceRequest.ServiceRequestsDetail.NewOrgAraName,
                OrganizationNewEnglishName = serviceRequest.ServiceRequestsDetail.NewOrgEngName,
                OrganizationOldArabicName = serviceRequest.ServiceRequestsDetail.OldOrgAraName,
                OrganizationOldEnglishName = serviceRequest.ServiceRequestsDetail.OldOrgEngName,
                TradingLicenseNo = serviceRequest.ServiceRequestsDetail.LicenseNumber,
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

            //todo check
            //if (!string.IsNullOrWhiteSpace(serviceRequest.ServiceRequestsDetail.ImporterLicenseType))
            //{
            //    obj.ImportLicenseType = int.Parse(serviceRequest.ServiceRequestsDetail.ImporterLicenseType);
            //    obj.ImporterLicenseType = int.Parse(serviceRequest.ServiceRequestsDetail.ImporterLicenseType);
            //    obj.IssueDate = null;
            //    obj.LicenseNo = null;
            //    obj.ExpiryDate = null;
            //}
            return obj;
        }

        private static  ConsigneeUndertakingRequestDTO GetConsigneeUndertakingRequestDTO(ServiceRequest serviceRequest)
        {
            return new ConsigneeUndertakingRequestDTO
            {
                //todo: check ConsigneeName is org name
                //  ConsigneeName= serviceRequest.ServiceRequestsDetail.OldOrgAraName,
                Eservicerequestid = serviceRequest.EserviceRequestId.ToString(),
                TradeLicenceNumber = serviceRequest.ServiceRequestsDetail.LicenseNumber,
                RequestNo = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }

        private static  SubmitOrganizationRequestDTO GetOrganizationRegistrationRequestDTO(ServiceRequest serviceRequest)
        {
            //todo check
            //var orgabizationRequestId = eServicesContext
            //                .Set<OrganizationRequests>()
            //                .Where(a => a.RequestNumber == serviceRequest.EserviceRequestNumber)
            //                .Select(a => a.OrganizationRequestId)
            //                .FirstOrDefault();
            return new SubmitOrganizationRequestDTO
            {
                OrganizationRequestId = CommonFunctions.CsUploadEncrypt(serviceRequest.EserviceRequestNumber)

            };

        }


        #endregion
    }
}
