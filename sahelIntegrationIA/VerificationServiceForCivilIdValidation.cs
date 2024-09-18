using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Core.Persistence;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.OrganizationEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using eServicesV2.Kernel.Infrastructure.Persistence.Dapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;

namespace sahelIntegrationIA
{
    public class VerificationServiceForCivilIdValidation
    {
        private readonly IRequestLogger _logger;
        private TimeSpan period;
        private readonly IBaseConfiguration _configurations;
        private readonly eServicesContext _eServicesContext;
        private readonly IRequestLogger _requestLogger;
        private readonly IDapper _dapper;
        private readonly SahelConfigurations _sahelConfigurations;
        Dictionary<int, string> requestedCivilIds = new Dictionary<int, string>();

        public VerificationServiceForCivilIdValidation(IRequestLogger logger,
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
                    _logger.LogException(ex, "Sahel-Windows-Service");
                }

            });

            await Task.WhenAll(tasks);

        }
        public async Task<List<AuthorizedPersonInfoForNewOrg>> GetRequestList()
        {


            _logger.LogInformation("Start fetching data for CivilId verification service");


            var requestList = await _eServicesContext
                               .Set<AuthorizedPersonInfoForNewOrg>()
                               .Where(p =>  p.TokenId.HasValue
                                           &&p.KMIDStatus.HasValue && 
                                           p.KMIDStatus== false
                                           && p.NotificationSent.HasValue
                                           && p.NotificationSent== false
                                          )
                                .AsNoTracking()
                                .ToListAsync();

   

            string log = Newtonsoft.Json.JsonConvert.SerializeObject(requestList, Newtonsoft.Json.Formatting.None,
                        new JsonSerializerSettings()
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });

            _logger.LogInformation("Number of records: {Records}. List of request numbers: {RequestNumbers}",
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
            List<string> expiredOrganizationRequestNumbers = new List<string>();
            if (expiredKmidRequests.Count > 0)
            {
                var requestedIds = requestList.Select(a => a.UserId).ToList();

                requestedCivilIds = await _eServicesContext
                             .Set<eServicesV2.Kernel.Domain.Entities.IdentityEntities.User>()
                             .Where(p => requestedIds.Contains(p.UserId))
                             .Select(a => new { a.UserId, a.CivilId })
                             .ToDictionaryAsync(a => a.UserId, a => a.CivilId);

                // expiredOrganizationRequestNumbers = organizationRequestsKmid.Where(a=> expiredKmidRequests.Contains(Convert.ToInt32(a.KMIDToken))).Select(a => a.RequestNumber).ToList();
                var expiredRequest = requestList.Where(a => expiredKmidRequests.Contains(Convert.ToInt32(a.TokenId))).ToList();
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
                _logger.LogException(new NullReferenceException($"Invalid token Id {serviceRequest.TokenId}"), "Sahel-Windows-Service");
                return;
            }

        
            await ProcessServiceRequestAndCallAPI(serviceRequest);

        }

        public async Task SendExpiredKmidNotification(List<AuthorizedPersonInfoForNewOrg> serviceRequest)
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

        private async Task CreateNotification(AuthorizedPersonInfoForNewOrg serviceRequest)
        {
            _logger.LogInformation("start Notification creation process for expired kmid");



            Notification notficationResponse = new Notification();
            string msgAr = string.Empty;
            string msgEn = string.Empty;
            _logger.LogInformation("Create Notification message");
            string civilID = requestedCivilIds.First(a => a.Key == serviceRequest.RequesterUserId).Value;

            _logger.LogInformation(message: "Get organization civil Id{0}", propertyValues: new object[] { civilID });

            msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.KmidExpiredAr, serviceRequest.EserviceRequestNumber);
            msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.KmidExpiredEn, serviceRequest.EserviceRequestNumber);

            _logger.LogInformation(message: "Notification Mesaage in arabic : {0} , Notification Message in english {1}",
                propertyValues: new object[] { msgAr, msgEn });

            var notificationType = SahelNotficationTypesEnum.OrganizationRegistrationService;
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
            _logger.LogInformation(message: "Preparing KMID expiration notification {0}", propertyValues: log);

            _logger.LogInformation(message: "start notification: {0}",
                    propertyValues: new object[] { serviceRequest.EserviceRequestNumber });

            bool isSent = PostNotification(notficationResponse, "Business");
            await InsertNotification(notficationResponse, isSent);

            var requestId = serviceRequest.EserviceRequestId;

            await _eServicesContext
                               .Set<ServiceRequestsDetail>()
                               .Where(a => a.EserviceRequestId == requestId)
                               .ExecuteUpdateAsync<ServiceRequestsDetail>(a => a.SetProperty(b => b.MCNotificationSent, true));

            _logger.LogInformation(message: "end notification: {0}",
                    propertyValues: new object[] { serviceRequest.EserviceRequestNumber });

        }
    }
