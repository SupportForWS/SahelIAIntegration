using eServicesV2.Kernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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
using eServicesV2.Kernel.Domain.Entities.OrganizationEntities;
using static sahelIntegrationIA.VerificationServiceForOrganizationServices;
using eServices.APIs.UserApp.OldApplication.Models;
namespace sahelIntegrationIA
{

    public class VerificationServiceForBrokerServices
    {
        private readonly IRequestLogger _logger;
        private readonly IBaseConfiguration _configurations;
        private readonly eServicesContext _eServicesContext;
        private readonly SahelConfigurations _sahelConfigurations;
        private Dictionary<long, string> requestedCivilIds = new();

        public VerificationServiceForBrokerServices(IRequestLogger logger,
                                                          IBaseConfiguration configuration,
                                                          eServicesContext eServicesContext,
                                                          SahelConfigurations sahelConfigurations)
        {
            _logger = logger;
            _configurations = configuration;
            _eServicesContext = eServicesContext;
            _sahelConfigurations = sahelConfigurations;
        }

        private async Task<List<ServiceRequest>> GetRequestList()
        {
            string[] statusEnums = new string[]
            {
                nameof(ServiceRequestStatesEnum.EServiceRequestORGCreatedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestORGForAdditionalInfo),
                nameof(ServiceRequestStatesEnum.EServiceRequestORGRejectedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestCreatedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestRejectedState),
                nameof(ServiceRequestStatesEnum.EServiceOrganizationRequestCreatedState),
                "OrganizationRequestCreatedState",
                "OrganizationRequestRejectedState",
                "OrganizationRequestedForAdditionalInfoState",


            };

            int[] serviceIds = new int[]
            {
                (int)ServiceTypesEnum.BrsPrintingCancelCommercialLicense,
                (int)ServiceTypesEnum.BrsPrintingIssueLicense,
                (int)ServiceTypesEnum.BrsPrintingChangeLicenseAddress,
                (int)ServiceTypesEnum.BrsPrintingChangeLicenseActivity,
                (int)ServiceTypesEnum.BrsPrintingReleaseBankGuarantee,
                (int)ServiceTypesEnum.BrsPrintingGoodBehave,
                (int)ServiceTypesEnum.BrsPrintingRenewLicense,
                (int)ServiceTypesEnum.BrsPrintingChangeJobTitleRenewResidency,
                (int)ServiceTypesEnum.BrsPrintingChangeJobTitleTransferResidency,
                (int)ServiceTypesEnum.BrsPrintingRenewResidency,
                (int)ServiceTypesEnum.BrsPrintingTransferResidency,
                (int)ServiceTypesEnum.BrsPrintingChangeJobTitle,
                (int)ServiceTypesEnum.BrsPrintingChangeJobTitleCivil,
                (int)ServiceTypesEnum.BrsPrintingDeActivateLicenseDeath
            };


            DateTime currentDate = DateTime.Now;

            _logger.LogInformation("Start fetching data for Broker verification service");


            var requestList = await _eServicesContext
                               .Set<ServiceRequest>()
                               .Include(p => p.ServiceRequestsDetail)
                               .Where(p => statusEnums.Contains(p.StateId)
                                           && p.RequestSource == "Sahel"
                                           && !string.IsNullOrEmpty(p.ServiceRequestsDetail.KMIDToken)
                                           && serviceIds.Contains((int)p.ServiceId.Value)
                                           && (p.ServiceRequestsDetail.ReadyForSahelSubmission == "1" ||
                                            (p.ServiceRequestsDetail.ReadyForSahelSubmission == "2"
                                            && p.RequestSubmissionDateTime.HasValue &&
                                            p.RequestSubmissionDateTime.Value.AddMinutes(_sahelConfigurations.SahelSubmissionTimer)
                                            < currentDate)
                                            ))
                                .ToListAsync();






            var requestNumbers = requestList.Select(p => p.EserviceRequestNumber).ToList();

            string log = JsonConvert.SerializeObject(requestNumbers, Formatting.None,
                        new JsonSerializerSettings()
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });

            _logger.LogInformation("Number of records: {Records}. List of request numbers: {RequestNumbers}",
                                   requestNumbers.Count,
                                   log);

            var requestId = requestList
                .Select(a => a.ServiceRequestsDetail.EserviceRequestDetailsId)
                .ToList();

            await _eServicesContext
                               .Set<ServiceRequestsDetail>()
                               .Where(a => requestId.Contains(a.EserviceRequestDetailsId))
                               .ExecuteUpdateAsync(a => a.SetProperty(b => b.ReadyForSahelSubmission, "2"));

            var kmidCreatedList = requestList
                .Select(a => a.ServiceRequestsDetail.KMIDToken)
                .ToList();

            var kmidStrings = kmidCreatedList
                .Select(k => k.ToString())
                .ToList();

            var currentTime = DateTime.Now;

            var expiredKmidRequests = await _eServicesContext.Set<KGACPACIQueue>()
                .Where(a => kmidStrings.Contains(a.KGACPACIQueueId.ToString())
                            && a.DateCreated.AddSeconds(_sahelConfigurations.BrokerKMIDCallingTimer) < currentTime)
                .Select(a => a.KGACPACIQueueId)
                .ToListAsync();

            if (expiredKmidRequests.Count > 0)
            {
                // var requestedIds = requestList.Select(a => a.RequesterUserId).ToList();

                //todo check
                requestedCivilIds = requestList
                             .Select(a => new { a.ServiceRequestsDetail.EserviceRequestDetailsId, a.ServiceRequestsDetail.CivilId })
                             .ToDictionary(a => a.EserviceRequestDetailsId, a => a.CivilId);

                var expiredRequest = requestList
                    .Where(a => expiredKmidRequests.Contains(Convert.ToInt32(a.ServiceRequestsDetail.KMIDToken)))
                    .ToList();

                await SendExpiredKmidNotification(expiredRequest);

                foreach (var request in expiredRequest)
                {
                    var expiredRequestsIds = expiredRequest
                        .Select(a => a.ServiceRequestsDetail.EserviceRequestDetailsId)
                        .ToList();

                    await _eServicesContext
                                   .Set<ServiceRequestsDetail>()
                                   .Where(a => expiredRequestsIds.Contains(a.EserviceRequestDetailsId))
                                   .ExecuteUpdateAsync(a => a.SetProperty(b => b.KMIDToken, string.Empty));

                }

            }


            var filteredRequestList = requestList
                .Where(request => !expiredKmidRequests.Contains(int.Parse(request.ServiceRequestsDetail.KMIDToken)))
                .ToList();


            requestNumbers = filteredRequestList
                .Select(p => p.EserviceRequestNumber)
                .ToList();

            string requestNumbersLog = JsonConvert.SerializeObject(requestNumbers);
            _logger.LogInformation("Filtered Requests Numbers: {filteredRequestList}", requestNumbersLog);

            return filteredRequestList;
        }

        public async Task CheckBrokerRequests()
        {
            _logger.LogInformation("Start Broker Verification Service");

            
            var serviceRequest = await GetRequestList();
            //todo check
            //var requestedIds = serviceRequest.Select(a => a.RequesterUserId).ToList();

            requestedCivilIds = serviceRequest
                              .Select(a => new { a.ServiceRequestsDetail.EserviceRequestDetailsId, a.ServiceRequestsDetail.CivilId })
                              .ToDictionary(a => a.EserviceRequestDetailsId, a => a.CivilId);

            var tasks = serviceRequest.Select(async serviceRequest =>
            {
                try
                {
                    await ProcessServiceRequest(serviceRequest);
                }

                catch (Exception ex)
                {
                    _logger.LogException(ex, "Sahel-Windows-Service");
                }

            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

        }

        private async Task ProcessServiceRequest(ServiceRequest serviceRequest)
        {
            if (serviceRequest.ServiceId is null or 0)
            {
                _logger.LogException(new NullReferenceException($"Service Id is required"), "Sahel-Windows-Service");
                return;
            }

            if (!Enum.IsDefined(typeof(ServiceTypesEnum), (int)serviceRequest.ServiceId))
            {
                _logger.LogException(new ArgumentException($"Invalid Service Id {serviceRequest.ServiceId}"), "Sahel-Windows-Service");
                return;
            }

            // Create DTO and call api
            await ProcessServiceRequestAndCallAPI(serviceRequest);

        }

        private async Task ProcessServiceRequestAndCallAPI(ServiceRequest serviceRequest)
        {
            _logger.LogInformation("Start processing service request: {EserviceRequestNumber}", serviceRequest.EserviceRequestNumber);

            string url;

            switch ((ServiceTypesEnum)serviceRequest.ServiceId)
            {
                case ServiceTypesEnum.BrsPrintingCancelCommercialLicense:
                case ServiceTypesEnum.BrsPrintingIssueLicense:
                case ServiceTypesEnum.BrsPrintingChangeLicenseAddress:
                case ServiceTypesEnum.BrsPrintingChangeLicenseActivity:
                case ServiceTypesEnum.BrsPrintingReleaseBankGuarantee:
                case ServiceTypesEnum.BrsPrintingGoodBehave:
                case ServiceTypesEnum.BrsPrintingRenewLicense:
                case ServiceTypesEnum.BrsPrintingChangeJobTitleRenewResidency:
                case ServiceTypesEnum.BrsPrintingChangeJobTitleTransferResidency:
                case ServiceTypesEnum.BrsPrintingRenewResidency:
                case ServiceTypesEnum.BrsPrintingTransferResidency:
                case ServiceTypesEnum.BrsPrintingChangeJobTitle:
                case ServiceTypesEnum.BrsPrintingChangeJobTitleCivil:
                case ServiceTypesEnum.BrsPrintingDeActivateLicenseDeath:
                    url = _sahelConfigurations.EservicesUrlsConfigurations.BrokerAffairsUrl;
                    await CallServiceAPI(GetBrokerArrairsRequestDto(serviceRequest), url);
                    break;

                default:
                    var errorMessage = $"Invalid service ID: {serviceRequest.ServiceId}";
                    _logger.LogException(new ArgumentException(errorMessage), "Sahel-Windows-Service");
                    return;

            }

        }

        private async Task CallServiceAPI<T>(T serviceDTO, string apiUrl)
        {
            try
            {
                _logger.LogInformation("Start calling eService API at URL: {ApiUrl}", apiUrl);

                using (var httpClient = new HttpClient())
                {
                    string json = JsonConvert.SerializeObject(serviceDTO);

                    _logger.LogInformation("Serialized DTO: {SerializedDTO}", json);

                    var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                    var httpResponse = await httpClient.PostAsync(apiUrl, httpContent);

                    string responseContent = await httpResponse.Content.ReadAsStringAsync();

                    _logger.LogInformation("eService Response content: {ResponseContent}", responseContent);

                    Notification notification = JsonConvert.DeserializeObject<Notification>(responseContent);

                    bool isSent = PostNotification(notification, "Business");
                    await InsertNotification(notification, isSent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Error occurred while calling API at URL: {ApiUrl}", apiUrl);
            }

        }

        #region Private Methods
        private async Task SendExpiredKmidNotification(List<ServiceRequest> serviceRequest)
        {
            var tasks = serviceRequest.Select(async request =>
            {
                try
                {
                    await CreateNotification(request);
                }

                catch (Exception ex)
                {
                    _logger.LogException(ex, "Sahel-Windows-Service");
                }

            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);
        }

        private async Task CreateNotification(ServiceRequest serviceRequest)
        {
            _logger.LogInformation("start Notification creation process for expired kmid");

            Notification notficationResponse = new();
            string msgAr = string.Empty;
            string msgEn = string.Empty;
            _logger.LogInformation("Create Notification message for broker expired KMID");

            string civilID = requestedCivilIds.First(a => a.Key == serviceRequest.ServiceRequestsDetail.EserviceRequestDetailsId).Value;
                
            _logger.LogInformation("Get broker civil Id{0}", civilID);

            msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.BrokerKmidExpiredAr,
                serviceRequest.EserviceRequestNumber);
            msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.BrokerKmidExpiredEn,
                serviceRequest.EserviceRequestNumber);

            _logger.LogInformation("Notification Message in Arabic : {0} , Notification Message in English {1}",
                 msgAr, msgEn);

            var notificationType = GetNotificationType((ServiceTypesEnum)serviceRequest.ServiceId);

            notficationResponse.bodyEn = msgAr;
            notficationResponse.bodyAr = msgEn;
            notficationResponse.isForSubscriber = "true";
            notficationResponse.dataTableEn = null;
            notficationResponse.dataTableAr = null;
            notficationResponse.subscriberCivilId = civilID;
            notficationResponse.notificationType = ((int)notificationType).ToString();

            string log = JsonConvert.SerializeObject(notficationResponse, Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            _logger.LogInformation(message: "Preparing KMID expiration notification {0}", log);

            _logger.LogInformation("start notification: {0}", serviceRequest.EserviceRequestNumber);

            bool isSent = PostNotification(notficationResponse, "Business");
            await InsertNotification(notficationResponse, isSent);

            var requestId = serviceRequest.EserviceRequestId;

            await _eServicesContext
                               .Set<ServiceRequestsDetail>()
                               .Where(a => a.EserviceRequestId == requestId)
                               .ExecuteUpdateAsync(a => a.SetProperty(b => b.MCNotificationSent, true));

            _logger.LogInformation("end notification broker: {0}", serviceRequest.EserviceRequestNumber);

        }

        private SahelNotficationTypesEnum GetNotificationType(ServiceTypesEnum serviceId)
        {
            return serviceId switch
            {
                ServiceTypesEnum.BrsPrintingCancelCommercialLicense =>
                SahelNotficationTypesEnum.BrsPrintingCancelCommercialLicense,

                ServiceTypesEnum.BrsPrintingIssueLicense =>
                SahelNotficationTypesEnum.BrsPrintingIssueLicense,

                ServiceTypesEnum.BrsPrintingChangeLicenseAddress =>
                SahelNotficationTypesEnum.BrsPrintingChangeLicenseAddress,

                ServiceTypesEnum.BrsPrintingChangeLicenseActivity =>
                SahelNotficationTypesEnum.BrsPrintingChangeLicenseActivity,

                ServiceTypesEnum.BrsPrintingReleaseBankGuarantee =>
                SahelNotficationTypesEnum.BrsPrintingReleaseBankGuarantee,

                ServiceTypesEnum.BrsPrintingGoodBehave =>
                SahelNotficationTypesEnum.BrsPrintingGoodBehave,

                ServiceTypesEnum.BrsPrintingRenewLicense =>
                SahelNotficationTypesEnum.BrsPrintingRenewLicense,

                ServiceTypesEnum.BrsPrintingChangeJobTitleRenewResidency =>
                SahelNotficationTypesEnum.BrsPrintingChangeJobTitleRenewResidency,

                ServiceTypesEnum.BrsPrintingChangeJobTitleTransferResidency =>
                SahelNotficationTypesEnum.BrsPrintingChangeJobTitleTransferResidency,

                ServiceTypesEnum.BrsPrintingRenewResidency =>
                SahelNotficationTypesEnum.BrsPrintingRenewResidency,

                ServiceTypesEnum.BrsPrintingTransferResidency =>
                SahelNotficationTypesEnum.BrsPrintingTransferResidency,

                ServiceTypesEnum.BrsPrintingChangeJobTitle =>
                SahelNotficationTypesEnum.BrsPrintingChangeJobTitle,

                ServiceTypesEnum.BrsPrintingChangeJobTitleCivil =>
                SahelNotficationTypesEnum.BrsPrintingChangeJobTitleCivil,

                ServiceTypesEnum.BrsPrintingDeActivateLicenseDeath =>
                SahelNotficationTypesEnum.BrsPrintingDeActivateLicenseDeath,

                _ => 0
            };
        }

        private async Task InsertNotification(Notification notification, bool isSent)
        {
            var syncQueueItem = new KGACSahelOutSyncQueue
            {
                CivilId = notification.subscriberCivilId,
                CreatedBy = notification.subscriberCivilId,
                NotificationId = int.Parse(notification.notificationType),
                SahelType = "B",
                MsgTableEn = JsonConvert.SerializeObject(notification.dataTableEn ?? new Dictionary<string, string>()),
                MsgTableAr = JsonConvert.SerializeObject(notification.dataTableAr ?? new Dictionary<string, string>()),
                MsgBodyEn = notification.bodyEn,
                MsgBodyAr = notification.bodyAr,
                DateCreated = DateTime.Now,
                Sync = isSent,
                TryCount = 1,
                Source = "eService"
            };

            _eServicesContext.Add(syncQueueItem);
            await _eServicesContext.SaveChangesAsync();
        }

        private bool PostNotification(Notification notification, string SahelOption = "Business")
        {

            if (string.IsNullOrEmpty(notification.bodyAr) && string.IsNullOrEmpty(notification.bodyAr))
            {
                return false;
            }
            string notificationString = JsonConvert.SerializeObject(notification);

            _logger.LogInformation("Start Sending NotificationBody: {NotificationBody}", notificationString);

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
                    return !string.IsNullOrEmpty(getResult(postTask));
                }
            }
            return false;
        }

        private string GenerateToken(string SahelOption)
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

        private string getResult(Task<HttpResponseMessage> postTask)
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
                _logger.LogException(ex, "Sahel-Windows-Service");
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

        private BrokerArrairsRequestDto GetBrokerArrairsRequestDto(ServiceRequest serviceRequest)
        {
            //TODO: check
            var details = serviceRequest.ServiceRequestsDetail;

            var requestDto = new BrokerArrairsRequestDto();

            requestDto.OrganizationId = CommonFunctions.CsUploadEncrypt(details.OrganizationId.ToString());
            // requestDto.BrokerTypeId = details.RequestForUserType;
            requestDto.BrokerArabicName = details.RequesterArabicName;
            requestDto.BrokerEnglishName = details.RequesterEnglishName;
            requestDto.CivilId = details.CivilId;
            requestDto.PassportNumber = details.PassportNo;//
            requestDto.MobileNumber = details.MobileNo;//
            requestDto.MailAddress = details.RequestForEmail;
            requestDto.RequestNumber = serviceRequest.EserviceRequestNumber;

            requestDto.EServiceRequestId = CommonFunctions.CsUploadEncrypt(serviceRequest.EserviceRequestId.ToString());

            requestDto.ChangeJobTitleTo = details.ChangeJobTitleTo.HasValue ?
                CommonFunctions.CsUploadEncrypt(details.ChangeJobTitleTo.ToString()) : string.Empty;

            requestDto.ChangeJobTitleFrom = details.ChangeJobTitleFrom;

            //requestDto.CivilIdExpiryDate = details.CivilIdexpiryDate.HasValue?
            //    details.CivilIdexpiryDate.Value : DateTime.Now;//

            requestDto.PassportExpiryDate = details.PassportExpiryDate.HasValue?
                details.PassportExpiryDate.Value:null;//

            requestDto.TradeLicenseExpiryDate = details.LicenseNumberExpiryDate.HasValue?
                details.LicenseExpiryDate:null;//

            requestDto.ServiceId = CommonFunctions.CsUploadEncrypt(serviceRequest.ServiceId.ToString());
            requestDto.FromBusiness = details.FromBusiness;
            // requestDto.ToBusiness = details.;
            // requestDto.SelectedOrganizationIdForIssuance = details.SelectedOrganizationIdForIssuance;
            requestDto.OfficialAddress = details.Address;

            // requestDto.BankGuaranteeNumber = details.BankGuaranteeNumber;
            // requestDto.BankName = details.BankGuaranteeBankId;
            // requestDto.BankGuaranteeIssuanceDate = details.BankGuaranteeDate;
            // requestDto.BankGuaranteeExpiryDate = details.BankGuaranteeExpiryDate;
            // requestDto.BankGuaranteeStatus = details.bank;

            return requestDto;
        }

        #endregion


        #region DTO
        public class BrokerArrairsRequestDto
        {
            public string OrganizationId { get; set; }
            public string BrokerTypeId { get; set; }
            public string BrokerArabicName { get; set; }
            public string BrokerEnglishName { get; set; }
            public string CivilId { get; set; }
            public string PassportNumber { get; set; }
            public string MobileNumber { get; set; }
            public string MailAddress { get; set; }
            public string RequestNumber { get; set; }
            public string EServiceRequestId { get; set; }
            public string ChangeJobTitleTo { get; set; }
            public DateTime CivilIdExpiryDate { get; set; }
            public DateTime? PassportExpiryDate { get; set; }
            public DateTime? TradeLicenseExpiryDate { get; set; }
            public string ServiceId { get; set; }
            public string FromBusiness { get; set; }
            public string ToBusiness { get; set; }
            public string ChangeJobTitleFrom { get; set; }
            public string SelectedOrganizationIdForIssuance { get; set; }
            public string OfficialAddress { get; set; }
            public string BankGuaranteeNumber { get; set; }
            public string BankName { get; set; }
            public DateTime? BankGuaranteeIssuanceDate { get; set; }
            public DateTime? BankGuaranteeExpiryDate { get; set; }
            public string BankGuaranteeStatus { get; set; }
        }

        #endregion

    }
}
