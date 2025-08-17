using eServices.APIs.UserApp.OldApplication;
using eServices.APIs.UserApp.OldApplication.Models;
using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Domain.Entities.BrokerEntities;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.OtherEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using eServicesV2.Kernel.Infrastructure.Logging.Logging.Implementations;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static eServicesV2.Kernel.Core.Configurations.SahelIntegrationModels;
using static eServicesV2.Kernel.Infrastructure.Persistence.Constants.StoredProcedureNames.UpdateMigrationStatus.UpdateMigrationStatusParamerters;

namespace sahelIntegrationIA.EmailService
{

    public class NotificationSenderJob
    {
        private readonly string _jobCycleId = Guid.NewGuid().ToString();
        private readonly IRequestLogger _logger;
        private readonly IBaseConfiguration _configurations;
        private readonly eServicesContext _eServicesContext;
        private readonly SahelConfigurations _sahelConfigurations;

        public NotificationSenderJob(IRequestLogger logger,
                                         IBaseConfiguration configurations,
                                         eServicesContext eServicesContext,
                                         SahelConfigurations sahelConfigurations)
        {
            _logger = logger;
            _configurations = configurations;
            _eServicesContext = eServicesContext;
            _sahelConfigurations = sahelConfigurations;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                await ProcessNotificationQueueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, $"{_jobCycleId} - Notification Sender Service - Notification queue processing failed");
            }
        }

        private async Task ProcessNotificationQueueAsync()
        {
            var now = DateTime.Now;

            var pendingNotifications = await _eServicesContext
                .Set<NotificationDetails>()
                .Where(e => !e.SentAt.HasValue &&
                            e.Attempts <= e.MaxRetries &&
                            e.ScheduledAt <= now)
                .OrderBy(e => e.ScheduledAt)
                .Take(10)
                .AsNoTracking()
                .ToListAsync();

            var tasks = pendingNotifications.Select(ProcessSingleNotificationAsync);
            await Task.WhenAll(tasks);
        }

        private async Task ProcessSingleNotificationAsync(NotificationDetails notification)
        {
            try
            {
                bool isSent;
                if (notification.IsEmail)
                {
                    isSent = await ProcessEmailAsync(notification);
                }
                else if (notification.IsSms)
                {
                    isSent = ProcessSms(notification);
                }
                else
                {
                    isSent = notification.IsNotification && await ProcessPushNotificationAsync(notification);
                }


                if (isSent)
                {
                    await UpdateSuccessfulNotificationAsync(notification);
                }
                else
                {
                    await UpdateFailedNotificationAsync(notification, "Service returned failure");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, $"{_jobCycleId} - Notification Sender Service - Notification processing failed");
                await UpdateFailedNotificationAsync(notification, ex.Message);
            }
        }

        private async Task<bool> ProcessEmailAsync(NotificationDetails notification)
        {
            var emailDetails = PrepareEmail(notification);
            return SendEmail(emailDetails);
        }

        private bool ProcessSms(NotificationDetails notification)
        {
            var smsDetails = new SmsDetails
            {
                SMSMessage = notification.SmsBody,
                MobileNumber = notification.ToPhone
            };
            var result = SendSMS(smsDetails);
            return result.Result;
        }

        private async Task<bool> ProcessPushNotificationAsync(NotificationDetails notification)
        {
            await Task.FromResult(true);
            // Implement push notification logic
            return true;
        }
        private async Task UpdateSuccessfulNotificationAsync(NotificationDetails notification)
        {
            var updateQuery = _eServicesContext.Set<NotificationDetails>()
                .Where(e => e.Id == notification.Id);

            if (notification.IsEmail)
            {
                await updateQuery.ExecuteUpdateAsync(e => e.SetProperty(x => x.HasEmailSent, true));
            }
            else if (notification.IsSms)
            {
                await updateQuery.ExecuteUpdateAsync(e => e.SetProperty(x => x.HasSmsSent, true));
            }
            else if (notification.IsNotification)
            {
                await updateQuery.ExecuteUpdateAsync(e => e.SetProperty(x => x.HasNotificationSent, true));
            }

            // Common property updates
            await updateQuery.ExecuteUpdateAsync(e => e.SetProperty(x => x.SentAt, DateTime.Now)
                                                      .SetProperty(e => e.Attempts, e => e.Attempts + 1));
        }



        private async Task UpdateFailedNotificationAsync(NotificationDetails notification, string error)
        {
            var errorMsg = error.Length > 500 ? error[..500] : error;

            await _eServicesContext.Set<NotificationDetails>()
                .Where(e => e.Id == notification.Id)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(e => e.Attempts, e => e.Attempts + 1)
                    .SetProperty(e => e.ErrorMessage, errorMsg)
                    );
        }

        private EmailDetails PrepareEmail(NotificationDetails notification)
        {
            var emailDetails = new EmailDetails
            {
                ToMail = notification.ToEmail,
                Subject = notification.EmailSubjectTemplate,
                Body = notification.EmailBodyTemplate,
               IsBodyHtml = notification.IsHtmlBody,
                alternateView = null
            };

            string resourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "resources", "staticFiles");
            string imageFilePath = Path.Combine(resourcesPath, "KgacLogo.png");
            var inlineLogo = new LinkedResource(imageFilePath)
            {
                ContentId = "DFFD1A8F-5393-4A67-9531-CBA0854B00D2"
            };

            if (emailDetails.IsBodyHtml && !string.IsNullOrEmpty(imageFilePath))
            {
                var alternateView = AlternateView.CreateAlternateViewFromString(emailDetails.Body, null, "text/html");
                alternateView.LinkedResources.Add(inlineLogo);
                emailDetails.alternateView = alternateView;
            }

            return emailDetails;
        }

        public bool SendEmail(EmailDetails emailDetails)
        {
            if (emailDetails == null) return false;

            try
            {
                using (var smtpClient = new SmtpClient
                {
                    Port = 25,
                    Credentials = new NetworkCredential("RMSnoreply", "P@ssw0rd", "kgachq"),
                    EnableSsl = true,
                    Host = "mailflow.kgac.com.kw",
                    Timeout = 10000,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                })
                {
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress("noreply@Customs.gov.kw"),
                        Subject = emailDetails.Subject,
                        Body = emailDetails.Body,
                        IsBodyHtml = emailDetails.IsBodyHtml
                    };

                    foreach (var recipient in emailDetails.ToMail.Split(',').Select(r => r.Trim()).Where(r => !string.IsNullOrWhiteSpace(r)))
                    {
                        mailMessage.To.Add(recipient);
                    }

                    if (emailDetails.alternateView != null)
                    {
                        mailMessage.AlternateViews.Add(emailDetails.alternateView);
                    }

                    smtpClient.Send(mailMessage);
                }

                return true;
            }
            catch
            {
                // log the exception
                // return false;
                throw;
            }
        }

        public ETradeAPI.SMSBox.SendingSMSResult SendSMS(SmsDetails smdDetails)
        {
            var smsConfig = _configurations.SmsConfiguration;

            var smsRequest = new ETradeAPI.SMSBox.SendingSMSRequest
            {
                User = new ETradeAPI.SMSBox.SoapUser
                {
                    Username = smsConfig.UserName,
                    Password = smsConfig.Password,
                    CustomerId = Convert.ToInt16(smsConfig.CustomerID)
                },
                SenderText = smsConfig.SenderText,
                MessageBody = smdDetails.SMSMessage,
                RecipientNumbers = smdDetails.MobileNumber,
                IsBlink = smsConfig.IsBlink,
                IsFlash = smsConfig.IsFlash
            };

            var smsClient = new ETradeAPI.SMSBox.MessagingSoapClient("MessagingSoap");
            return smsClient.SendSMS(smsRequest);
        }
    }



}
