using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.BrokerEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;
using static eServicesV2.Kernel.Infrastructure.Persistence.Constants.StoredProcedureNames.UpdateMigrationStatus.UpdateMigrationStatusParamerters;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Enums;

namespace sahelIntegrationIA
{
    public class VerificationServiceForSignUp
    {
        private readonly IRequestLogger _logger;
        private readonly IBaseConfiguration _configurations;
        private readonly eServicesContext _eServicesContext;
        private readonly SahelConfigurations _sahelConfigurations;
        private Dictionary<int, string> requestedCivilIds = new();

        public VerificationServiceForSignUp(IRequestLogger logger,
                                                          IBaseConfiguration configuration,
                                                          eServicesContext eServicesContext,
                                                          SahelConfigurations sahelConfigurations)
        {
            _logger = logger;
            _configurations = configuration;
            _eServicesContext = eServicesContext;
            _sahelConfigurations = sahelConfigurations;
        }
        private async Task<List<SahelPreSignUpDetails>> GetSignUpRequests()
        {
            _logger.LogInformation("Start fetching data for signUp  service");

            var requestList = await _eServicesContext
                               .Set<SahelPreSignUpDetails>()
                               .Where(p => !string.IsNullOrEmpty(p.KMIDToken)
                                           && !p.OTPSent.Value
                                           && !p.NotificationSent
                                            )
                                .ToListAsync();


            var kmidTokens = requestList.Select(a=> a.KMIDToken).ToList();

            var currentTime = DateTime.Now;

            var expiredKmidRequests = await _eServicesContext.Set<KGACPACIQueue>()
              .Where(a => kmidTokens.Contains(a.KGACPACIQueueId.ToString())
                          && a.DateCreated.AddSeconds(_sahelConfigurations.BrokerKMIDCallingTimer) < currentTime)
              .Select(a => a.KGACPACIQueueId)
              .ToListAsync();

            var expiredRequest = requestList
                    .Where(a => expiredKmidRequests.Contains(Convert.ToInt32(a.KMIDToken)))
                    .ToList();
            if (expiredKmidRequests.Any())
            {
                await SendExpiredKmidNotification(expiredRequest);


                foreach (var request in expiredRequest)
                {
                    var expiredRequestsIds = expiredRequest
                        .Select(a => a.KMIDToken)
                        .ToList();

                    await _eServicesContext
                                   .Set<SahelPreSignUpDetails>()
                                   .Where(a => expiredRequestsIds.Contains(a.KMIDToken))
                                   .ExecuteUpdateAsync(a => a.SetProperty(b => b.KMIDToken, string.Empty));

                }


            }

            var filteredRequests = requestList
                .Where(request => !expiredKmidRequests.Contains(int.Parse(request.KMIDToken)))
                .ToList();

            return filteredRequests;

        }
        public async Task CheckBrokerRequests()
        {
            _logger.LogInformation("Start Broker Verification Service");


            var requests = await GetSignUpRequests();

            var tasks = requests.Select(async serviceRequest =>
            {
                try
                {
                    await ProcessServiceRequestAndCallAPI(serviceRequest);
                }

                catch (Exception ex)
                {
                    _logger.LogException(ex, "Sahel-Windows-Service");
                }

            });

            await Task.WhenAll(tasks);
        }
 
        private async Task ProcessServiceRequestAndCallAPI(SahelPreSignUpDetails serviceRequest)
        {
            _logger.LogInformation("Start processing service for civilId: {CivilId}", serviceRequest.CivilId);

            string url = _sahelConfigurations.EservicesUrlsConfigurations.SubmitBrokerSignUpUrl;
            await CallServiceAPI(CreateRequestObject(serviceRequest), url);

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
        private async Task SendExpiredKmidNotification(List<SahelPreSignUpDetails> serviceRequest)
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

            await Task.WhenAll(tasks);
        }
        private async Task CreateNotification(SahelPreSignUpDetails serviceRequest)
        {
            _logger.LogInformation("start Notification creation process for expired kmid");

            Notification notficationResponse = new();
            string msgAr = string.Empty;
            string msgEn = string.Empty;
            _logger.LogInformation("Create Notification message for signup expired KMID");

            // string civilID = requestedCivilIds.First(a => a.Key == serviceRequest.ServiceRequestsDetail.EserviceRequestDetailsId).Value;
            string civilID = serviceRequest.CivilId;

            _logger.LogInformation("Get  civil Id{0}", civilID);

            msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.SignUpKmidExpiredAr,
                serviceRequest.CivilId);
            msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.SignUpKmidExpiredEn,
                serviceRequest.CivilId);

            _logger.LogInformation("Notification Message in Arabic : {0} , Notification Message in English {1}",
                 msgAr, msgEn);

            var notificationType = ((int)SahelNotficationTypesEnum.BrokerSignUp).ToString();

            notficationResponse.bodyEn = msgAr;
            notficationResponse.bodyAr = msgEn;
            notficationResponse.isForSubscriber = "true";
            notficationResponse.dataTableEn = null;
            notficationResponse.dataTableAr = null;
            notficationResponse.subscriberCivilId = civilID;
            notficationResponse.notificationType = notificationType;

            string log = JsonConvert.SerializeObject(notficationResponse, Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            _logger.LogInformation(message: "Preparing KMID expiration notification {0}", log);

            _logger.LogInformation("start notification: {0}", serviceRequest.CivilId);

            bool isSent = PostNotification(notficationResponse, "Business");
            await InsertNotification(notficationResponse, isSent);

            var kmidToken = serviceRequest.KMIDToken;

            await _eServicesContext
                               .Set<SahelPreSignUpDetails>()
                               .Where(a => a.KMIDToken == kmidToken)
                               .ExecuteUpdateAsync(a => a.SetProperty(b => b.NotificationSent, true));

            _logger.LogInformation("end notification token: {0}", serviceRequest.KMIDToken);

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

        private  SignUpDetailsDto CreateRequestObject(SahelPreSignUpDetails request)
        {
            SignUpDetailsDto signUpObj = new SignUpDetailsDto();
            signUpObj.KMIDToken = request.KMIDToken;
            signUpObj.MobileNumber = request.MobileNumber;
            signUpObj.Password = request.Password;
            signUpObj.Email = request.Email;
            signUpObj.Id    = request.Id;
            signUpObj.Gender = request.Gender;
            signUpObj.CivilId = request.CivilId;
            return signUpObj;

        }
        #endregion
        #region Dtos
        public class SignUpDetailsDto
        {
            public int Id { get; set; }

            public string CivilId { get; set; }

            public string Password { get; set; }


            public string Email { get; set; }


            public string MobileNumber { get; set; }


            public string Gender { get; set; }

            public string KMIDToken { get; set; }
        }
        #endregion
    }
}
