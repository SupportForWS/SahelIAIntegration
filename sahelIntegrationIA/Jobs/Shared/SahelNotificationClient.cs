using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;

namespace sahelIntegrationIA.Jobs.Shared
{
    /// <summary>
    /// Shared HTTP client for sending notifications to Sahel.
    /// </summary>
    public class SahelNotificationClient
    {
        private readonly IBaseConfiguration _config;
        private readonly IRequestLogger _logger;

        public SahelNotificationClient(IBaseConfiguration config, IRequestLogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.SystemDefault;
        }

        /// <summary>
        /// Waits on an HTTP task and returns the response body if successful, otherwise logs and returns empty.
        /// </summary>
        public string GetResult(Task<HttpResponseMessage> httpTask)
        {
            try
            {
                httpTask.Wait();
                var response = httpTask.Result;
                if (response.IsSuccessStatusCode)
                {
                    var readTask = response.Content.ReadAsStringAsync();
                    readTask.Wait();
                    return readTask.Result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "SahelNotificationClient.GetResult");
                var path = AppDomain.CurrentDomain.BaseDirectory;
                File.AppendAllText(
                    Path.Combine(path, "SahelClient.log"),
                    $"{DateTime.Now}: GetResult Error ==> {ex}\n");
            }
            return string.Empty;
        }

        /// <summary>
        /// Posts a Notification object to the Sahel endpoint, using a bearer token.
        /// </summary>
        public bool PostNotification(Notification notification, string requestNumber, SahelOptionsTypesEnum option)
        {
            if (string.IsNullOrEmpty(notification.bodyAr) && string.IsNullOrEmpty(notification.bodyEn))
            {
                _logger.LogInformation($"{requestNumber} - Empty notification body; skipping send.");
                return false;
            }

            var baseUrl = option == SahelOptionsTypesEnum.Individual
                ? _config.IndividualAuthorizationSahelConfiguration.TargetUrlIndividual
                : _config.IndividualAuthorizationSahelConfiguration.TargetUrlBusiness;

            using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
            var token = GenerateToken(option);
            if (string.IsNullOrEmpty(token))
                return false;

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var postTask = client.PostAsJsonAsync("single", notification);
            var result = GetResult(postTask);

            _logger.LogInformation($"{requestNumber} - PostNotification result: {result}");
            return !string.IsNullOrEmpty(result);
        }

        /// <summary>
        /// Generates a bearer token by calling the Sahel auth endpoint.
        /// </summary>
        public string GenerateToken(SahelOptionsTypesEnum option)
        {
            var conf = _config.IndividualAuthorizationSahelConfiguration;
            var tokenUrl = option == SahelOptionsTypesEnum.Individual
                ? conf.TargetURLToken
                : conf.TargetURLTokenBusiness;

            using var client = new HttpClient { BaseAddress = new Uri(tokenUrl) };
            var credentials = new authenticate
            {
                username = conf.UsernamePaci,
                password = option == SahelOptionsTypesEnum.Individual
                    ? conf.passwordIndividual
                    : conf.passwordBusiness
            };

            var authTask = client.PostAsJsonAsync("generate", credentials);
            var response = GetResult(authTask);
            if (string.IsNullOrEmpty(response))
                return null;

            var tokenResult = JsonConvert.DeserializeObject<TokenResult>(response);
            return tokenResult?.accessToken;
        }


        public Notification BuildNotificationFromQueueMessage(KGACSahelOutSyncQueue notification)
        { 
            var notificationResponse = new Notification
            {
                bodyEn = notification.MsgBodyAr,
                bodyAr = notification.MsgBodyEn,
                isForSubscriber = "true",
                notificationType = notification.NotificationId.ToString(),
                subscriberCivilId = notification.CivilId
            };

            try
            {
                notificationResponse.dataTableEn = JsonConvert.DeserializeObject<Dictionary<string, string>>(notification.MsgTableEn);
                notificationResponse.dataTableAr = JsonConvert.DeserializeObject<Dictionary<string, string>>(notification.MsgTableAr);
            }
            catch (JsonException)
            {
                notificationResponse.dataTableEn = new Dictionary<string, string> { { "Header", notification.MsgTableEn } };
                notificationResponse.dataTableAr = new Dictionary<string, string> { { "عنوان", notification.MsgTableAr } };
            }

            return notificationResponse;
        }
    }
}
