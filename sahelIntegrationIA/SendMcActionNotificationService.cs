using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Core.Persistence;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sahelIntegrationIA.Configurations;
using System.Net.Http.Json;
using System.Net;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;
using eServicesV2.Kernel.Domain.Entities.IdentityEntities;
using sahelIntegrationIA.Models;
using eServices.APIs.UserApp.OldApplication.Models;
using Azure.Core;
using eServicesV2.Kernel.Domain.Entities;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.OrganizationEntities;

namespace sahelIntegrationIA
{
    public class SendMcActionNotificationService
    {
        private string _jobCycleId = Guid.NewGuid().ToString();
        private readonly IRequestLogger _logger;
        private TimeSpan period;
        private readonly IBaseConfiguration _configurations;
        private readonly eServicesContext _eServicesContext;
        private readonly IRequestLogger _requestLogger;
        private readonly IDapper _dapper;
        private readonly SahelConfigurations _sahelConfigurations;
        Dictionary<int, string> requestedCivilIds = new Dictionary<int, string>();

        public SendMcActionNotificationService(IRequestLogger logger,
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
            _jobCycleId = Guid.NewGuid().ToString();

            string[] statusEnums = new string[]
            {
                nameof(ServiceRequestStatesEnum.EServiceRequestORGForVisitState),
                nameof(ServiceRequestStatesEnum.EServiceRequestORGForAdditionalInfo),
                nameof(ServiceRequestStatesEnum.EServiceRequestORGRejectedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestRejectedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestORGApprovedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestFinalRejectedState),
                "EServiceRequestApprovedState",
                "EServiceRequestRejectState",
                "OrganizationRequestRejectedState",
                "OrganizationRequestedForAdditionalInfoState"
            };

            int[] serviceIds = new int[]
            {
                (int)ServiceTypesEnum.NewImportLicenseRequest,
                (int)ServiceTypesEnum.ImportLicenseRenewalRequest,
                (int)ServiceTypesEnum.CommercialLicenseRenewalRequest,
                (int)ServiceTypesEnum.IndustrialLicenseRenewalRequest,
                (int)ServiceTypesEnum.AddNewAuthorizedSignatoryRequest,
                (int)ServiceTypesEnum.RenewAuthorizedSignatoryRequest,
                (int)ServiceTypesEnum.RemoveAuthorizedSignatoryRequest,
                (int)ServiceTypesEnum.OrgNameChangeReqServiceId,
                (int)ServiceTypesEnum.ChangeCommercialAddressRequest,
                (int)ServiceTypesEnum.ConsigneeUndertakingRequest
            };

            var statusJson = Newtonsoft.Json.JsonConvert.SerializeObject(statusEnums, Newtonsoft.Json.Formatting.None,
                        new JsonSerializerSettings()
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });

            var serviceIdsJson = Newtonsoft.Json.JsonConvert.SerializeObject(serviceIds, Newtonsoft.Json.Formatting.None,
                        new JsonSerializerSettings()
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });

            _logger.LogInformation("{2} - ShaleNotificationMC - start Sahel MC Notifications - {0} - {1}",
                        new object[] { statusJson, serviceIdsJson, _jobCycleId });

            var requestList = await _eServicesContext
                               .Set<ServiceRequest>()
                               .Include(p => p.ServiceRequestsDetail)
                               .Where(p => statusEnums.Contains(p.StateId)
                                           && p.RequestSource == "Sahel"
                                           && serviceIds.Contains((int)p.ServiceId.Value)
                                           && (p.ServiceRequestsDetail.ReadyForSahelSubmission == "0")
                                            && (p.ServiceRequestsDetail.MCNotificationSent.HasValue 
                                            && !p.ServiceRequestsDetail.MCNotificationSent.Value))
                               .AsNoTracking()
                               .ToListAsync();
            //var organizationRequests = await _eServicesContext
            //                   .Set<ServiceRequest>()
            //                   .Where(p => statusEnums.Contains(p.StateId)
            //                               && p.RequestSource == "Sahel"
            //                               && p.ServiceId == (int)ServiceTypesEnum.OrganizationRegistrationService
            //                               )
            //                   .Select(a => a.EserviceRequestNumber)
            //                   .ToListAsync();
            //if(organizationRequests.Any())
            //{
            //   var organizationRequestNeedMcnotification = await _eServicesContext
            //                   .Set<OrganizationRequests>()
            //    .Where(p => statusEnums.Contains(p.StateId)
            //                   && organizationRequests.Contains(p.req) 
            //}
            string log = Newtonsoft.Json.JsonConvert.SerializeObject(requestList, Newtonsoft.Json.Formatting.None,
                        new JsonSerializerSettings()
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });
            _logger.LogInformation(message: "{1} - ShaleNotificationMC - Sahel MC Notifications {0}", new object[] { log, _jobCycleId });

            return requestList;
        }
        public async Task SendNotification()
        {
            _logger.LogInformation($"{_jobCycleId} - ShaleNotificationMC - start send mc action notification service");

            var serviceRequests = await GetRequestList();
            var requestedIds = serviceRequests.Select(a => a.RequesterUserId).ToList();

            requestedCivilIds = await _eServicesContext
                              .Set<eServicesV2.Kernel.Domain.Entities.IdentityEntities.User>()
                              .Where(p => requestedIds.Contains(p.UserId))
                              .Select(a => new { a.UserId, a.CivilId })
                              .ToDictionaryAsync(a => a.UserId, a => a.CivilId);

            var exceptions = new List<Exception>();

            var tasks = serviceRequests.Select(async serviceRequest =>
            {
                try
                {
                    await ProcessServiceRequest(serviceRequest);
                }

                catch (Exception ex)
                {
                    _logger.LogException(ex,
                        $"{_jobCycleId} - ShaleNotificationMC - notification sahel exception - {0}",
                        new object[] { serviceRequest.EserviceRequestNumber });
                }

            });
            await Task.WhenAll(tasks);
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

            await CreateNotification(serviceRequest);

        }

        private async Task CreateNotification(ServiceRequest serviceRequest)
        {
            var reqJson = Newtonsoft.Json.JsonConvert.SerializeObject(serviceRequest, Newtonsoft.Json.Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
            _logger.LogInformation("{1} - ShaleNotificationMC - start Notification creation process - {0}",
                propertyValues: new object[] { reqJson , _jobCycleId });

            string civilID = requestedCivilIds.First(a => a.Key == serviceRequest.RequesterUserId).Value;

            _logger.LogInformation(message: "{2} - ShaleNotificationMC - Get organization civil Id - {0} - {1}",
                propertyValues: new object[] { serviceRequest.EserviceRequestNumber, civilID , _jobCycleId });

            Notification notficationResponse = new Notification();
            string msgAr = string.Empty;
            string msgEn = string.Empty;


            switch (serviceRequest.StateId)
            {
                case nameof(ServiceRequestStatesEnum.EServiceRequestORGForVisitState):
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.VisiNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.VisiNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;
                case nameof(ServiceRequestStatesEnum.EServiceRequestORGForAdditionalInfo):
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.AdditionalInfoNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.AdditionalInfoNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;
                case nameof(ServiceRequestStatesEnum.EServiceRequestORGRejectedState):
                case nameof(ServiceRequestStatesEnum.EServiceRequestRejectedState):
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.RejectNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.RejectNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;
                case nameof(ServiceRequestStatesEnum.EServiceRequestFinalRejectedState):
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.FinalRejectNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.FinalRejectNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;
                case nameof(ServiceRequestStatesEnum.EServiceRequestORGApprovedState):
                case "EServiceRequestApprovedState":
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.ApproveNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.ApproveNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;
            }

            var notificationType = GetNotificationType((ServiceTypesEnum)serviceRequest.ServiceId);
            notficationResponse.bodyEn = msgAr;
            notficationResponse.bodyAr = msgEn;
            notficationResponse.isForSubscriber = "true";
            notficationResponse.notificationType = serviceRequest.ServiceId.ToString();
            notficationResponse.dataTableEn = null;
            notficationResponse.dataTableAr = null;
            notficationResponse.subscriberCivilId = civilID;
            notficationResponse.notificationType = ((int)notificationType).ToString();
            string log = Newtonsoft.Json.JsonConvert.SerializeObject(notficationResponse, Newtonsoft.Json.Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            _logger.LogInformation(message: "{2} - ShaleNotificationMC - Preparing notification {0} - {1}",
                propertyValues: new object[] { serviceRequest.EserviceRequestNumber, log , _jobCycleId });

            var sendNotificationResult = PostNotification(notficationResponse, serviceRequest.EserviceRequestNumber, "Individual");
            await InsertNotification(notficationResponse, sendNotificationResult);

            if (sendNotificationResult)
            {
                _logger.LogInformation(message: " {1} - ShaleNotificationMC - notification sent successfully: {0}",
                        propertyValues: new object[] { serviceRequest.EserviceRequestNumber, _jobCycleId });
                await _eServicesContext
                                   .Set<ServiceRequestsDetail>()
                                   .Where(a => a.EserviceRequestId == serviceRequest.EserviceRequestId)
                                   .ExecuteUpdateAsync<ServiceRequestsDetail>(a => a.SetProperty(b => b.MCNotificationSent, true));
            }
            else
            {
                _logger.LogInformation(message: "{1} - ShaleNotificationMC - notification sent faild: {0}",
                        propertyValues: new object[] { serviceRequest.EserviceRequestNumber , _jobCycleId });
            }
            _logger.LogInformation(message: "{1} - ShaleNotificationMC - end notification: {0}",
                    propertyValues: new object[] { serviceRequest.EserviceRequestNumber, _jobCycleId });

        }

        #region Private Methods
        public SahelNotficationTypesEnum GetNotificationType(ServiceTypesEnum service)
        {
            switch (service)
            {
                case ServiceTypesEnum.AddNewAuthorizedSignatoryRequest:
                    return SahelNotficationTypesEnum.AddNewAuthorizedSignatory;
                case ServiceTypesEnum.RemoveAuthorizedSignatoryRequest:
                    return SahelNotficationTypesEnum.RemoveAuthorizedSignatory;
                case ServiceTypesEnum.RenewAuthorizedSignatoryRequest:
                    return SahelNotficationTypesEnum.RenewAuthorizedSignatory;
                case ServiceTypesEnum.ImportLicenseRenewalRequest:
                    return SahelNotficationTypesEnum.RenewImportLicense;
                case ServiceTypesEnum.NewImportLicenseRequest:
                    return SahelNotficationTypesEnum.AddNewImportLicense;
                case ServiceTypesEnum.ChangeCommercialAddressRequest:
                    return SahelNotficationTypesEnum.ChangeCommercialAddress;
                case ServiceTypesEnum.IndustrialLicenseRenewalRequest:
                    return SahelNotficationTypesEnum.RenewIndustrialLicense;
                case ServiceTypesEnum.CommercialLicenseRenewalRequest:
                    return SahelNotficationTypesEnum.RenewCommercialLicense;
                case ServiceTypesEnum.OrgNameChangeReqServiceId:
                    return SahelNotficationTypesEnum.OrganizationNameChange;
                case ServiceTypesEnum.ConsigneeUndertakingRequest:
                    return SahelNotficationTypesEnum.UnderTakingConsigneeRequest;
                    case ServiceTypesEnum.OrganizationRegistrationService:
                    return SahelNotficationTypesEnum.OrganizationRegistrationService;
                default:
                    return SahelNotficationTypesEnum.RenewImportLicense;
            }
        }

        public bool PostNotification(Notification notification, string requestNumber, string SahelOption = "Business")
        {
            if (string.IsNullOrEmpty(notification.bodyAr) && string.IsNullOrEmpty(notification.bodyEn))
            {
                _logger.LogInformation(message: "{1} - ShaleNotificationMC - Can't send notification because the body is empty - {0}",
                    propertyValues: new object[] { requestNumber , _jobCycleId });
                return false;
            }

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
                    _logger.LogInformation("{2} - ShaleNotificationMC - notification result: {0} - {1}",
                        propertyValues: new object[] { requestNumber, rEsult, _jobCycleId });

                    return !string.IsNullOrEmpty(rEsult);
                }
            }
            return false;
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

        #endregion
    }
}
