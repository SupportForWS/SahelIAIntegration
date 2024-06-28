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
using sahelIntegrationIA.Models;
using eServicesV2.Kernel.Domain.Entities.OrganizationEntities;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServices.APIs.UserApp.OldApplication.Models;
using ExamCandidateInfo = eServicesV2.Kernel.Domain.Entities.BrokerEntities.ExamCandidateInfo;
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
        Dictionary<int, string> _requestedCivilIds = new();
        List<int> _brokerServices = new();



        public SendMcActionNotificationService(IRequestLogger logger,
            IBaseConfiguration configuration, eServicesContext eServicesContext, IRequestLogger requestLogger,
            IDapper dapper, SahelConfigurations sahelConfigurations)
        {
            _logger = logger;
            _configurations = configuration;
            _eServicesContext = eServicesContext;
            _requestLogger = requestLogger;
            _dapper = dapper;
            _sahelConfigurations = sahelConfigurations;


            _brokerServices = new List<int>(){
                (int)ServiceTypesEnum.BrsPrintingCancelCommercialLicense,
                (int)ServiceTypesEnum.BrsPrintingIssueLicense,
                (int)ServiceTypesEnum.BrsPrintingChangeLicenseAddress,
                (int)ServiceTypesEnum.BrsPrintingChangeLicenseActivity,
                (int)ServiceTypesEnum.BrsPrintingReleaseBankGuarantee,
                (int)ServiceTypesEnum.BrsPrintingGoodBehave,
                (int)ServiceTypesEnum.BrsPrintingRenewLicense,
                (int)ServiceTypesEnum.BrsPrintingChangeJobTitleRenewResidency,
                (int)ServiceTypesEnum.BrsPrintingChangeJobTitleTransferResidency,
                (int)ServiceTypesEnum.BrsPrintingRenewResidency,
                (int)ServiceTypesEnum.BrsPrintingTransferResidency,
                (int)ServiceTypesEnum.BrsPrintingChangeJobTitle,
                (int)ServiceTypesEnum.BrsPrintingChangeJobTitleCivil,
                (int)ServiceTypesEnum.BrsPrintingDeActivateLicenseDeath,

                (int)ServiceTypesEnum.ExamService,
                 (int)ServiceTypesEnum.DeActivateService,
                 (int)ServiceTypesEnum.RenewalService,
                 (int)ServiceTypesEnum.IssuanceService,
                 (int)ServiceTypesEnum.BrsPrintingCancelLicense,
                 (int)ServiceTypesEnum.WhomItConcernsLetterService,
                 (int)ServiceTypesEnum.PrintLostIdCard,
                 (int)ServiceTypesEnum.TransferService,

               };

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
                "OrganizationRequestedForAdditionalInfoState",
                "OrganizationRequestApprovedForUpdate",
                "OrganizationRequestApprovedForCreate",


                 nameof(ServiceRequestStatesEnum.EServiceRequestAcceptedState),
                "EServiceRequestIDPrintedState",
                "EServiceRequestCompletedState",


                //add tran state id
                nameof(ServiceRequestStatesEnum.EservTranReqInitRejectedState),
                nameof(ServiceRequestStatesEnum.EservTranReqFinalRejectedState),
                nameof(ServiceRequestStatesEnum.EservTranReqInitAcceptedState),
                "EservTranReqAcceptedState", //For Broker Transfer Requests Has been Approved

               // EservTranReqInitSubmittedState
               //EservTranReqProceedState
              //EservTranReqSubmittedState
            };

            List<int> serviceIds = new()
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
                (int)ServiceTypesEnum.ConsigneeUndertakingRequest,
                (int)ServiceTypesEnum.WhomItConcernsLetterService

                //broker
                
            };


            serviceIds.AddRange(_brokerServices);

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
                                            && (p.ServiceRequestsDetail.MCNotificationSent.HasValue &&
                                            !p.ServiceRequestsDetail.MCNotificationSent.Value))
                               .AsNoTracking()
                               .ToListAsync();

            var organizationRequests = await _eServicesContext
                               .Set<ServiceRequest>()
                               .Include(p => p.OrganizationRequest)
                               .Where(p => statusEnums.Contains(p.OrganizationRequest.StateId)
                                           && p.RequestSource == "Sahel"
                                           && p.ServiceId == (int)ServiceTypesEnum.OrganizationRegistrationService
                                           && (p.OrganizationRequest.ReadyForSahelSubmission == "0")
                                            && (p.OrganizationRequest.MCNotificationSent.HasValue
                                            && !p.OrganizationRequest.MCNotificationSent.Value))
                               .ToListAsync();

            //requests for exam
            var examRequests = await _eServicesContext
                             .Set<ServiceRequest>()
                             .Include(p => p.ServiceRequestsDetail)
                             .Where(p => p.StateId == nameof(ServiceRequestStatesEnum.EServiceRequestAcceptedState)
                                         && p.RequestSource == "Sahel"
                                         && p.ServiceId == (int)ServiceTypesEnum.ExamService
                                         && p.ServiceRequestsDetail.ReadyForSahelSubmission == "0"
                                         && p.ServiceRequestsDetail.MCNotificationSent.HasValue
                                         && p.ServiceRequestsDetail.MCNotificationSent.Value
                                         )
                             .AsNoTracking()
                             .ToListAsync();

            var examRequestsId = examRequests
                .Select(p => p.EserviceRequestId)
                .ToList();

            var filteredExamRequestsId = await _eServicesContext.Set<ExamCandidateInfo>()
                    .Where(x => examRequestsId.Contains(x.EServiceRequestId.Value)
                                && (x.StateId == "ExamCandidateInfoExamSentState"
                                || x.StateId == "ExamCandidateInfoApprovedState"))
                    .Select(x => new ExamCandidateInfo
                    {
                        StateId = x.StateId,
                        ExamResult = x.ExamResult,
                        EServiceRequestId = x.EServiceRequestId.Value
                    })
                    .ToListAsync();

            examRequests = examRequests
                .Where(x => IsMatchingStateAndNotification(x, filteredExamRequestsId))
                .ToList();


            requestList.AddRange(examRequests);
            requestList.AddRange(organizationRequests);

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

            //todo check
            var requestedIds = serviceRequests
                .Select(a => a.RequesterUserId)
                .ToList();

            //var brokerRequest = serviceRequests
            //    .Where(a => _brokerServices.Contains((int)a.ServiceId))
            //    .ToList();

            _requestedCivilIds = await _eServicesContext
                     .Set<eServicesV2.Kernel.Domain.Entities.IdentityEntities.User>()
                     .Where(p => requestedIds.Contains(p.UserId))
                     .Select(a => new { a.UserId, a.CivilId })
                     .ToDictionaryAsync(a => a.UserId, a => a.CivilId);

            //if (brokerRequest.Any())
            //{
            //    var tmpRequestedCivilIds = brokerRequest
            //                .Select(a => new { a.ServiceRequestsDetail.EserviceRequestDetailsId, a.ServiceRequestsDetail.CivilId })
            //                .ToDictionary(a => (int)a.EserviceRequestDetailsId, a => a.CivilId);

            //    foreach (var kvp in tmpRequestedCivilIds)
            //    {
            //        _requestedCivilIds[kvp.Key] = kvp.Value; // This will update the value if the key already exists
            //    }

            //}





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
                propertyValues: new object[] { reqJson, _jobCycleId });

            string civilID = string.Empty;
            //if (_brokerServices.Contains((int)serviceRequest.ServiceId))
            //{
            //    //for broker we need civil id in request details for each broker
            //    //not the account civil id
            //    civilID = _requestedCivilIds.First(a => a.Key == serviceRequest.ServiceRequestsDetail.EserviceRequestDetailsId).Value;
            //}
            //else
            //{
            //    civilID = _requestedCivilIds.First(a => a.Key == serviceRequest.RequesterUserId).Value;
            //}

            civilID = _requestedCivilIds.First(a => a.Key == serviceRequest.RequesterUserId).Value;


            _logger.LogInformation(message: "{2} - ShaleNotificationMC - Get organization civil Id - {0} - {1}",
                propertyValues: new object[] { serviceRequest.EserviceRequestNumber, civilID, _jobCycleId });

            Notification notficationResponse = new Notification();
            string msgAr = string.Empty;
            string msgEn = string.Empty;
            string stateId = string.Empty;
            if (serviceRequest.ServiceId != (int)ServiceTypesEnum.OrganizationRegistrationService)
            {
                stateId = serviceRequest.StateId;

            }
            else
            {
                stateId = serviceRequest.OrganizationRequest.StateId;
            }


            switch (stateId)
            {
                case nameof(ServiceRequestStatesEnum.EServiceRequestORGForVisitState):
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.VisiNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.VisiNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;

                case nameof(ServiceRequestStatesEnum.EServiceRequestORGForAdditionalInfo):
                case "OrganizationRequestedForAdditionalInfoState":
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.AdditionalInfoNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.AdditionalInfoNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;

                case nameof(ServiceRequestStatesEnum.EServiceRequestORGRejectedState):
                case nameof(ServiceRequestStatesEnum.EServiceRequestRejectedState):
                case "OrganizationRequestRejectedState":
                case "EServiceRequestRejectState":
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.RejectNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.RejectNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;

                case nameof(ServiceRequestStatesEnum.EServiceRequestFinalRejectedState):
                case nameof(ServiceRequestStatesEnum.EservTranReqFinalRejectedState):
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.FinalRejectNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.FinalRejectNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;

                case nameof(ServiceRequestStatesEnum.EServiceRequestORGApprovedState):
                case "EServiceRequestApprovedState":
                case "OrganizationRequestApprovedForUpdate":
                case "OrganizationRequestApprovedForCreate":
                case "EservTranReqAcceptedState":
                case "EServiceRequestAcceptedState":
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.ApproveNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.ApproveNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;

                case "EServiceRequestIDPrintedState":
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.IdPrintedNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.IdPrintedNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;

                case "EServiceRequestCompletedState":
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.CompletedNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.CompletedNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;

                case nameof(ServiceRequestStatesEnum.EservTranReqInitAcceptedState):
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.InitAcceptedNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.InitAcceptedNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;

                case nameof(ServiceRequestStatesEnum.EservTranReqInitRejectedState):
                    msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.InitRejectedNotificationAr, serviceRequest.EserviceRequestNumber);
                    msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.InitRejectedNotificationEn, serviceRequest.EserviceRequestNumber);
                    break;


            }
            List<actionButtonRequestList> actionButtons = null;


            if (serviceRequest.ServiceId == (int)ServiceTypesEnum.WhomItConcernsLetterService
                && stateId == "EServiceRequestCompletedState")
            {
                var requestNumber = CommonFunctions.CsUploadEncrypt(serviceRequest.EserviceRequestNumber.ToString());

                msgAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.CompletedNotificationToWhomAr, serviceRequest.EserviceRequestNumber);
                msgEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.CompletedNotificationToWhomEn, serviceRequest.EserviceRequestNumber);
                var redirectUrl = string.Format(_sahelConfigurations.ToWhomPrintableFormRedirectUrl, requestNumber);
                actionButtons = new List<actionButtonRequestList>
                                    {
                                        new actionButtonRequestList
                                        {
                                            actionType = "download",
                                            LabelAr = "تحميل الشهادة",
                                            LabelEn = "Download Letter",
                                            actionUrl = redirectUrl
                                        }
                                    };

            }


            string examStateId = string.Empty;
            int examNotificationValue = 0;
            if (serviceRequest.ServiceId == (int)ServiceTypesEnum.ExamService
                && stateId == nameof(ServiceRequestStatesEnum.EServiceRequestAcceptedState))
            {
                var examInfo = await _eServicesContext.Set<ExamCandidateInfo>()
                   .Where(x => x.EServiceRequestId == serviceRequest.EserviceRequestId)
                   .Select(x => new ExamCandidateInfo
                   {
                       StateId = x.StateId,
                       ExamResult = x.ExamResult
                   })
                   .FirstOrDefaultAsync();

                if (examInfo != null)
                {
                    examStateId = examInfo.StateId;

                    if (examStateId == "ExamCandidateInfoExamSentState")
                    {
                        var result = HandleExamAttendNotification(serviceRequest.EserviceRequestNumber);
                        examNotificationValue = result.NotificationExamValue;
                        msgAr = result.MessageAr;
                        msgEn = result.MessageEn;
                        actionButtons = result.ActionButtons;
                    }
                    else if (examStateId == "ExamCandidateInfoApprovedState")
                    {
                        var result = HandleExamApprovedNotification(examInfo, serviceRequest);
                        examNotificationValue = result.NotificationExamValue;
                        msgAr = result.MessageAr;
                        msgEn = result.MessageEn;
                    }
                }

            }




            var notificationType = GetNotificationType((ServiceTypesEnum)serviceRequest.ServiceId);
            notficationResponse.bodyEn = msgEn;
            notficationResponse.bodyAr = msgAr;
            notficationResponse.isForSubscriber = "true";
            notficationResponse.dataTableEn = null;
            notficationResponse.dataTableAr = null;
            notficationResponse.actionButtonRequestList = actionButtons;
            notficationResponse.subscriberCivilId = civilID;
            notficationResponse.notificationType = ((int)notificationType).ToString();

            string log = Newtonsoft.Json.JsonConvert.SerializeObject(notficationResponse, Newtonsoft.Json.Formatting.None,
                new JsonSerializerSettings()
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            _logger.LogInformation(message: "{2} - ShaleNotificationMC - Preparing notification {0} - {1}",
                propertyValues: new object[] { serviceRequest.EserviceRequestNumber, log, _jobCycleId });

            var sendNotificationResult = PostNotification(notficationResponse,
                serviceRequest.EserviceRequestNumber,
                "Business");

            await InsertNotification(notficationResponse, sendNotificationResult);

            if (sendNotificationResult)
            {
                _logger.LogInformation(message: " {1} - ShaleNotificationMC - notification sent successfully: {0}",
                         serviceRequest.EserviceRequestNumber, _jobCycleId);

                if (serviceRequest.ServiceId != (int)ServiceTypesEnum.OrganizationRegistrationService)
                {
                    if (examStateId == "ExamCandidateInfoApprovedState" || examStateId == "ExamCandidateInfoExamSentState")
                    {
                        await _eServicesContext
                             .Set<ServiceRequestsDetail>()
                             .Where(a => a.EserviceRequestId == serviceRequest.EserviceRequestId)
                             .ExecuteUpdateAsync(a => a.SetProperty(b => b.ExamNotification, examNotificationValue));
                    }
                    else
                    {
                        await _eServicesContext
                               .Set<ServiceRequestsDetail>()
                               .Where(a => a.EserviceRequestId == serviceRequest.EserviceRequestId)
                               .ExecuteUpdateAsync<ServiceRequestsDetail>(a => a.SetProperty(b => b.MCNotificationSent, true));
                    }

                }
                else
                {
                    await _eServicesContext
                                   .Set<OrganizationRequests>()
                                   .Where(a => a.RequestNumber == serviceRequest.EserviceRequestNumber)
                                   .ExecuteUpdateAsync<OrganizationRequests>(a => a.SetProperty(b => b.MCNotificationSent, true));
                }

            }
            else
            {
                _logger.LogInformation(message: "{1} - ShaleNotificationMC - notification sent faild: {0}",
                        serviceRequest.EserviceRequestNumber, _jobCycleId);
            }
            _logger.LogInformation(message: "{1} - ShaleNotificationMC - end notification: {0}",
                   serviceRequest.EserviceRequestNumber, _jobCycleId);

        }


        #region Private Methods
        public SahelNotficationTypesEnum GetNotificationType(ServiceTypesEnum service)
        {
            return service switch
            {
                ServiceTypesEnum.AddNewAuthorizedSignatoryRequest => SahelNotficationTypesEnum.AddNewAuthorizedSignatory,
                ServiceTypesEnum.RemoveAuthorizedSignatoryRequest => SahelNotficationTypesEnum.RemoveAuthorizedSignatory,
                ServiceTypesEnum.RenewAuthorizedSignatoryRequest => SahelNotficationTypesEnum.RenewAuthorizedSignatory,
                ServiceTypesEnum.ImportLicenseRenewalRequest => SahelNotficationTypesEnum.RenewImportLicense,
                ServiceTypesEnum.NewImportLicenseRequest => SahelNotficationTypesEnum.AddNewImportLicense,
                ServiceTypesEnum.ChangeCommercialAddressRequest => SahelNotficationTypesEnum.ChangeCommercialAddress,
                ServiceTypesEnum.IndustrialLicenseRenewalRequest => SahelNotficationTypesEnum.RenewIndustrialLicense,
                ServiceTypesEnum.CommercialLicenseRenewalRequest => SahelNotficationTypesEnum.RenewCommercialLicense,
                ServiceTypesEnum.OrgNameChangeReqServiceId => SahelNotficationTypesEnum.OrganizationNameChange,
                ServiceTypesEnum.ConsigneeUndertakingRequest => SahelNotficationTypesEnum.UnderTakingConsigneeRequest,
                ServiceTypesEnum.OrganizationRegistrationService => SahelNotficationTypesEnum.OrganizationRegistrationService,

                //broker
                ServiceTypesEnum.BrsPrintingCancelCommercialLicense =>
           SahelNotficationTypesEnum.BrsPrintingCancelCommercialLicense,

                ServiceTypesEnum.BrsPrintingIssueLicense =>
                SahelNotficationTypesEnum.BrsPrintingIssueLicense,

                ServiceTypesEnum.BrsPrintingChangeLicenseAddress =>
                SahelNotficationTypesEnum.BrsPrintingChangeLicenseAddress,

                ServiceTypesEnum.BrsPrintingChangeLicenseActivity =>
                SahelNotficationTypesEnum.BrsPrintingChangeLicenseActivity,

                ServiceTypesEnum.BrsPrintingReleaseBankGuarantee =>
                SahelNotficationTypesEnum.BrsPrintingReleaseBankGuarantee,

                ServiceTypesEnum.BrsPrintingGoodBehave =>
                SahelNotficationTypesEnum.BrsPrintingGoodBehave,

                ServiceTypesEnum.BrsPrintingRenewLicense =>
                SahelNotficationTypesEnum.BrsPrintingRenewLicense,

                ServiceTypesEnum.BrsPrintingChangeJobTitleRenewResidency =>
                SahelNotficationTypesEnum.BrsPrintingChangeJobTitleRenewResidency,

                ServiceTypesEnum.BrsPrintingChangeJobTitleTransferResidency =>
                SahelNotficationTypesEnum.BrsPrintingChangeJobTitleTransferResidency,

                ServiceTypesEnum.BrsPrintingRenewResidency =>
                SahelNotficationTypesEnum.BrsPrintingRenewResidency,

                ServiceTypesEnum.BrsPrintingTransferResidency =>
                SahelNotficationTypesEnum.BrsPrintingTransferResidency,

                ServiceTypesEnum.BrsPrintingChangeJobTitle =>
                SahelNotficationTypesEnum.BrsPrintingChangeJobTitle,

                ServiceTypesEnum.BrsPrintingChangeJobTitleCivil =>
                SahelNotficationTypesEnum.BrsPrintingChangeJobTitleCivil,

                ServiceTypesEnum.BrsPrintingDeActivateLicenseDeath =>
                SahelNotficationTypesEnum.BrsPrintingDeActivateLicenseDeath,

                ServiceTypesEnum.ExamService =>
              SahelNotficationTypesEnum.ExamService,

                ServiceTypesEnum.IssuanceService =>
                  SahelNotficationTypesEnum.IssuanceService,


                ServiceTypesEnum.RenewalService =>
                  SahelNotficationTypesEnum.RenewalService,


                ServiceTypesEnum.BrsPrintingCancelLicense =>
                 SahelNotficationTypesEnum.BrsPrintingCancelLicense,


                ServiceTypesEnum.DeActivateService =>
                 SahelNotficationTypesEnum.DeActivateService,


                ServiceTypesEnum.WhomItConcernsLetterService =>
                 SahelNotficationTypesEnum.WhomItConcernsLetterService,


                ServiceTypesEnum.PrintLostIdCard =>
                  SahelNotficationTypesEnum.PrintLostIdCard,

                ServiceTypesEnum.TransferService =>
               SahelNotficationTypesEnum.TransferService,


                _ => SahelNotficationTypesEnum.RenewImportLicense,
            };
        }

        public bool PostNotification(Notification notification, string requestNumber, string SahelOption = "Business")
        {
            if (string.IsNullOrEmpty(notification.bodyAr) && string.IsNullOrEmpty(notification.bodyEn))
            {
                _logger.LogInformation(message: "{1} - ShaleNotificationMC - Can't send notification because the body is empty - {0}",
                    propertyValues: new object[] { requestNumber, _jobCycleId });
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

        private bool IsMatchingStateAndNotification(ServiceRequest request, List<ExamCandidateInfo> filteredExamRequestsId)
        {
            var examInfo = filteredExamRequestsId.Find(z => z.EServiceRequestId == request.EserviceRequestId);
            if (examInfo == null)
                return false;

            bool isExamSentStateWithNoNotification = examInfo.StateId == "ExamCandidateInfoExamSentState"
                && request.ServiceRequestsDetail.ExamNotification != 1;

            bool isApprovedStateWithNoNotificationAndExamPassed = examInfo.StateId == "ExamCandidateInfoApprovedState"
                && request.ServiceRequestsDetail.ExamNotification != 2
                && examInfo.ExamResult == 332294364;

            bool isApprovedStateWithNoNotificationAndExamFailed = examInfo.StateId == "ExamCandidateInfoApprovedState"
                && request.ServiceRequestsDetail.ExamNotification != 3
                && examInfo.ExamResult == 332294365;

            bool isApprovedStateWithNoNotificationAndExamNotAttend = examInfo.StateId == "ExamCandidateInfoApprovedState"
               && request.ServiceRequestsDetail.ExamNotification != 4
               && examInfo.ExamResult == 332294366;

            return isExamSentStateWithNoNotification
                || isApprovedStateWithNoNotificationAndExamFailed
                || isApprovedStateWithNoNotificationAndExamPassed
                || isApprovedStateWithNoNotificationAndExamNotAttend;


            //"ExamPassed": "332294364",
            //"ExamFailed": "332294365",
            //"NotAttend": "332294366",
        }

        private ExamNotificationResult HandleExamApprovedNotification(ExamCandidateInfo examInfo, ServiceRequest serviceRequest)
        {
            var result = new ExamNotificationResult();

            bool isExamPassed = examInfo.StateId == "ExamCandidateInfoApprovedState"
                       && serviceRequest.ServiceRequestsDetail.ExamNotification != 2
                       && examInfo.ExamResult == 332294364;

            bool isExamFailed = examInfo.StateId == "ExamCandidateInfoApprovedState"
                && serviceRequest.ServiceRequestsDetail.ExamNotification != 3
                && examInfo.ExamResult == 332294365;

            bool isExamNotAttend = examInfo.StateId == "ExamCandidateInfoApprovedState"
               && serviceRequest.ServiceRequestsDetail.ExamNotification != 4
               && examInfo.ExamResult == 332294366;

            if (isExamPassed)
            {
                result.NotificationExamValue = 2;
                result.MessageAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.PassExamNotificationAr, serviceRequest.EserviceRequestNumber);
                result.MessageEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.PassExamNotificationEn, serviceRequest.EserviceRequestNumber);
            }
            else if (isExamFailed)
            {
                result.NotificationExamValue = 3;
                result.MessageAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.FailedExamNotificationAr, serviceRequest.EserviceRequestNumber);
                result.MessageEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.FailedExamNotificationEn, serviceRequest.EserviceRequestNumber);
            }
            else if (isExamNotAttend)
            {
                result.NotificationExamValue = 4;
                result.MessageAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.NotAttendExamNotificationAr, serviceRequest.EserviceRequestNumber);
                result.MessageEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.NotAttendExamNotificationEn, serviceRequest.EserviceRequestNumber);
            }


            return result;

        }

        private ExamNotificationResult HandleExamAttendNotification(string eServiceRequestNumber)
        {
            var result = new ExamNotificationResult();
            result.NotificationExamValue = 1;
            var requestNumber = CommonFunctions.CsUploadEncrypt(eServiceRequestNumber);

            result.MessageAr = string.Format(_sahelConfigurations.MCNotificationConfiguration.ConfirmExamAttendanceAr, eServiceRequestNumber);
            result.MessageEn = string.Format(_sahelConfigurations.MCNotificationConfiguration.ConfirmExamAttendanceEn, eServiceRequestNumber);

            var redirectUrl = string.Format(_sahelConfigurations.ExamAttendanceRedirectUrl, requestNumber);
            var actionButtons = new List<actionButtonRequestList>
                                    {
                                        new actionButtonRequestList
                                        {
                                            actionType = "details",
                                            LabelAr = "تأكيد حضور الاختبار",
                                            LabelEn = "Confirm Exam Attandance",
                                            actionUrl = redirectUrl
                                        }
                                    };
            result.ActionButtons = actionButtons;
            return result;

        }

        #endregion
    }

    class ExamNotificationResult
    {
        public string MessageAr { get; set; }
        public string MessageEn { get; set; }
        public int NotificationExamValue { get; set; }
        public List<actionButtonRequestList> ActionButtons { get; set; }
    }
}
