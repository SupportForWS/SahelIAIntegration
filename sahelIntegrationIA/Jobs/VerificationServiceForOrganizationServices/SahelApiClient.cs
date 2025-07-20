using eServicesV2.Kernel.Core.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;

namespace sahelIntegrationIA.Jobs.VerificationServiceForOrganizationServices
{
    public interface ISahelApiClient
    {
        Task<Notification> CallAsync<T>(T dto, string url);
    }

    public class SahelApiClient : ISahelApiClient
    {
        private readonly IRequestLogger _log;

        public SahelApiClient(IRequestLogger log)
        {
            _log = log;
        }

        public async Task<Notification> CallAsync<T>(T dto, string url)
        {
            _log.LogInformation("Initiating API call to Sahel endpoint: {Url}", url);

            try
            {
                using var client = new HttpClient();
                var jsonPayload = JsonConvert.SerializeObject(dto);

                _log.LogInformation("Serialized Request Payload for {Url}: {Payload}", url, jsonPayload);

                var response = await client.PostAsync(url, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
                var responseText = await response.Content.ReadAsStringAsync();

                _log.LogInformation("API Response from {Url}: {Response}", url, responseText);

                var notification = JsonConvert.DeserializeObject<Notification>(responseText);
                return notification;
            }
            catch (Exception ex)
            {
                _log.LogException(
                    ex,
                    "Sahel API call failed. Endpoint: {Url}, Error Message: {Message}, Stack Trace: {StackTrace}",
                    url,
                    ex.Message,
                    ex.StackTrace
                );
                throw;
            }
        }
    }

}
