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
    public class SendMCNotificationForSahelService
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

        public SendMCNotificationForSahelService(
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
            _logger.LogInformation("MCNotificationForSahelService - start MC Notifications For Sahel");

            var notificationList = await _eServicesContext
                               .Set<KGACSahelOutSyncQueue>()
                               .Where(x => x.Sync.Value != true
                                           && x.TryCount.Value < 3)
                               .AsNoTracking()
                               .ToListAsync();

            string log = JsonConvert.SerializeObject(notificationList, Formatting.None,
                        new JsonSerializerSettings()
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });

            _logger.LogInformation(message: $"MCNotificationForSahelService - MC Notifications For Sahel {0}", log);


            return notificationList;
        }


        public async Task SendNotification()
        {
            _logger.LogInformation($"MCNotificationForSahelService - start send mc notification for sahel service");

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
                    .LogException(ex, "MCNotificationForSahelService - mc notification sahel exception - {0}",
                    notification.KGACSahelOutSyncQueueId);
                }

            });
            await Task.WhenAll(tasks);
        }


        private async Task ProcessServiceRequest(KGACSahelOutSyncQueue notification)
        {
            //if (serviceRequest.ServiceId is null or 0)
            //{
            //    _logger.LogException(new ArgumentException($"INVALID SERVICE ID {nameof(serviceRequest.ServiceId)}"));
            //    return; //log the error
            //}

            //if (!Enum.IsDefined(typeof(ServiceTypesEnum), (int)serviceRequest.ServiceId))
            //{
            //    _logger.LogException(new ArgumentException($"INVALID SERVICE ID {nameof(serviceRequest.ServiceId)}"));
            //    return; //log the error
            //}

            await CreateNotification(notification);

        }

        private async Task CreateNotification(KGACSahelOutSyncQueue notification)
        {
            var reqJson = JsonConvert.SerializeObject(notification, Formatting.None,
             new JsonSerializerSettings()
             {
                 ReferenceLoopHandling = ReferenceLoopHandling.Ignore
             });

            _logger.LogInformation("MCNotificationForSahelService - start Notification creation process - {0}",
                propertyValues: new { reqJson });

            Notification notficationResponse = new Notification();

            notficationResponse.bodyEn = notification.MsgBodyAr;
            notficationResponse.bodyAr = notification.MsgBodyEn;
            notficationResponse.isForSubscriber = "true";
            notficationResponse.notificationType = notification.NotificationId.ToString();
            notficationResponse.dataTableEn = new Dictionary<string, string> { { "Header", notification.MsgTableEn } };
            notficationResponse.dataTableAr = new Dictionary<string, string> { { "عنوان", notification.MsgTableAr } };
            notficationResponse.subscriberCivilId = notification.CivilId;

            string log = JsonConvert.SerializeObject(notficationResponse, Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            _logger.LogInformation("MCNotificationForSahelService - Preparing notification {0} - {1}",
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
               "MCNotificationForSahelService - mc notification sahel exception - {0} - {1}",
               notification.SahelType, notification.KGACSahelOutSyncQueueId);
                return;
            }

            var sendNotificationResult = PostNotification(notficationResponse, notification.KGACSahelOutSyncQueueId, sahelOption);

            if (sendNotificationResult)
            {
                _logger
                    .LogInformation("MCNotificationForSahelService - notification sent successfully: {0}", notification.KGACSahelOutSyncQueueId);

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
                    .LogInformation("MCNotificationForSahelService - notification sent faild: {0}", notification.KGACSahelOutSyncQueueId);

                await _eServicesContext
                                  .Set<KGACSahelOutSyncQueue>()
                                  .Where(a => a.KGACSahelOutSyncQueueId == notification.KGACSahelOutSyncQueueId)
                                  .ExecuteUpdateAsync(a => a
                                  .SetProperty(b => b.TryCount, notification.TryCount + 1)
                                  .SetProperty(b => b.DateModified, DateTime.Now));
            }
            _logger.LogInformation(message: "MCNotificationForSahelService - end notification: {0}", notification.KGACSahelOutSyncQueueId);

        }


        #region Private Methods

        private bool PostNotification(Notification notification, int notificationID, string SahelOption = "Business")
        {
            if (string.IsNullOrEmpty(notification.bodyAr) && string.IsNullOrEmpty(notification.bodyEn))
            {
                _logger
                    .LogInformation("MCNotificationForSahelService - Can't send notification because the body is empty - {0}",
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
