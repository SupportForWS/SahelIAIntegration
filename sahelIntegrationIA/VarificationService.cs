using Azure.Core;
using Dapper;
using eServices.APIs.UserApp.OldApplication;
using eServices.APIs.UserApp.OldApplication.Models;
using eServicesV2.Kernel.Core;
using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Extentions;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Core.Persistence;
using eServicesV2.Kernel.Data.Contexts;
using eServicesV2.Kernel.Domain.Entities.IndividualAuthorizationEntities;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.LookupEntities;
using eServicesV2.Kernel.Domain.Enums;
using eServicesV2.Kernel.Infrastructure.Integration.MOIIntegration.DTOs;
using eServicesV2.Kernel.Infrastructure.Persistence.Constants;
using eServicesV2.Kernel.Service.ServiceRequestServices.Models.ServiceRequestService;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sahelIntegrationIA.Configurations;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Validation;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;
using static eServicesV2.Kernel.Infrastructure.Persistence.Constants.StoredProcedureNames.GenarateQRToken.GenarateQRTokenParamerters;
using eServicesContext = sahelIntegrationIA.Models.eServicesContext;

namespace IndividualAuthorizationSahelWorker
{
    public class VarificationService
    {
        private readonly IRequestLogger _logger;
        private readonly eServicesContext _eServicesContext;
        private TimeSpan period;
        private readonly IBaseConfiguration _configurations;
        private readonly IRequestLogger _requestLogger;
        private readonly IDapper _dapper;
        private readonly SahelConfigurations _sahelConfigurations;

        public VarificationService(IRequestLogger logger, eServicesContext eServicesContext,
            IBaseConfiguration configuration, IRequestLogger requestLogger, IDapper dapper, SahelConfigurations sahelConfigurations)
        {
            _logger = logger;
            this._eServicesContext = eServicesContext;
            _configurations = configuration;
            _requestLogger = requestLogger;
            _dapper = dapper;
            _sahelConfigurations = sahelConfigurations;
        }

        public async Task VarifyRequests()
        {
            int[] statusEnums = new int[]
            {
                    (int)IndividualAuthorizationStatusEnum.AuthorizationCompleted,
                    (int)IndividualAuthorizationStatusEnum.PendingAuthorization,
                    (int)IndividualAuthorizationStatusEnum.PendingAuthorizerApproval,
                    (int)IndividualAuthorizationStatusEnum.PendingRequesterApproval,

            };
            int interval = Convert.ToInt32(_configurations.IndividualAuthorizationSahelConfiguration.TimeIntervalToCheckRequests);
            DateTime timeToCheck = DateTime.Now.AddSeconds(interval);

            try
            {
                _logger.LogInformation("start fetching the data");

                List<IndividualAuthorizationRequest>? recentIARequestsFromSahel = await _eServicesContext
                    .Set<IndividualAuthorizationRequest>()
                    .Include(p => p.Actions)
                    .Where(p => p.AppliedBySahel == true /*&&
                            (p.CreatedAt >= timeToCheck || p.LastModifiedAt >= timeToCheck)*/ &&
                            statusEnums.Contains(p.StateId)
                            )
                    .ToListAsync();

                _logger.LogInformation("number of records: {records}", recentIARequestsFromSahel.Count);


                foreach (var request in recentIARequestsFromSahel)
                {
                    _logger.LogInformation("started request number: {request}", request.RequestNumber);

                    var result = await VerifyIndividualRequest(request.Id, true);

                    _logger.LogInformation($"completed request number: {request.RequestNumber} - {Newtonsoft.Json.JsonConvert.SerializeObject(result)}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Sahel-Service");
                string message = ex.Message;
                string innerException = ex.InnerException != null ? ex.InnerException.Message : "";
                var error = $"exception: {message} ---- inner exception: {innerException}";
                Console.WriteLine(error);
            }

            _logger.LogInformation("completed all requests");

        }

        public async Task<VerifyKMIDSharedFunctionResponse> VerifyIndividualRequest(long requestId, bool fromSahel = false)
        {
            VerifyKMIDSharedFunctionResponse response = new VerifyKMIDSharedFunctionResponse()
            {
                Success = false
            };

            string operatorName = string.Empty;
            bool updateRequest = false;


            var authorizationRequest = await _eServicesContext
                .Set<IndividualAuthorizationRequest>()
                .Include(p => p.Type)
                .Include(p => p.Port)
                .Include(p => p.Actions)
                .Where(p => p.Id == requestId)
                .FirstOrDefaultAsync();
            if (authorizationRequest == null)
            {
                response.ExceptionResourceName = "SomethingWentWrong";
                response.Result = null;
                return response;
            }

            VerificationResponseDto verificationResponse = new VerificationResponseDto();
            KgacpacipersonIdentity requesterpersonalData = new KgacpacipersonIdentity();
            KgacpacipersonIdentity authorizerPersoanlData = new KgacpacipersonIdentity();

            bool requesterApproved = authorizationRequest.RequesterKMIDApprovedDate.HasValue;
            bool authorizerApproved = authorizationRequest.AuthorizerKMIDApprovedDate.HasValue;

            if (authorizationRequest.StateId == (int)IndividualAuthorizationStatusEnum.PendingAuthorization ||
                authorizationRequest.StateId == (int)IndividualAuthorizationStatusEnum.PendingAuthorizerApproval ||
                authorizationRequest.StateId == (int)IndividualAuthorizationStatusEnum.PendingRequesterApproval ||
                authorizationRequest.StateId == (int)IndividualAuthorizationStatusEnum.AuthorizationCompleted
    )
            {
                if (!requesterApproved)
                {
                    _logger.LogInformation("requester not approved");
                    string KMIDStatus = KMIDVerification(authorizationRequest.RequesterToken);

                    if (KMIDStatus == KMIDVerificationStatusEnum.KMIDRequestPassed.ToString())
                    {
                        _logger.LogInformation("requester not approved - KMIDRequestPassed");
                        requesterApproved = true;
                        updateRequest = true;
                        await AddAction(
                            requestId: requestId,
                            authorizationRequest: authorizationRequest,
                            status: IndividualAuthorizationStatusEnum.PendingAuthorizerApproval,
                            action: IndividualAuthorizationStatusEnum.RequesterApproval);
                    }
                    else if (KMIDStatus == KMIDVerificationStatusEnum.KMIDRequestRejected.ToString())
                    {
                        _logger.LogInformation("requester not approved - KMIDRequestRejected");
                        requesterApproved = false;
                        updateRequest = true;
                        await AddAction(
                            requestId: requestId,
                            authorizationRequest: authorizationRequest,
                            status: IndividualAuthorizationStatusEnum.RequesterReject,
                            action: IndividualAuthorizationStatusEnum.RequesterReject);
                    }
                    else
                    {
                        _logger.LogInformation("requester not approved - requesterApproved = false");
                        requesterApproved = false;
                    }
                    _requestLogger.LogInformation(
                        message: "requesterApproved is {0}:",
                        propertyValues: requesterApproved);
                }
                if (!authorizerApproved)
                {
                    _logger.LogInformation("authorizer not approved");
                    string KMIDStatus = KMIDVerification(authorizationRequest.AuthorizerToken);
                    if (KMIDStatus == KMIDVerificationStatusEnum.KMIDRequestPassed.ToString())
                    {
                        _logger.LogInformation("authorizer not approved - KMIDRequestPassed");
                        authorizerApproved = true;
                        updateRequest = true;
                        await AddAction(
                            requestId: requestId,
                            authorizationRequest: authorizationRequest,
                            status: IndividualAuthorizationStatusEnum.PendingRequesterApproval,
                            action: IndividualAuthorizationStatusEnum.AuthorizerApproval);
                    }
                    else if (KMIDStatus == KMIDVerificationStatusEnum.KMIDRequestRejected.ToString())
                    {
                        _logger.LogInformation("authorizer not approved - KMIDRequestRejected");
                        authorizerApproved = false;
                        updateRequest = true;
                        await AddAction(
                            requestId: requestId,
                            authorizationRequest: authorizationRequest,
                            status: IndividualAuthorizationStatusEnum.AuthorizerReject,
                            action: IndividualAuthorizationStatusEnum.AuthorizerReject);
                    }
                    else
                    {
                        _logger.LogInformation("authorizer not approved - authorizerApproved = false");
                        authorizerApproved = false;
                    }

                    _requestLogger.LogInformation(
                        message: "authorizerApproved is {0}:",
                        propertyValues: authorizerApproved);
                }
            }

            if (authorizationRequest.Actions.Any(a => a.StateId == IndividualAuthorizationStatusEnum.AuthorizerReject.ToString()) &&
                authorizationRequest.Actions.Any(a => a.StateId == IndividualAuthorizationStatusEnum.RequesterReject.ToString()) &&
                !authorizationRequest.Actions.Any(a => a.StateId == IndividualAuthorizationStatusEnum.RequeterAndAuthorizerReject.ToString()))
            {
                updateRequest = true;
                await AddAction(
                    requestId: requestId,
                    authorizationRequest: authorizationRequest,
                    status: IndividualAuthorizationStatusEnum.RequeterAndAuthorizerReject,
                    action: IndividualAuthorizationStatusEnum.RequeterAndAuthorizerReject);
            }

            _requestLogger.LogInformation(
                message: "authorizerApproved && requesterApproved is {0}:",
                propertyValues: authorizerApproved && requesterApproved);

            try
            {
                if (authorizerApproved && requesterApproved &&
                        !authorizationRequest.Actions.Where(a => (a.StateId == IndividualAuthorizationStatusEnum.Approved.ToString() ||a.StateId == IndividualAuthorizationStatusEnum.AuthorizationCompleted.ToString()))
                        .Any())
                {

                    updateRequest = true;
                    await AddAction(
                        requestId: requestId,
                        authorizationRequest: authorizationRequest,
                        status: IndividualAuthorizationStatusEnum.Approved,
                        action: IndividualAuthorizationStatusEnum.Approved);


                    var id = Convert.ToInt32(authorizationRequest.RequesterToken);

                    _requestLogger.LogInformation(
                        message: "RequesterpersonalData id is: {0}",
                        propertyValues: id);

                    requesterpersonalData = await _eServicesContext
                        .Set<KgacpacipersonIdentity>()
                        .Where(p => p.KgacpaciqueueId == id)
                        .FirstOrDefaultAsync();

                    _requestLogger.LogInformation(
                        message: "RequesterpersonalData is: {0}",
                        propertyValues: requesterpersonalData == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(requesterpersonalData));

                    id = Convert.ToInt32(authorizationRequest.AuthorizerToken);

                    _requestLogger.LogInformation(
                        message: "authorizationRequest id is: {0}",
                        propertyValues: id);

                    authorizerPersoanlData = await _eServicesContext.Set<KgacpacipersonIdentity>()
                        .Where(p => p.KgacpaciqueueId == id)
                        .FirstOrDefaultAsync();

                    _requestLogger.LogInformation(
                        message: "AuthorizerPersoanlData is: {0}",
                        propertyValues: authorizerPersoanlData == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(authorizerPersoanlData, Formatting.None,
                    new JsonSerializerSettings()
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }));

                    if (requesterpersonalData == null || authorizerPersoanlData == null)
                    {
                        response.Result = null;
                        response.ExceptionResourceName = "SomethingWentWrong";
                        return response;
                        //throw new Exception(_Localizer[Resources.ResourcesEnum.SomethingWentWrong]);
                    }

                    _requestLogger.LogInformation(
                    message: "verificationResponse is {0}:",
                    propertyValues: verificationResponse);
                }

            }
            catch (DbUpdateException e)
            {
                _requestLogger.LogException(e, "KMID");
            }
            catch (DbEntityValidationException e)
            {
                _requestLogger.LogException(e, "KMID");
            }
            catch (NullReferenceException e)
            {
                _requestLogger.LogException(e, "KMID");
            }
            catch (ArgumentNullException e)
            {
                _requestLogger.LogException(e, "KMID");
            }
            catch (Exception e)
            {
                _requestLogger.LogException(e, "KMID");
            }
            _requestLogger.LogInformation(
            message: "updateRequest is {0}:",
            propertyValues: updateRequest);

            if (updateRequest)
            {
                authorizationRequest.LastModifiedAt = DateTime.Now;
                _eServicesContext.Set<IndividualAuthorizationRequest>().Update(authorizationRequest);
                var commited = await _eServicesContext.SaveChangesAsync();
                if (commited > 0 && fromSahel)
                {
                    Notification notification = new Notification()
                    {
                        dataTableAr = null,
                        dataTableEn = null,
                        isForSubscriber = "true",
                        subscriberCivilId = authorizationRequest.RequesterCivilId,
                        notificationType = _sahelConfigurations.IndividualAuthorizationConfiguration.NotificationTypeForIA,
                        actionButtonRequestList = new()
                        //actionButtonRequestList = actionButtons,
                    };
                    switch (authorizationRequest.StateId)
                    {
                        case (int)IndividualAuthorizationStatusEnum.RequesterReject:
                            notification.bodyAr = $"تم رفض طلب المصادقة على الطلب رقم {authorizationRequest.RequestNumber} من قبل مقدم الطلب (المفوِض)";
                            notification.bodyEn = $"Verification request for request number {authorizationRequest.RequestNumber} has been rejected by the requester";
                            break;
                        case (int)IndividualAuthorizationStatusEnum.AuthorizerReject:
                            notification.bodyAr = $"تم رفض طلب المصادقة على الطلب رقم {authorizationRequest.RequestNumber} من قبل المفوض له";
                            notification.bodyEn = $"Verification request for request number {authorizationRequest.RequestNumber} has been rejected by the authorizer";
                            break;
/*                        case (int)IndividualAuthorizationStatusEnum.RequeterAndAuthorizerReject:
                            notification.bodyAr = $"تم رفض طلب المصادقة  على الطلب رقم {authorizationRequest.RequestNumber} من قبل مقدم الطلب(المفوِض) و من قبل المفوض له";
                            notification.bodyEn = $"Verification request for request number {authorizationRequest.RequestNumber} has been rejected by the requester and the authorizer";
                            break;*/
/*                        case (int)IndividualAuthorizationStatusEnum.PendingAuthorizerApproval:
                            notification.bodyAr = $"تمت الموافقة على طلب المصادقة على الطلب رقم {authorizationRequest.RequestNumber} من قبل المفوض, و بإنتظار رد المفوض لة";
                            notification.bodyEn = $"Verification request for request number {authorizationRequest.RequestNumber} has been accepted by the requester and waiting for the authorizer approval";
                            break;*/
/*                        case (int)IndividualAuthorizationStatusEnum.PendingRequesterApproval:
                            notification.bodyAr = $"تمت الموافقة على طلب المصادقة رقم{authorizationRequest.RequestNumber} من قبل المفوض له, و بإنتظار رد المفوض ";
                            notification.bodyEn = $"Verification request for request number {authorizationRequest.RequestNumber} has been accepted by the autorizer and waiting for the requester approval";
                            break;
                        case (int)IndividualAuthorizationStatusEnum.PendingAuthorization:
                            notification.bodyAr = $"في انتظار المصادقة على الطلب  رقم {authorizationRequest.RequestNumber} من قبل المفوض و المفوض له";
                            notification.bodyEn = $"waiting for verification for request number {authorizationRequest.RequestNumber} by the requester and authorizer";
                            break;*/
                        case (int)IndividualAuthorizationStatusEnum.Approved:
                            string url = await PrintingIndividualRequest(authorizationRequest);
                            //string encryptedRequestId = CommonFunctions.CsUploadEncrypt(authorizationRequest.Id.ToString());
                            var authPurpose = await _eServicesContext.Set<IndividualAuthReferenceType>().Where(a => a.Id == authorizationRequest.PurposeId).FirstOrDefaultAsync();
                            if (authPurpose != null)
                            {
                                notification.bodyAr = $"تمت الموافقة على  طلب التفويض رقم {authorizationRequest.RequestNumber}" +
                                    $" للمفوض له صاحب البطاقة المدنية رقم {authorizationRequest.AuthorizerCivilId} من أجل {authPurpose.ArabicName}" + $" و ينتهي صلاحيته في {authorizationRequest.ExpiryDate.ToString("dd-MM-yyyy")}";
                                notification.bodyEn = $"Authorization request with number {authorizationRequest.RequestNumber} and authorizer civil id {authorizationRequest.AuthorizerCivilId} for {authPurpose.EnglishName} and expiry date in {authorizationRequest.ExpiryDate.ToString("dd-MM-yyyy")} has been approved by the requester and the authorizer ";
                                actionButtonRequestList actionButtonRequest = new actionButtonRequestList()
                                {
                                    actionType = "details",
                                    actionUrl = url,
                                    LabelAr= "تحميل",
                                    LabelEn="details"
                                };
                                List<actionButtonRequestList> actionButtons = new List<actionButtonRequestList>();
                                actionButtons.Add(actionButtonRequest);
                                notification.actionButtonRequestList= actionButtons;
                            }
                            break;
                        default:
                            break;
                    }
                    if (string.IsNullOrEmpty(notification.bodyAr) || string.IsNullOrEmpty(notification.bodyEn))
                    {
                        string authorizationRequestStr = JsonConvert.SerializeObject(authorizationRequest);
                        _logger.LogInformation($"Authorization Request is empty or null ==>{authorizationRequestStr}");
                    }

                  
                    bool isSent =  PostNotification(notification, SahelOptionsTypesEnum.Individual.ToString());
                    await InsertNotification(notification, isSent);
                    notification.subscriberCivilId = authorizationRequest.AuthorizerCivilId;

                    //notification.actionButtonRequestList = null;

                    isSent =  PostNotification(notification, SahelOptionsTypesEnum.Individual.ToString());
                    await InsertNotification( notification, isSent);

                }

            }

            if (authorizationRequest.Actions.Any(a => a.StateId == IndividualAuthorizationStatusEnum.RequeterAndAuthorizerReject.ToString()))
            {
                response.Success = false;
                response.ExceptionResourceName = "VerificationRequestRejectedbyRequesterAndAuthorizer";
                response.Result = null;
                return response;
                //throw new BusinessRuleException(_Localizer[Resources.ResourcesEnum.VerificationRequestRejectedbyRequesterAndAuthorizer]);
            }
            if (authorizationRequest.Actions.Any(a => a.StateId == IndividualAuthorizationStatusEnum.AuthorizerReject.ToString()) &&
                authorizationRequest.Actions.Any(a => a.StateId == IndividualAuthorizationStatusEnum.RequesterApproval.ToString()))
            {
                response.Success = false;
                response.ExceptionResourceName = "VerificationRequestRejectedbyAuthorizer";
                response.Result = null;
                return response;
                //throw new BusinessRuleException(_Localizer[Resources.ResourcesEnum.VerificationRequestRejectedbyAuthorizer]);
            }
            if (authorizationRequest.Actions.Any(a => a.StateId == IndividualAuthorizationStatusEnum.RequesterReject.ToString()) &&
                authorizationRequest.Actions.Any(a => a.StateId == IndividualAuthorizationStatusEnum.AuthorizerApproval.ToString()))
            {
                response.Success = false;
                response.ExceptionResourceName = "VerificationRequestRejectedbyRequester";
                response.Result = null;
                return response;
                //throw new BusinessRuleException(_Localizer[Resources.ResourcesEnum.VerificationRequestRejectedbyRequester]);
            }
            return new VerifyKMIDSharedFunctionResponse();
        }

        public bool PostNotification(Notification notification, string SahelOption = "Business")
        {
            if(string.IsNullOrEmpty(notification.bodyAr) && string.IsNullOrEmpty(notification.bodyAr))
            {
                return false;
            }
            string notificationString=JsonConvert.SerializeObject(notification);
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
                    Task<HttpResponseMessage> postTask = client.PostAsJsonAsync<Notification>("single", notification);
/*                    var notificationString = JsonConvert.SerializeObject(notification);
                    _logger.LogInformation(notificationString);*/
                    return !string.IsNullOrEmpty(getResult(postTask));
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
                SahelType = "I", //Individual
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

        public async Task AddAction(long requestId, IndividualAuthorizationRequest authorizationRequest,
         IndividualAuthorizationStatusEnum status, IndividualAuthorizationStatusEnum action)
        {
            //_logger.LogInformation($"Date time now {DateTime.Now}");
            //_logger.LogInformation($"Date time now {DateTime.UtcNow}");
            if (status == IndividualAuthorizationStatusEnum.PendingAuthorizerApproval)
            {
                if (authorizationRequest.Actions.Any(a => a.StateId == IndividualAuthorizationStatusEnum.AuthorizerReject.ToString()))
                {
                    status = IndividualAuthorizationStatusEnum.AuthorizerReject;
                }
                authorizationRequest.RequesterKMIDApprovedDate = DateTime.Now;
            }
            if (status == IndividualAuthorizationStatusEnum.PendingRequesterApproval)
            {
                if (authorizationRequest.Actions.Any(a => a.StateId == IndividualAuthorizationStatusEnum.RequesterReject.ToString()))
                {
                    status = IndividualAuthorizationStatusEnum.RequesterReject;
                }
                authorizationRequest.AuthorizerKMIDApprovedDate = DateTime.Now;
            }
            if (status == IndividualAuthorizationStatusEnum.Approved)
            {
                authorizationRequest.ExpiryDate = DateTime.Now.AddDays(_configurations.IndividualAuthorizationSahelConfiguration.IndividualAuthorizationExpirationDays);
                authorizationRequest.ApprovedDate = DateTime.Now;
                authorizationRequest.SubmissionDate = DateTime.Now;
            }

            authorizationRequest.StateId = (int)status;
            _eServicesContext.Set<IndividualAuthorizationRequest>().Update(authorizationRequest);

            var individualAuthorizationRequestsAction = new IndividualAuthorizationRequestsAction()
            {
                IndividualAuthRequestId = requestId,
                StateId = action.ToString(),
                IpAddress = GetIpAddress(),
                CreatedAt = DateTime.Now
            };
            _logger.LogInformation($"Date time utc now {DateTime.UtcNow}");

            individualAuthorizationRequestsAction.AuthorizerNotified = true;
            individualAuthorizationRequestsAction.RequesterNotified = true;

            authorizationRequest.Actions.Add(individualAuthorizationRequestsAction);
        }

        public string GetIpAddress()
        {
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            string _ipAddress = string.Empty;
            foreach (IPAddress ipAddress in localIPs)
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    _ipAddress = ipAddress.ToString();
                }
            }
            return _ipAddress;
        }

        public string KMIDVerification(string token)
        {
            string kMIDStatus = null;
            var data = GetKMIDVerificationStatusFromDB(token);
            if (data.Rows.Count == 1)
            {
                string status = data.Rows[0]["KMIDStatusDetails"].ToString();

                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (status.Equals("Accept"))
                    {
                        kMIDStatus = KMIDVerificationStatusEnum.KMIDRequestPassed.ToString();
                    }
                    else if (status.Equals("Decline"))
                    {
                        kMIDStatus = KMIDVerificationStatusEnum.KMIDRequestRejected.ToString();
                    }
                }
            }
            return kMIDStatus;
        }

        public DataTable GetKMIDVerificationStatusFromDB(string sCSVTokens)
        {
            DataTable result = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(sCSVTokens))
                {
                    using (SqlConnection connection = new SqlConnection(_configurations.ConnectionStrings.Default))
                    {
                        using SqlCommand sqlCommand = new SqlCommand("[etrade].[sp_GetKMIDVerificationStatus]", connection);
                        sqlCommand.CommandType = CommandType.StoredProcedure;
                        sqlCommand.Parameters.Add("@csvTokens", SqlDbType.NVarChar).Value = sCSVTokens;
                        DataSet dataSet = new DataSet();
                        SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(sqlCommand);
                        sqlDataAdapter.Fill(dataSet);
                        result = dataSet.Tables[0];
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Sahel-Service");
                CommonFunctions.LogUserActivity("GetKMIDVerificationStatusFromDB", "", "", "", "", ex.Message.ToString());
            }

            return result;
        }

        public async Task<string> PrintingIndividualRequest(IndividualAuthorizationRequest request)
        {
            List<ParameterModel> parameterModel = new List<ParameterModel>();
            QRCodeReportDataModel jsonData = new QRCodeReportDataModel();
            List<ParameterHeaderModel> parameterHeaderModel = new List<ParameterHeaderModel>();

            var requesterPersolnalData = await _eServicesContext
               .Set<KgacpacipersonIdentity>()
              .Where(p => p.KgacpaciqueueId == Convert.ToInt32(request.RequesterToken))
              .FirstOrDefaultAsync();

            parameterModel.Add(new ParameterModel
            {
                Key = "RequestId",
                Value = request.Id.ToString()
            });
            parameterModel.Add(new ParameterModel
            {
                Key = "PrintedBy",
                Value = requesterPersolnalData.FullNameAr
            });

            parameterHeaderModel.Add(new ParameterHeaderModel
            {
                ReferenceId = request.Id.ToString(),
                ReferenceNumber = request.RequestNumber,
                Parameter = parameterModel
            });
            DateTime? issuedDate = request.SubmissionDate;
            string x = issuedDate.ToString("O");
            jsonData.QRReportConfigId = _sahelConfigurations.IndividualAuthorizationConfiguration.QRReportConfigId;
            jsonData.ReferenceType = _sahelConfigurations.IndividualAuthorizationConfiguration.ReferenceType;
            jsonData.IssuedToUser = requesterPersolnalData.FullNameAr;
            jsonData.IssuedDate = x;
            jsonData.OwnerOrgId = "0";
            jsonData.OwnerLocId = request.PortId.ToString();
            jsonData.Parameters = parameterHeaderModel;

            string json = System.Text.Json.JsonSerializer.Serialize(jsonData, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(UnicodeRanges.All)
            });

            var QrTokens = await QRCodeTokenGenerator(json, request.RequesterCivilId);


            var url = string.Format(_sahelConfigurations.IndividualAuthorizationConfiguration.IndividualQRReportUrl, QrTokens.Token_1, QrTokens.Token_2);
            return url;
        }
        public class QRCodeReportDataModel
        {
            public string QRReportConfigId { get; set; }
            public string ReferenceType { get; set; }
            public string IssuedToUser { get; set; }
            public string IssuedDate { get; set; }
            public string OwnerOrgId { get; set; }
            public string OwnerLocId { get; set; }
            public List<ParameterHeaderModel> Parameters { get; set; }
        }

        public class ParameterHeaderModel
        {
            public string ReferenceId { get; set; }
            public string ReferenceNumber { get; set; }
            public List<ParameterModel> Parameter { get; set; }
        }

        public class ParameterModel
        {
            public string Key { get; set; }
            public string Value { get; set; }
        }

        public async Task<QRToken> QRCodeTokenGenerator(string jsondata, string sUserId)//,string sIp
        {
            Dictionary<string, string> returnData = new Dictionary<string, string>();
            DataSet Ds = new DataSet();
            string sRresult = string.Empty;
            string sStatus = string.Empty;
            String id1 = String.Empty;
            String id2 = String.Empty;
            QRToken obj = new QRToken();
            try
            {

                int j = 1;
                for (int i = 0; i < j;)
                {
                    id1 = fnGenerateToken();
                    id2 = fnGenerateToken();
                    sStatus = ValidateToken(id1, id2);
                    if (sStatus == "1")
                    {
                        i++;
                        j++;
                    }
                    else
                        break;
                }

                try
                {
                    var dapperParameters = new DynamicParameters();
                    dapperParameters.Add(StoredProcedureNames.GenarateQRToken.GenarateQRTokenParamerters.JSONData.Name, jsondata);
                    dapperParameters.Add(StoredProcedureNames.GenarateQRToken.GenarateQRTokenParamerters.NoOfCalls.Name, 1000);
                    dapperParameters.Add(StoredProcedureNames.GenarateQRToken.GenarateQRTokenParamerters.IpAdress.Name, GetIpAddress());
                    dapperParameters.Add(StoredProcedureNames.GenarateQRToken.GenarateQRTokenParamerters.CreatedBy.Name, sUserId);
                    dapperParameters.Add(StoredProcedureNames.GenarateQRToken.GenarateQRTokenParamerters.Token_1.Name, Convert.ToInt64(id1));
                    dapperParameters.Add(StoredProcedureNames.GenarateQRToken.GenarateQRTokenParamerters.Token_2.Name, Convert.ToInt64(id2));

                    dapperParameters.Add(StoredProcedureNames.GenarateQRToken.GenarateQRTokenParamerters.Result.Name, dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);
                    var result = await _dapper.Get<int>(StoredProcedureNames.GenarateQRToken.Name, dapperParameters);

                    sRresult = Convert.ToString(result);
                    if (!string.IsNullOrEmpty(sRresult))
                    {
                        obj.status = sRresult;
                        obj.Token_1 = id1;
                        obj.Token_2 = id2;
                    }

                    return obj;
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Sahel-Service");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Sahel-Service");
                throw;
            }
        }
        public static String fnGenerateToken()
        {
            String sfirst = "9";
            String dateString1 = DateTime.Now.ToString("ddyyMM");
            String rtoc = GetRandomKey(6);

            int Part1 = int.Parse(sfirst.ToString());

            int Part2 = int.Parse(dateString1[0].ToString());
            int Part3 = int.Parse(dateString1[1].ToString());
            int Part4 = int.Parse(dateString1[2].ToString());
            int Part5 = int.Parse(dateString1[3].ToString());
            int Part6 = int.Parse(dateString1[4].ToString());
            int Part7 = int.Parse(dateString1[5].ToString());

            int Part8 = int.Parse(rtoc[0].ToString());
            int Part9 = int.Parse(rtoc[1].ToString());
            int Part10 = int.Parse(rtoc[2].ToString());
            int Part11 = int.Parse(rtoc[3].ToString());
            int Part12 = int.Parse(rtoc[4].ToString());
            int Part13 = int.Parse(rtoc[5].ToString());

            int calculation = 0;

            calculation = calculation + (2 * Part1) + (1 * Part2) + (6 * Part3) + (3 * Part4) + (7 * Part5) +
                    (9 * Part6) + (10 * Part7) + (5 * Part8) + (8 * Part9) + (4 * Part10) + (2 * Part11) + (1 * Part12) + (6 * Part13);
            calculation = calculation % 13;
            calculation = 13 - calculation;
            String scalculation = calculation.ToString("000");

            String Token = Part1.ToString() + Part2.ToString() + Part3.ToString() + Part4.ToString() + Part5.ToString() + Part6.ToString() + Part7.ToString()
                            + Part8.ToString() + Part9.ToString() + Part10.ToString() + Part11.ToString() + Part12.ToString() + Part13.ToString() + scalculation;

            return Token;
        }
        private static String GetRandomKey(int len)
        {
            int maxSize = len;
            char[] chars = new char[30];
            String a;
            a = "1234567890";
            chars = a.ToCharArray();
            int size = maxSize;
            byte[] data = new byte[1];
            RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider();
            crypto.GetNonZeroBytes(data);
            size = maxSize;
            data = new byte[size];
            crypto.GetNonZeroBytes(data);
            StringBuilder result = new StringBuilder(size);
            foreach (byte b in data) { result.Append(chars[b % (chars.Length)]); }
            return result.ToString();
        }
        public string ValidateToken(string sToken_1, string sToken_2)
        {
            DataSet Ds = new DataSet();
            string sStatus = string.Empty;
            try
            {
                using (var connectDB = new SqlConnection(_configurations.ConnectionStrings.Default))
                using (var commandDB = new SqlCommand("[dbo].Usp_ValidateQRTokens", connectDB))
                using (var dataAdapter = new SqlDataAdapter(commandDB))
                {
                    commandDB.CommandType = CommandType.StoredProcedure;
                    commandDB.Parameters.Add("@Token_1", SqlDbType.NVarChar).Value = sToken_1;
                    commandDB.Parameters.Add("@Token_2", SqlDbType.NVarChar).Value = sToken_2;
                    dataAdapter.Fill(Ds);
                    if (Ds.Tables.Count > 0 && Ds.Tables[0].Rows.Count > 0)
                    {
                        sStatus = Ds.Tables[0].Rows[0]["Status"].ToString();
                    }
                    connectDB.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Sahel-Service");
                throw;
            }
            return sStatus;
        }
    }
}
