using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Core.Persistence;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Models;
using System.Data;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;
using System.Text.RegularExpressions;
using eServicesV2.Kernel.Core;

namespace sahelIntegrationIA
{
    public class VerificationServiceForOrganizationServices
    {
        private readonly IRequestLogger _logger;
        private TimeSpan period;
        private readonly IBaseConfiguration _configurations;
        private readonly eServicesContext _eServicesContext;
        private readonly IRequestLogger _requestLogger;
        private readonly IDapper _dapper;
        private readonly SahelConfigurations _sahelConfigurations;
        public VerificationServiceForOrganizationServices(IRequestLogger logger,
            IBaseConfiguration configuration, eServicesContext eServicesContext, IRequestLogger requestLogger, IDapper dapper, SahelConfigurations sahelConfigurations)
        {
            _logger = logger;
            _configurations = configuration;
            _eServicesContext = eServicesContext;
            _requestLogger = requestLogger;
            _dapper = dapper;
            _sahelConfigurations = sahelConfigurations;
        }
        public async Task<List<ServiceRequest>> GetRequestList()
        {
            string[] statusEnums = new string[]
            {
                nameof(ServiceRequestStatesEnum.EServiceRequestORGCreatedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestORGForAdditionalInfo),
                nameof(ServiceRequestStatesEnum.EServiceRequestORGRejectedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestCreatedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestRejectedState),
            };

            int[] serviceIds = new int[]
            {
                (int)ServiceTypesEnum.NewImportLicenseRequest,
                (int)ServiceTypesEnum.ImportLicenseRenewalRequest,
            };

            var requestList = await _eServicesContext
                               .Set<ServiceRequest>()
                               .Include(p => p.ServiceRequestsDetail)
                               .Where(p => statusEnums.Contains(p.StateId)
                                           && p.RequestSource == "Sahel"
                                           && !string.IsNullOrEmpty(p.ServiceRequestsDetail.KMIDToken)
                                           && serviceIds.Contains((int)p.ServiceId.Value))
                                .ToListAsync();

            var kmidCreatedList = requestList
                .Select(a => a.ServiceRequestsDetail.KMIDToken)
                .ToList();

            var kmidStrings = kmidCreatedList
                .Select(k => k.ToString())
                .ToList();

            var currentTime = DateTime.Now;

            var expiredKmidRequests = await _eServicesContext.Set<KGACPACIQueue>()
                .Where(a => kmidStrings.Contains(a.KGACPACIQueueId.ToString())
                            && a.DateCreated.AddSeconds(_sahelConfigurations.OrganizationKMIDCallingTimer) < currentTime)
                .Select(a => a.KGACPACIQueueId)
                .ToListAsync();

            var filteredRequestList = requestList
                .Where(request => !expiredKmidRequests.Contains(int.Parse(request.ServiceRequestsDetail.KMIDToken)))
                .ToList();

            return filteredRequestList;
        }

        public async Task CreateRequestObjectDTO()
        {
            var serviceRequest = await GetRequestList();
            var exceptions = new List<Exception>();

            var tasks = serviceRequest.Select(async serviceRequest =>
            {
                try

                {
                    await ProcessServiceRequest(serviceRequest);
                }

                catch (Exception ex)

                {
                    _logger.LogException(ex, "Sahel-Services");
                    exceptions.Add(ex); // Collect exceptions

                }

            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);


            // If there are any exceptions, handle them here
            if (exceptions.Any())

            {
                //   throw new AggregateException("One or more exceptions occurred during processing.", exceptions);
            }

        }


        private async Task ProcessServiceRequest(ServiceRequest serviceRequest)
        {
            if (serviceRequest.ServiceId is null or 0)
            {
                _logger.LogException(new ArgumentException($"INVALID SERVICE ID {nameof(serviceRequest.ServiceId)}"));
                return; //log the error
            }

            if (!Enum.IsDefined(typeof(ServiceTypesEnum), (int)serviceRequest.ServiceId))
            {
                _logger.LogException(new ArgumentException($"INVALID SERVICE ID {nameof(serviceRequest.ServiceId)}"));
                return; //log the error
            }

            // Create DTO and call api
            await ProcessServiceRequestAndCallAPI(serviceRequest);

        }

        private async Task ProcessServiceRequestAndCallAPI(ServiceRequest serviceRequest)
        {
            string url;
            switch ((ServiceTypesEnum)serviceRequest.ServiceId)

            {
                case ServiceTypesEnum.NewImportLicenseRequest:
                    url = _sahelConfigurations.EservicesUrlsConfigurations.AddNewImportLicenseUrl;
                    await CallServiceAPI(GetAddLicenseLicenseDTO(serviceRequest), url);
                    break;

                case ServiceTypesEnum.ImportLicenseRenewalRequest:
                    url = _sahelConfigurations.EservicesUrlsConfigurations.ReNewImportLicenseUrl;
                    await CallServiceAPI(GetRenewLicenseDTO(serviceRequest), url);
                    break;

                default:
                    _logger.LogException(new ArgumentException($"INVALID SERVICE ID {nameof(serviceRequest.ServiceId)}"));
                    break; //log the error
            }

        }


        private async Task CallServiceAPI<T>(T serviceDTO, string apiUrl)
        {
            using (var httpClient = new HttpClient())

            {
                string json = JsonConvert.SerializeObject(serviceDTO);

                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                var httpResponse = await httpClient.PostAsync(apiUrl, httpContent);

                string responseContent = await httpResponse.Content.ReadAsStringAsync();

                Notification notification = JsonConvert.DeserializeObject<Notification>(responseContent);

                PostNotification(notification, "Individual");
            }

        }

        #region Private Methods
        public void PostNotification(Notification notification, string SahelOption = "Business")
        {
            if (string.IsNullOrEmpty(notification.bodyAr) && string.IsNullOrEmpty(notification.bodyAr))
            {
                return;
            }
            string notificationString = JsonConvert.SerializeObject(notification);
            _logger.LogInformation($"NotificationBody-->{notificationString}");
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = //SecurityProtocolType.Tls12;
            SecurityProtocolType.Tls12 |
            SecurityProtocolType.Tls11 |
            SecurityProtocolType.SystemDefault;

            string TargetURL = string.Empty;
            TargetURL = SahelOption == SahelOptionsTypesEnum.Individual.ToString() ?
                                    _configurations.IndividualAuthorizationSahelConfiguration.TargetUrlIndividual
                                                                            : _configurations.IndividualAuthorizationSahelConfiguration.TargetUrlBusiness;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(TargetURL);
                string token = GenerateToken(SahelOption);
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    //HTTP POST //single to be modified as enum
                    //HTTP POST //single to be modified as enum
                    Task<HttpResponseMessage> postTask = client.PostAsJsonAsync<Notification>("single", notification);
                    /*                    var notificationString = JsonConvert.SerializeObject(notification);
                                        _logger.LogInformation(notificationString);*/
                    String rEsult = getResult(postTask);
                }
            }

        }

        public string GenerateToken(string SahelOption)
        {
            var confi = _configurations.IndividualAuthorizationSahelConfiguration;
            String token = String.Empty;

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol =
            SecurityProtocolType.Tls12 |
            SecurityProtocolType.Tls11 |
            SecurityProtocolType.SystemDefault;

            using (HttpClient client = new HttpClient())
            {
                string TokenTargetURL = SahelOption == SahelOptionsTypesEnum.Individual.ToString() ?
                        _configurations.IndividualAuthorizationSahelConfiguration.TargetURLToken
                        : _configurations.IndividualAuthorizationSahelConfiguration.TargetURLTokenBusiness;

                string username = _configurations.IndividualAuthorizationSahelConfiguration.UsernamePaci;
                string password = SahelOption == SahelOptionsTypesEnum.Individual.ToString() ?
                                    _configurations.IndividualAuthorizationSahelConfiguration.passwordIndividual
                                    : _configurations.IndividualAuthorizationSahelConfiguration.passwordBusiness;

                client.BaseAddress = new Uri(TokenTargetURL);
                authenticate authenticate = new authenticate()
                {
                    username = username, // "kgac",
                    password = password
                };

                Task<HttpResponseMessage> postTask =
                    client.PostAsJsonAsync<authenticate>("generate", authenticate);
                string result = getResult(postTask);
                if (string.IsNullOrEmpty(result))
                {
                    //catch exception
                    return null;
                }
                TokenResult tokenResult = JsonConvert.DeserializeObject<TokenResult>(result);
                token = tokenResult.accessToken;

                return token;
            }
        }

        private String getResult(Task<HttpResponseMessage> postTask)
        {
            try
            {
                postTask.Wait();
                HttpResponseMessage result = postTask.Result;
                if (result.IsSuccessStatusCode)
                {
                    //eadAsStringAsync();
                    Task<string> readTask = result.Content.ReadAsStringAsync(); //ReadAsAsync<object>();
                    readTask.Wait();
                    string jsonres = readTask.Result;
                    return jsonres;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Sahel-Service");
                string path = AppDomain.CurrentDomain.BaseDirectory;
                using (System.IO.StreamWriter outputFile = new
                  System.IO.StreamWriter(path + "\\log.txt", true))
                {
                    outputFile.WriteLine(DateTime.Now.ToString() +
                        "  getResult Error ==> " +
                        ex.ToString());
                }
            }
            return "";
        }
        #endregion


        #region DTOs
        private CreateRenewImportLicenseDTO GetRenewLicenseDTO(ServiceRequest serviceRequest)
        {
            return new CreateRenewImportLicenseDTO
            {
                CommercialLicenseNo = serviceRequest.ServiceRequestsDetail.CommercialLicenseNo,
                eServiceRequestId = serviceRequest.EserviceRequestId.ToString(),
                ImporterLicenseTypeDesc = serviceRequest.ServiceRequestsDetail.ImporterLicenseTypeDesc,
                IndustrialLicenseNo = serviceRequest.ServiceRequestsDetail.IndustrialLicenseNo,
                LicenseExpiryDate = serviceRequest.ServiceRequestsDetail.LicenseExpiryDate.Value,
                LicenseIssueDate = serviceRequest.ServiceRequestsDetail.LicenseIssueDate.Value,
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer,

                //TODO: check this
                ImporterLicenseNo = serviceRequest.ServiceRequestsDetail.ImporterLicenseNo,
                ImporterLicenseType =
                int.Parse(serviceRequest.ServiceRequestsDetail.ImporterLicenseType),
                LicenseType = serviceRequest.ServiceRequestsDetail.ImporterLicenseType,
                LicenseTypeDesc = serviceRequest.ServiceRequestsDetail.ImporterLicenseType,
                //TypeOfLicenseRequest = serviceRequest.ServiceRequestsDetail.type
            };

        }

        private CreateAddNewImportLicenseDTO GetAddLicenseLicenseDTO(ServiceRequest serviceRequest)
        {
            return new CreateAddNewImportLicenseDTO
            {
                ImporterLicenseNo = serviceRequest.ServiceRequestsDetail.ImporterLicenseNo,
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }

        private RenewIndustrialLicenseDto GetRenewIndustrialLicenseDto(ServiceRequest serviceRequest)
        {
            return new RenewIndustrialLicenseDto
            {
                CommercialLicenseNumber= serviceRequest.ServiceRequestsDetail.CommercialLicenseNo,
                ImportLicenseNumber= serviceRequest.ServiceRequestsDetail.ImporterLicenseNo,
                LicenseExpiryDate= serviceRequest.ServiceRequestsDetail.LicenseExpiryDate.Value,
                LicenseIssueDate= serviceRequest.ServiceRequestsDetail.LicenseExpiryDate.Value,
                //todo: check
                LicenseNumber= serviceRequest.ServiceRequestsDetail.LicenseNumber,
                RequestNumber= serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId= serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }

        private CreateRenewCommercialLicenseRequestDTO GetCreateRenewCommercialLicenseRequestDTO(ServiceRequest serviceRequest)
        {
            return new CreateRenewCommercialLicenseRequestDTO
            {
                CommercialLicenseNumber = serviceRequest.ServiceRequestsDetail.CommercialLicenseNo,
                ImportLicenseNumber = serviceRequest.ServiceRequestsDetail.ImporterLicenseNo,
                LicenseExpiryDate = serviceRequest.ServiceRequestsDetail.LicenseExpiryDate.Value,
                LicenseIssueDate = serviceRequest.ServiceRequestsDetail.LicenseExpiryDate.Value,
                IndustrialLicenseNumber = serviceRequest.ServiceRequestsDetail.IndustrialLicenseNo,
                ImportLicenseType= serviceRequest.ServiceRequestsDetail.ImporterLicenseType,
                ImportLicenseTypeDesc= serviceRequest.ServiceRequestsDetail.ImporterLicenseTypeDesc,
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer

            };

        }
        private AuthorizedSignatoryDto GetAuthorizedSignatoryDto(ServiceRequest serviceRequest)
        {
            return new AuthorizedSignatoryDto
            {
                AuthorizedSignatoryCivilIdExpiryDate= serviceRequest.ServiceRequestsDetail.AuthorizedSignatoryCivilIdExpiryDate.Value,
                CivilId= serviceRequest.ServiceRequestsDetail.CivilId,
                EServiceRequestId= serviceRequest.EserviceRequestId.ToString(),
                AuthPerson= serviceRequest.ServiceRequestsDetail.AuthorizedPerson,
                ExpiryDate= serviceRequest.ServiceRequestsDetail.ExpiryDate.Value,
                IssueDate= serviceRequest.ServiceRequestsDetail.IssueDate.Value,
                NationalityId= serviceRequest.ServiceRequestsDetail.Nationality,
                OrganizationId= serviceRequest.ServiceRequestsDetail.OrganizationId.Value.ToString(),
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }
        private RemoveAuthorizedSignatoryDTO GetRemoveAuthorizedSignatoryDTO(ServiceRequest serviceRequest)
        {
            return new RemoveAuthorizedSignatoryDTO
            {
                AssociatedAuthorizedSignatories= new()
                {
                    AssociatedPersonName= serviceRequest.ServiceRequestsDetail.AuthorizedPerson,
                    CivilIdNo= serviceRequest.ServiceRequestsDetail.CivilId
                },
                EServicerequestId = serviceRequest.EserviceRequestId.ToString(),
                OrganizationNameArabic = serviceRequest.ServiceRequestsDetail.OldOrgAraName,
                OrganizationNameEnglish= serviceRequest.ServiceRequestsDetail.OldOrgEngName,
                TradeLicenceNumber= serviceRequest.ServiceRequestsDetail.LicenseNumber,
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }
        private ReNewAuthorizedSignatoryDTO GetReNewAuthorizedSignatoryDTO(ServiceRequest serviceRequest)
        {
            return new ReNewAuthorizedSignatoryDTO
            {
                AuthorizedSignatory= new()
                {
                    CivilId= serviceRequest.ServiceRequestsDetail.CivilId,
                    CivilIdExpiryDate= serviceRequest.ServiceRequestsDetail.AuthorizedSignatoryCivilIdExpiryDate.Value,
                    ExpiryDate= serviceRequest.ServiceRequestsDetail.ExpiryDate.Value,
                    IssueDate= serviceRequest.ServiceRequestsDetail.IssueDate.Value,
                    Name= serviceRequest.ServiceRequestsDetail.AuthorizedPerson
                },
                EServiceRequestId = serviceRequest.EserviceRequestId.ToString(),
                RequestNumber = serviceRequest.EserviceRequestNumber,
                SelectedAuthorizerCivilId = serviceRequest.ServiceRequestsDetail.SelectedAuthorizer
            };

        }
      
        #endregion


        #region DTO
        public class CreateAddNewImportLicenseDTO

        {

            public string RequestNumber { get; set; }

            public string ImporterLicenseNo { get; set; }

            public string SelectedAuthorizerCivilId { get; set; }

            public bool IsFromSahel { get; set; }



        }

        public class CreateRenewImportLicenseDTO

        {

            public string RequestNumber { get; set; }

            public string eServiceRequestId { get; set; }

            public string ImporterLicenseNo { get; set; }

            public DateTime LicenseIssueDate { get; set; }

            public DateTime LicenseExpiryDate { get; set; }

            public string TypeOfLicenseRequest { get; set; }

            public string IndustrialLicenseNo { get; set; }

            public string CommercialLicenseNo { get; set; }

            public string LicenseType { get; set; }

            public string LicenseTypeDesc { get; set; }

            public int ImporterLicenseType { get; set; }

            public string ImporterLicenseTypeDesc { get; set; }

            public bool IsFromSahel { get; set; }

            public string SelectedAuthorizerCivilId { get; set; }

        }

        public class RenewIndustrialLicenseDto
        {
            public string RequestNumber { get; set; }
            public string LicenseNumber { get; set; }
            public string ImportLicenseNumber { get; set; }
            public string CommercialLicenseNumber { get; set; }
            public DateTime LicenseIssueDate { get; set; }
            public DateTime LicenseExpiryDate { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }

        }

        public class CreateRenewCommercialLicenseRequestDTO
        {
            public string RequestNumber { get; set; }
            public string ImportLicenseNumber { get; set; }
            public string CommercialLicenseNumber { get; set; }

            public string IndustrialLicenseNumber { get; set; }

            public DateTime LicenseIssueDate { get; set; }
            public DateTime LicenseExpiryDate { get; set; }
            public string ImportLicenseType { get; set; }
            public string ImportLicenseTypeDesc { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }


        }

        public class AuthorizedSignatoryDto
        {
            public string EServiceRequestId { get; set; }
            public string RequestNumber { get; set; }
            public string OrganizationId { get; set; }
            public string AuthPerson { get; set; }
            public string CivilId { get; set; }
            public DateTime AuthorizedSignatoryCivilIdExpiryDate { get; set; }
            public int? NationalityId { get; set; }
            public DateTime IssueDate { get; set; }
            public DateTime ExpiryDate { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }


        }

        public class RemoveAuthorizedSignatoryDTO
        {
            public string RequestNumber { get; set; }
            public string OrganizationNameEnglish { get; set; }
            public string OrganizationNameArabic { get; set; }
            public AssociatedPersonDetails AssociatedAuthorizedSignatories { get; set; }
            public string EServicerequestId { get; set; }
            public string TradeLicenceNumber { get; set; }   //not used 
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }


        }
        public class AssociatedPersonDetails
        {
            public string AssociatedPersonName { get; set; }
            public string CivilIdNo { get; set; }
        }

        public class ReNewAuthorizedSignatoryDTO
        {
            public string RequestNumber { get; set; }
            public string EServiceRequestId { get; set; }
            public CreateAuthorizedSignatoryDTO AuthorizedSignatory { get; set; }
            public string SelectedAuthorizerCivilId { get; set; }
            public bool IsFromSahel { get; set; }
        }
        public class CreateAuthorizedSignatoryDTO
        {
            public string Name { get; set; }
            public string CivilId { get; set; }
            public DateTime CivilIdExpiryDate { get; set; }
            public DateTime IssueDate { get; set; }
            public DateTime ExpiryDate { get; set; }
        }
        #endregion
    }


}
