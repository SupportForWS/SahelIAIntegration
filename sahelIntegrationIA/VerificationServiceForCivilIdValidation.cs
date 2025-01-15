using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Core.Persistence;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.OrganizationEntities;
using eServicesV2.Kernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Models;
using System.Net.Http.Json;
using System.Net;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;
using eServicesV2.Kernel.Core.Resources;
using eServicesV2.Kernel.Domain.Entities.LookupEntities;
using eServices.APIs.UserApp.OldApplication;
using Azure.Core;
using eServices.APIs.UserApp.OldApplication.Models;
using static sahelIntegrationIA.VerificationServiceForCivilIdValidation;
using eServicesV2.Kernel.Core.Infrastructure.Localization;
using eServicesV2.Kernel.Core.Enums;
using eServices.Kernel.Core.Extensions;
using System.Data;
using Microsoft.Data.SqlClient;






namespace sahelIntegrationIA
{
    public class VerificationServiceForCivilIdValidation
    {
        private readonly IRequestLogger _logger;
        private TimeSpan period;
        private readonly IBaseConfiguration _configurations;
        private readonly eServicesContext _eServicesContext;
        private readonly IDapper _dapper;
        private readonly SahelConfigurations _sahelConfigurations;
        Dictionary<int, string> requestedCivilIds = new Dictionary<int, string>();


        //todo check


        public VerificationServiceForCivilIdValidation(IRequestLogger logger,
                                                          IBaseConfiguration configuration,
                                                          eServicesContext eServicesContext,
                                                          IDapper dapper,
        SahelConfigurations sahelConfigurations)

        {
            _logger = logger;
            _configurations = configuration;
            _eServicesContext = eServicesContext;
            _dapper = dapper;
            _sahelConfigurations = sahelConfigurations;

        }
        public async Task CreateRequestObjectDTO()
        {
            _logger.LogInformation("Start CivilId Verification Service");
            var serviceRequest = await GetRequestList();
            var requestedIds = serviceRequest.Select(a => a.UserId).ToList();

            requestedCivilIds = await _eServicesContext
                              .Set<eServicesV2.Kernel.Domain.Entities.IdentityEntities.User>()
                              .Where(p => requestedIds.Contains(p.UserId))
                              .Select(a => new { a.UserId, a.CivilId })
                              .ToDictionaryAsync(a => a.UserId, a => a.CivilId);

            var tasks = serviceRequest.Select(async serviceRequest =>
            {
                try
                {
                    await ProcessServiceRequest(serviceRequest);
                }

                catch (Exception ex)
                {
                    _logger.LogException(ex, "CivilId Verification Service Sahel-Windows-Service");
                }

            });

            await Task.WhenAll(tasks);

        }

        public async Task<List<AuthorizedPersonInfoForNewOrg>> GetRequestList()
        {
            _logger.LogInformation("Start fetching data for CivilId verification service");

            var requestList = await _eServicesContext
                               .Set<AuthorizedPersonInfoForNewOrg>()
                               .Where(p => p.TokenId.HasValue
                                           && (p.KMIDStatus.HasValue &&
                                           p.KMIDStatus == false
                                           && p.NotificationSent.HasValue
                                           && p.NotificationSent == false) || !p.NotificationSent.HasValue
                                          )
                                .AsNoTracking()
                                .ToListAsync();

            string log = JsonConvert.SerializeObject(requestList, Formatting.None,
                        new JsonSerializerSettings()
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });

            _logger.LogInformation("CivilId verification service Number of records: {Records}. List of request numbers: {RequestNumbers}",
                                   requestList.Count,
                                   log);

            var kmidCreatedList = requestList
                .Select(a => a.TokenId)
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


            List<string> expiredOrganizationRequestNumbers = new();
            if (expiredKmidRequests.Count > 0)
            {
                var requestedIds = requestList.Select(a => a.UserId).ToList();

                requestedCivilIds = await _eServicesContext
                             .Set<eServicesV2.Kernel.Domain.Entities.IdentityEntities.User>()
                             .Where(p => requestedIds.Contains(p.UserId))
                             .Select(a => new { a.UserId, a.CivilId })
                             .ToDictionaryAsync(a => a.UserId, a => a.CivilId);

                var expiredRequest = requestList
                                    .Where(a => expiredKmidRequests.Contains(Convert.ToInt32(a.TokenId)))
                                    .ToList();
                await SendExpiredKmidNotification(expiredRequest);
            }

            var filteredRequestList = requestList
                .Where(request => !expiredKmidRequests.Contains(request.TokenId.Value))
                .ToList();

            return filteredRequestList;
        }

        private async Task ProcessServiceRequest(AuthorizedPersonInfoForNewOrg serviceRequest)
        {
            if (serviceRequest.TokenId is null or 0)
            {
                _logger.LogException(new NullReferenceException("CivilId Verification Service Invalid token Id"), "CivilId Verification Service Sahel-Windows-Service");
                return;
            }

            var result = await FetchKmidCivilIdData(serviceRequest.TokenId.ToString());

            if (result.HasError)
            {
                await HandleErrorNotification(result.ErrorMessage, serviceRequest);
            }
            else
            {
                await HandleSuccessNotification(result, serviceRequest);
            }
        }


        public async Task SendExpiredKmidNotification(List<AuthorizedPersonInfoForNewOrg> serviceRequest)
        {
            var tasks = serviceRequest.Select(async request =>
            {
                try
                {
                    string msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.SignUpKmidExpiredAr, request.CivilId);
                    string msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.SignUpKmidExpiredEn, request.CivilId);

                    _ = await CreateNotification(msgAr, msgEn, request);
                }

                catch (Exception ex)
                {
                    _logger.LogException(ex, "CivilId verification service Sahel-Windows-Service");
                }

            });

            await Task.WhenAll(tasks);
        }

        private async Task<bool> CreateNotification(string msgAr, string msgEn, AuthorizedPersonInfoForNewOrg serviceRequest)
        {
            _logger.LogInformation("CivilId verification service start Notification creation process for expired kmid");

            Notification notficationResponse = new Notification();
            string civilID = requestedCivilIds.First(a => a.Key == serviceRequest.UserId).Value;


            var notificationType = SahelNotficationTypesEnum.OrganizationRegistrationService;
            notficationResponse.bodyEn = msgAr;
            notficationResponse.bodyAr = msgEn;
            notficationResponse.isForSubscriber = "true";
            notficationResponse.dataTableEn = null;
            notficationResponse.dataTableAr = null;
            notficationResponse.subscriberCivilId = civilID;
            notficationResponse.notificationType = ((int)notificationType).ToString();

            string log = Newtonsoft.Json.JsonConvert.SerializeObject(notficationResponse, Newtonsoft.Json.Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
            _logger.LogInformation(message: "CivilId verification service Preparing KMID expiration notification {0}", propertyValues: log);

            _logger.LogInformation(message: "CivilId verification service start notification: {0}",
                    propertyValues: new object[] { serviceRequest.CivilId });

            bool isSent = PostNotification(notficationResponse, "Business");
            await InsertNotification(notficationResponse, isSent);

            return isSent;
        }

        #region Private Methods
        public bool PostNotification(Notification notification, string SahelOption = "Business")
        {

            if (string.IsNullOrEmpty(notification.bodyAr) && string.IsNullOrEmpty(notification.bodyEn))
            {
                _logger.LogInformation("Notification not sent NotificationBody is empty.");
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


        public async Task<CivilIdCheckResult> FetchKmidCivilIdData(string token)
        {
            CivilIdCheckResult civilIdCheckDetails = new();

            //todo use sahel config
            var verificationTimeout = DateTime.Now.AddSeconds(_sahelConfigurations.OrganizationKMIDCallingTimer);

            if (string.IsNullOrEmpty(token) || token == "0")
            {
                civilIdCheckDetails.HasError = true;
                civilIdCheckDetails.ErrorMessage = ResourcesEnum.SomethingWentWrong;
                return civilIdCheckDetails;
            }

            while (DateTime.Now < verificationTimeout)
            {
                var kmidStatus = await KmidVerificationStatus(token.ToString());

                if (kmidStatus == nameof(KMIDVerificationStatusEnum.KMIDRequestRejected))
                {
                    civilIdCheckDetails.HasError = true;
                    civilIdCheckDetails.ErrorMessage = ResourcesEnum.VerificationRequestRejectedByAuthorizedSignatorySahel;
                    return civilIdCheckDetails;
                }

                if (kmidStatus == nameof(KMIDVerificationStatusEnum.KMIDRequestPassed))
                {
                    int tokenId = Convert.ToInt32(token);

                    var civilIdDetails = await _eServicesContext
                        .Set<KgacpacipersonIdentity>()
                        .Where(a => a.KgacpaciqueueId == tokenId)
                        .FirstOrDefaultAsync();

                    if (civilIdDetails is null)
                    {
                        civilIdCheckDetails.HasError = true;
                        civilIdCheckDetails.ErrorMessage = ResourcesEnum.SomethingWentWrong;
                        return civilIdCheckDetails;
                    }

                    if (civilIdDetails.CardExpiryDate.Value.Date < DateTime.Now.Date)
                    {
                        civilIdCheckDetails.HasError = true;
                        civilIdCheckDetails.ErrorMessage = ResourcesEnum.CivilIdExpiredSahel;
                        return civilIdCheckDetails;
                    }

                    // TODO: Convert SplitName to synchronous method if possible
                    var arabicName = await SplitName(civilIdDetails.FullNameAr);
                    var englishName = await SplitName(civilIdDetails.FullNameEn);

                    civilIdCheckDetails.ArabicFirstName = arabicName.FirstName;
                    civilIdCheckDetails.ArabicSecondName = arabicName.SecondName;
                    civilIdCheckDetails.ArabicThirdName = arabicName.ThirdName;
                    civilIdCheckDetails.ArabicLastName = arabicName.LastName;

                    civilIdCheckDetails.EnglishFirstName = englishName.FirstName;
                    civilIdCheckDetails.EnglishSecondName = englishName.SecondName;
                    civilIdCheckDetails.EnglishThirdName = englishName.ThirdName;
                    civilIdCheckDetails.EnglishLastName = englishName.LastName;

                    civilIdCheckDetails.CivilIdExpiryDate = civilIdDetails.CardExpiryDate.Value;

                    var nationalityId = await _eServicesContext
                        .Set<Location>()
                        .Where(a => a.KMIDNationalityCode == civilIdDetails.NationalityEn)
                        .Select(a => a.LocationId)
                        .FirstOrDefaultAsync();
                    civilIdCheckDetails.Nationality = nationalityId;

                    return civilIdCheckDetails;
                }
            }

            civilIdCheckDetails.HasError = true;
            civilIdCheckDetails.ErrorMessage = ResourcesEnum.KMIDFailureForSahel;
            return civilIdCheckDetails;
        }

        private async Task<Name> SplitName(string Name)
        {
            var parts = Name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var name = new Name
            {
                FirstName = parts.Length > 0 ? parts[0] : string.Empty,
                SecondName = parts.Length > 1 ? parts[1] : string.Empty,
                ThirdName = parts.Length > 2 ? parts[2] : string.Empty,
                LastName = parts.Length > 3 ? parts[3] : string.Empty
            };

            return name;
        }

        private async Task<string> KmidVerificationStatus(string token)
        {
            string kMIDStatus = string.Empty;
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

        private async Task HandleErrorNotification(ResourcesEnum errorMessage, AuthorizedPersonInfoForNewOrg serviceRequest)
        {
            var (msgAr, msgEn) = FormatNotificationMessage(errorMessage, serviceRequest.CivilId);

            bool isSent = await CreateNotification(msgAr, msgEn, serviceRequest);

            if (!isSent)
            {
                // add log for failure
            }
        }


        private (string msgAr, string msgEn) FormatNotificationMessage(ResourcesEnum errorMessage, string civilId)
        {
            var config = _sahelConfigurations.MCNotificationConfiguration;
            string ar = string.Empty;
            string en = string.Empty;
            //todo check can access _localizer
            if (errorMessage == ResourcesEnum.SomethingWentWrong)
            {
                ar = _sahelConfigurations.KMIDVerificationNotification.SomethingErrorAr;
                en = _sahelConfigurations.KMIDVerificationNotification.SomethingErrorEn;
            }
            else if (errorMessage == ResourcesEnum.VerificationRequestRejectedByAuthorizedSignatorySahel)
            {
                ar = _sahelConfigurations.KMIDVerificationNotification.VerificationRequestRejectedByAuthorizedSignatoryAr;
                en = _sahelConfigurations.KMIDVerificationNotification.VerificationRequestRejectedByAuthorizedSignatoryEn;
            }
            else if (errorMessage == ResourcesEnum.CivilIdExpiredSahel)
            {
                ar = _sahelConfigurations.KMIDVerificationNotification.CivilIdExpiredAr; ;
                en = _sahelConfigurations.KMIDVerificationNotification.CivilIdExpiredAr; ;
            }
            ar = string.Format(ar
                                             , civilId);

            en = string.Format(en
                                 , civilId);

            //todo add error message
            return errorMessage switch
            {
                ResourcesEnum.VerificationRequestRejectedByAuthorizedSignatorySahel => ("", ""),

                ResourcesEnum.CivilIdExpiredSahel => ("", ""),

                ResourcesEnum.KMIDFailureForSahel => ("", ""),

                _ => ("", ""),
            };
        }

        private async Task HandleSuccessNotification(CivilIdCheckResult result, AuthorizedPersonInfoForNewOrg serviceRequest)
        {
            string msgAr = string.Format(_sahelConfigurations.KMIDVerificationNotification.KMIDSuccessAr, serviceRequest.CivilId);
            string msgEn = string.Format(_sahelConfigurations.KMIDVerificationNotification.KMIDSuccessEn, serviceRequest.CivilId);

            bool isSent = await CreateNotification(msgAr, msgEn, serviceRequest);

            if (isSent)
            {
                await _eServicesContext
                    .Set<AuthorizedPersonInfoForNewOrg>()
                    .Where(p => p.Id == serviceRequest.Id)
                    .ExecuteUpdateAsync(x => x
                        .SetProperty(p => p.KMIDStatus, true)
                        .SetProperty(p => p.NotificationSent, true)
                        .SetProperty(p => p.AuthorizedPersonNationality, result.Nationality)
                        .SetProperty(p => p.AuthorizedPerson, $"{result.ArabicFirstName} {result.ArabicSecondName}")
                        .SetProperty(p => p.AuthorizedSignatoryCivilIdExpiryDate, result.CivilIdExpiryDate));
            }
            else
            {
                // add log for failure
            }
        }

        #endregion
        public class Name
        {
            public string FirstName { get; set; }
            public string SecondName { get; set; }
            public string ThirdName { get; set; }
            public string LastName { get; set; }
        }
        public class CivilIdCheckResult
        {
            public string ArabicFirstName { get; set; }
            public string ArabicSecondName { get; set; }
            public string ArabicThirdName { get; set; }
            public string ArabicLastName { get; set; }
            public string EnglishFirstName { get; set; }
            public string EnglishSecondName { get; set; }
            public string EnglishThirdName { get; set; }
            public string EnglishLastName { get; set; }
            public int Nationality { get; set; }
            public DateTime CivilIdExpiryDate { get; set; }

            public bool HasError { get; set; }
            public ResourcesEnum ErrorMessage { get; set; }

            public string FullNameAr
            {
                get
                {
                    return $"{ArabicFirstName} {ArabicSecondName} {ArabicThirdName} {ArabicLastName}".Trim();
                }
            }
            public string FullNameEn
            {
                get
                {
                    return $"{EnglishFirstName} {EnglishSecondName} {EnglishThirdName} {EnglishLastName}".Trim();
                }
            }
        }

    }

}