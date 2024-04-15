﻿using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Core.Persistence;
using eServicesV2.Kernel.Data.Contexts;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sahelIntegrationIA.Configurations;
using System.Net.Http.Json;
using System.Net;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;
using eServicesV2.Kernel.Domain.Entities.IdentityEntities;
using eServicesV2.Kernel.Domain.Entities.IndividualAuthorizationEntities;

namespace sahelIntegrationIA
{
    public class SendMcActionNotificationService
    {
        private readonly IRequestLogger _logger;
        private TimeSpan period;
        private readonly IBaseConfiguration _configurations;
        private readonly eServicesContext _eServicesContext;
        private readonly IRequestLogger _requestLogger;
        private readonly IDapper _dapper;
        private readonly SahelConfigurations _sahelConfigurations;
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
            string[] statusEnums = new string[]
            {
                nameof(ServiceRequestStatesEnum.EServiceRequestORGForVisitState),
                nameof(ServiceRequestStatesEnum.EServiceRequestORGForAdditionalInfo),
                nameof(ServiceRequestStatesEnum.EServiceRequestORGRejectedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestRejectedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestORGApprovedState),
                nameof(ServiceRequestStatesEnum.EServiceRequestFinalRejectedState),

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

            var requestList = await _eServicesContext
                               .Set<ServiceRequest>()
                               .Include(p => p.ServiceRequestsDetail)
                               .Where(p => statusEnums.Contains(p.StateId)
                                           && p.RequestSource == "Sahel"
                                           && serviceIds.Contains((int)p.ServiceId.Value)
                                           && (p.ServiceRequestsDetail.ReadyForSahelSubmission.HasValue && !p.ServiceRequestsDetail.ReadyForSahelSubmission.Value)
                                            && (p.ServiceRequestsDetail.MCNotificationSent.HasValue && !p.ServiceRequestsDetail.MCNotificationSent.Value))
                                .ToListAsync();
            return requestList;
        }
        public async Task SendNotification()
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
            await Task.WhenAll(tasks);


            if (exceptions.Any())

            {
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
            await CreateNotification(serviceRequest);

        }

        private async Task CreateNotification(ServiceRequest serviceRequest)
        {
            string civilID = await _eServicesContext
                               .Set<User>()
                               .Where(p => p.UserId == serviceRequest.RequesterUserId)
                               .Select(a=> a.CivilId)
                               .FirstOrDefaultAsync();
            Notification notficationResponse = new Notification();
            string msgAr = string.Empty;
            string msgEn = string.Empty;
            if (serviceRequest.StateId == nameof(ServiceRequestStatesEnum.EServiceRequestORGForVisitState))
            {
                msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.VisiNotificationAr, serviceRequest.EserviceRequestNumber);
                msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.VisiNotificationEn, serviceRequest.EserviceRequestNumber);
            }
            else if (serviceRequest.StateId == nameof(ServiceRequestStatesEnum.EServiceRequestORGForAdditionalInfo))
            {
                msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.AdditionalInfoNotificationAr, serviceRequest.EserviceRequestNumber);
                msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.AdditionalInfoNotificationEn, serviceRequest.EserviceRequestNumber);
            }
            else if (serviceRequest.StateId == nameof(ServiceRequestStatesEnum.EServiceRequestORGRejectedState)|| (serviceRequest.StateId == nameof(ServiceRequestStatesEnum.EServiceRequestRejectedState)))
            {
                msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.RejectNotificationAr, serviceRequest.EserviceRequestNumber);
                msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.RejectNotificationEn, serviceRequest.EserviceRequestNumber);
            }
            else if (serviceRequest.StateId == nameof(ServiceRequestStatesEnum.EServiceRequestFinalRejectedState))
            {
                msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.FinalRejectNotificationAr, serviceRequest.EserviceRequestNumber);
                msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.FinalRejectNotificationEn, serviceRequest.EserviceRequestNumber);
            }
            else if (serviceRequest.StateId == nameof(ServiceRequestStatesEnum.EServiceRequestORGApprovedState))
            {
                msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.ApproveNotificationAr, serviceRequest.EserviceRequestNumber);
                msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.ApproveNotificationEn, serviceRequest.EserviceRequestNumber);
            }
            notficationResponse.bodyEn = msgAr;
            notficationResponse.bodyAr = msgEn;
            notficationResponse.isForSubscriber = "true";
            notficationResponse.notificationType = serviceRequest.ServiceId.ToString();
            notficationResponse.dataTableEn = null;
            notficationResponse.dataTableAr = null;
            notficationResponse.subscriberCivilId = civilID;

            PostNotification(notficationResponse, "Individual");
            serviceRequest.ServiceRequestsDetail.MCNotificationSent = true;
            _eServicesContext.Set<ServiceRequest>().Update(serviceRequest);
            var commited = await _eServicesContext.SaveChangesAsync();

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
    }
}