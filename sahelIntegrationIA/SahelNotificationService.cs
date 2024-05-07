using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sahelIntegrationIA.Configurations;
using System.Net.Http.Json;
using System.Net;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;
using sahelIntegrationIA.Models;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;

namespace sahelIntegrationIA
{
    public class SahelNotificationService
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

        public SahelNotificationService(
            IRequestLogger logger,
            IBaseConfiguration configuration,
            eServicesContext eServicesContext,
            IRequestLogger requestLogger,
            IDapper dapper,
            SahelConfigurations sahelConfigurations)
        {
            _logger = logger;
            _configurations = configuration;
            _eServicesContext = eServicesContext;
            _requestLogger = requestLogger;
            _dapper = dapper;
            _sahelConfigurations = sahelConfigurations;

        }


        public async Task<List<KGACSahelOutSyncQueue>> GetNotifications()
        {
            _logger.LogInformation("SahelNotificationService - start Notifications For Sahel");

            var notificationList = await _eServicesContext
                               .Set<KGACSahelOutSyncQueue>()
                               .Where(x => (x.Source == "MC"
                                                  && x.Sync.Value != true
                                                  && x.TryCount.Value < _sahelConfigurations.TryCountForMCNotification)
                                           ||( x.Source=="eService"
                                               && x.Sync!=true 
                                               && x.TryCount.Value < _sahelConfigurations.TryCountForeServiceNotification))
                               .AsNoTracking()
                               .ToListAsync();

            string log = JsonConvert.SerializeObject(notificationList, Formatting.None,
                        new JsonSerializerSettings()
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });

            _logger.LogInformation(message: $"SahelNotificationService - start Notifications For Sahel {0}", log);


            return notificationList;
        }


        public async Task SendNotification()
        {
            _logger.LogInformation($"SahelNotificationService - start send notification for sahel service");

            var notificationList = await GetNotifications();

            var tasks = notificationList.Select(async notification =>
            {
                try
                {
                    await ProcessServiceRequest(notification);
                }

                catch (Exception ex)
                {
                    _logger
                    .LogException(ex, "SahelNotificationService - notification sahel exception - {0}",
                    notification.KGACSahelOutSyncQueueId);
                }

            });
            await Task.WhenAll(tasks);
        }


        private async Task ProcessServiceRequest(KGACSahelOutSyncQueue notification)
        {
            await CreateNotification(notification);
        }

        private async Task CreateNotification(KGACSahelOutSyncQueue notification)
        {
            var reqJson = JsonConvert.SerializeObject(notification, Formatting.None,
             new JsonSerializerSettings()
             {
                 ReferenceLoopHandling = ReferenceLoopHandling.Ignore
             });

            _logger.LogInformation("SahelNotificationService - start Notification creation process - {0}",
                propertyValues: new { reqJson });

            Notification notficationResponse = new Notification();

            notficationResponse.bodyEn = notification.MsgBodyAr;
            notficationResponse.bodyAr = notification.MsgBodyEn;
            notficationResponse.isForSubscriber = "true";
            notficationResponse.notificationType = notification.NotificationId.ToString();
            notficationResponse.subscriberCivilId = notification.CivilId;

            try
            {
                notficationResponse.dataTableEn = JsonConvert.DeserializeObject<Dictionary<string, string>>(notification.MsgTableEn);
                notficationResponse.dataTableAr = JsonConvert.DeserializeObject<Dictionary<string, string>>(notification.MsgTableAr);
            }
            catch (JsonException ex)
            {
                //Add log
                notficationResponse.dataTableEn = new Dictionary<string, string> { { "Header", notification.MsgTableEn } };
                notficationResponse.dataTableAr = new Dictionary<string, string> { { "عنوان", notification.MsgTableAr } };
            }

            string log = JsonConvert.SerializeObject(notficationResponse, Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            _logger.LogInformation("SahelNotificationService - Preparing notification {0} - {1}",
                propertyValues: new { notification.KGACSahelOutSyncQueueId, log });

            var sahelOption = notification.SahelType.ToLower() switch
            {
                "i" => "Individual",
                "b" => "Business",
                _ =>string.Empty
            };

            if (string.IsNullOrEmpty(sahelOption))
            {
                _logger.LogException(new ArgumentException("Invalid SahelType"),
               "SahelNotificationService - notification sahel exception - {0} - {1}",
               notification.SahelType, notification.KGACSahelOutSyncQueueId);
                return;
            }

            var sendNotificationResult = PostNotification(notficationResponse, notification.KGACSahelOutSyncQueueId, sahelOption);

            if (sendNotificationResult)
            {
                _logger
                    .LogInformation("SahelNotificationService - notification sent successfully: {0}", notification.KGACSahelOutSyncQueueId);

                await _eServicesContext
                                  .Set<KGACSahelOutSyncQueue>()
                                  .Where(a => a.KGACSahelOutSyncQueueId == notification.KGACSahelOutSyncQueueId)
                                  .ExecuteUpdateAsync(a => a
                                  .SetProperty(b => b.Sync, true)
                                  .SetProperty(b => b.TryCount, notification.TryCount + 1)
                                  .SetProperty(b => b.DateModified, DateTime.Now));
            }
            else
            {
                _logger
                    .LogInformation("SahelNotificationService - notification sent faild: {0}", notification.KGACSahelOutSyncQueueId);

                await _eServicesContext
                                  .Set<KGACSahelOutSyncQueue>()
                                  .Where(a => a.KGACSahelOutSyncQueueId == notification.KGACSahelOutSyncQueueId)
                                  .ExecuteUpdateAsync(a => a
                                  .SetProperty(b => b.TryCount, notification.TryCount + 1)
                                  .SetProperty(b => b.DateModified, DateTime.Now));
            }
            _logger.LogInformation(message: "SahelNotificationService - end notification: {0}", notification.KGACSahelOutSyncQueueId);

        }


        #region Private Methods

        private bool PostNotification(Notification notification, int notificationID, string SahelOption = "Business")
        {
            if (string.IsNullOrEmpty(notification.bodyAr) && string.IsNullOrEmpty(notification.bodyEn))
            {
                _logger
                    .LogInformation("SahelNotificationService - Can't send notification because the body is empty - {0}",
                    notificationID);
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

                    string rEsult = getResult(postTask);
                    //_logger.LogInformation($"{_jobCycleId} - ShaleNotificationMC - notification result: {0} - {1}",
                    //    propertyValues: new object[] { requestNumber, rEsult });

                    return !string.IsNullOrEmpty(rEsult);
                }
            }
            return false;
        }

        private string GenerateToken(string SahelOption)
        {
            var confi = _configurations.IndividualAuthorizationSahelConfiguration;
            string token = string.Empty;

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
