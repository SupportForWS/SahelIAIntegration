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
using eServicesV2.Kernel.Domain.Entities.OtherEntities;
using System.Net.Mail;
using eServices.APIs.UserApp.OldApplication.Models;
using eServices.APIs.UserApp.OldApplication;
using eServicesV2.Kernel.Infrastructure.Logging.Logging.Implementations;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace sahelIntegrationIA.EmailService
{
    public class EmailSenderService
    {
        private readonly string _jobCycleId = Guid.NewGuid().ToString();

        private readonly IRequestLogger _logger;
        private readonly IBaseConfiguration _configurations;
        private readonly eServicesContext _eServicesContext;
        private readonly SahelConfigurations _sahelConfigurations;

       


        public EmailSenderService(IRequestLogger logger,
                                   IBaseConfiguration configurations,
                                  eServicesContext eServicesContext,
                                   SahelConfigurations sahelConfigurations
                                    )
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
                var newEmailQueueItem = new EServicesEmailOutSyncQueue
                {
                    Sync = 0,
                    MsgType = "Notification",
                    UserId = "user123",
                    TOEmailAddress = "to@example.com",
                    CCEmailAddress = "cc@example.com",
                    BCCEmailAddress = "bcc@example.com",
                    Subject = "Test Email",
                    Body = "This is a test email body.",
                    BodyFormat = "HTML",
                    DateCreated = DateTime.Now,
                    ScheduledAt = DateTime.Now,
                    AttemptCount = 0,
                    MaxRetries = 3,
                    StatusId = 0, // assuming 0 means 'pending' or similar
                    MailPriority="Normal",
                    ErrorMessage="none",
                    
                };

              //  await _eServicesContext.Set<EServicesEmailOutSyncQueue>().AddAsync(newEmailQueueItem);
           //     await _eServicesContext.SaveChangesAsync();

                await ProcessEmailQueueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, $"{_jobCycleId} - Email Sender Service - Email queue processing failed");
            }

        }

        private async Task ProcessEmailQueueAsync()
        {
            var pendingEmails = await _eServicesContext
               .Set<EServicesEmailOutSyncQueue>()
               .Where(e => e.Sync == 0 &&
               e.AttemptCount < e.MaxRetries &&
               e.ScheduledAt <= DateTime.Now)
               .OrderBy(e => e.ScheduledAt)
               //   .Take(_configurationss.BatchSize) // Add batch size to config
               .Take(10) // Add batch size to config
               .AsNoTracking()   // detach so we can use a fresh context per task
               .ToListAsync();

            var tasks = pendingEmails.Select(async email =>
            {
                await ProcessSingleEmailAsync(email);
            });

            await Task.WhenAll(tasks);
        }

        private async Task ProcessSingleEmailAsync(EServicesEmailOutSyncQueue email)
        {
            try
            {
                var emailDetails = PrepareEmail(email);

                bool isSent = SendEmail(emailDetails);

                if (isSent)
                {
                    await UpdateSuccessfulEmailAsync(email);
                }
                else
                {
                    await UpdateFailedEmailAsync(email, "Email service returned failure");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, $"{_jobCycleId} - Email Sender Service - Email single processing failed");
                await UpdateFailedEmailAsync(email, ex.Message);
            }
        }

        private async Task UpdateSuccessfulEmailAsync(EServicesEmailOutSyncQueue email)
        {
            // _logger.LogInformation( $"{_jobCycleId} - Email Sender Service - Email updated successfully");

            await _eServicesContext.Set<EServicesEmailOutSyncQueue>()
      .Where(e => e.KGACEmailOutSyncQueueId == email.KGACEmailOutSyncQueueId)
      .ExecuteUpdateAsync(updates => updates
          .SetProperty(e => e.Sync, (short)1)
          .SetProperty(e => e.SentAt, DateTime.Now)
          .SetProperty(e => e.StatusId, (byte)EmailStatus.Sent)
          .SetProperty(e => e.LastAttemptAt, DateTime.Now)
      );
        }

        private async Task UpdateFailedEmailAsync(EServicesEmailOutSyncQueue email, string error)
        {
            // _logger.LogInformation($"{_jobCycleId} - Email Sender Service - Email Failed");

            var errorMsg = error.Length > 500 ? error[..500] : error;

            await _eServicesContext.Set<EServicesEmailOutSyncQueue>()
                .Where(e => e.KGACEmailOutSyncQueueId == email.KGACEmailOutSyncQueueId)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(e => e.AttemptCount, e => e.AttemptCount + 1)
                    .SetProperty(e => e.LastAttemptAt, _ => DateTime.Now)
                    .SetProperty(e => e.ErrorMessage, _ => errorMsg)
                    .SetProperty(e => e.StatusId, e =>
                        (byte)((e.AttemptCount + 1) >= email.MaxRetries ? (int)EmailStatus.Failed : e.StatusId))
                );
        }

        private EmailDetails PrepareEmail(EServicesEmailOutSyncQueue email)
        {

            // 2) Create the EmailDetails
            var emailDetails = new EmailDetails
            {
                ToMail = email.TOEmailAddress,
                Subject = email.Subject,
                Body = email.Body,
                IsBodyHtml = email.BodyFormat == nameof(EmailBodyFormat.HTML),
                alternateView = null,
            };

            // Prepare inline image resource
            string resourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "resources", "staticFiles");
            string imageFilePath = Path.Combine(resourcesPath, "KgacLogo.png");
            var inlineLogo = new LinkedResource(imageFilePath)
            {
                ContentId = "DFFD1A8F-5393-4A67-9531-CBA0854B00D2"
            };

            // 3) Only if HTML & image info is present, wire up the inline image
            if (emailDetails.IsBodyHtml
             && !string.IsNullOrEmpty(imageFilePath))
            {
                // Create an alternate view for HTML content with the inline image
                var alternateView = AlternateView.CreateAlternateViewFromString(emailDetails.Body, null, "text/html");
                alternateView.LinkedResources.Add(inlineLogo);
                emailDetails.alternateView = alternateView;
            }

            return emailDetails;
        }

        public bool PrepareMailRequest(PrepareMailRequest mailRequest)
        {
            _logger.LogInformation(
                  message: "prepare-EMAIL: {0}",
                  propertyValues: mailRequest.Action);
            string subject = "Kuwait eCustoms Services Email Verification Code";
            string bodyHtmlFile = "EmailTempt.htm";

            string NotifTypeLog = "";
            if (mailRequest.Action.Contains("UserRegistration"))
            {
                NotifTypeLog = "UserRegistration";
            }
            else if (mailRequest.Action.ToUpper().Contains("RESET"))
            {
                NotifTypeLog = "ResetPassword";
            }
            else if (mailRequest.Action.ToUpper().Contains("RESENDOTP"))
            {
                NotifTypeLog = "ResendOTP";
            }
            else if (mailRequest.Action.Contains("Exception"))
            {
                _logger.LogInformation(
             message: "Is-Exception-Mail: {0}",
             propertyValues: mailRequest.Action.Contains("Exception").ToString());
                NotifTypeLog = "Exception";
            }
            else
            {
                StackFrame caller = new StackTrace().GetFrame(1);
                string methodName = caller.GetMethod().Name;
                NotifTypeLog = "GenerateEmailOTP";
            }
            string UserHostAddress = "";
            LogUserActivity LUA = new LogUserActivity
            {
                ActivityPerformed = NotifTypeLog + " EMAIL",
                IPAddress = UserHostAddress,
                LoginTime = DateTime.Now.ToString(),
                OtherAdditionalInfo = mailRequest.ToMail
            };



            if (_configurations.emailConfiguration.EnableSendingMail)
            {
                try
                {

                    ServicePointManager.ServerCertificateValidationCallback = delegate (object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                    {
                        return true;
                    };


                    //string toMaileAddress = new MailAddress(mailRequest.ToMail, "Kuwait eCustoms Services").ToString();
                    if (mailRequest.Action.Contains("Exception"))
                    {
                        subject = "Exception Occured";
                        bodyHtmlFile = "ExceptionTemplate";
                    }
                    else
                    {
                        if (mailRequest.Status == "Submitted")
                        {
                            subject = "eService Request";
                            bodyHtmlFile = "EServiceRequest.html";
                        }
                        else
                        {
                            if (bodyHtmlFile == "EmailGeneralVerification.html")
                                bodyHtmlFile = "EmailGeneralVerification";
                            else
                                bodyHtmlFile = "EmailTempt";
                        }
                    }
                    string htmlContent = string.Empty;
                    string resourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "resources\\staticFiles");

                    string BodyFile = Path.Combine(resourcesPath, bodyHtmlFile + ".html");
                    FileStream fsreader = new FileStream(BodyFile, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
                    StreamReader reader = new StreamReader(fsreader);

                    string readFile = reader.ReadToEnd();

                    reader.Dispose();
                    fsreader.Dispose();
                    string myString = "";

                    if (mailRequest.Action.Contains("Exception"))
                    {

                        myString = readFile.ToString();

                        myString = myString.Replace("#1", mailRequest.ExceptionDetails);

                    }
                    else
                    {
                        if (mailRequest.Status == "Submitted")
                        {
                            myString = readFile.ToString();
                            myString = myString.Replace("#0", mailRequest.Name);
                            myString = myString.Replace("#1", mailRequest.MailKeyValue);
                            myString = myString.Replace("#2", mailRequest.ServiceName);
                            myString = myString.Replace("#3", mailRequest.Status);
                            myString = myString.Replace("#4", DateTime.Now.ToString());
                        }
                        else
                        {
                            myString = readFile.ToString();
                            myString = myString.Replace("#1", mailRequest.Name);
                            myString = myString.Replace("#2", mailRequest.MailKeyValue);
                            myString = myString.Replace("#3", mailRequest.ToMail);
                        }

                    }
                    string sMailBody = myString.ToString();
                    string mailBody = sMailBody;
                    _logger.LogInformation(
                message: "EMAIL-Body: {0}",
                propertyValues: sMailBody);
                    string imagePath = Path.Combine(Directory.GetCurrentDirectory(), "resources\\staticFiles");
                    string imageFile = Path.Combine(resourcesPath, "KgacLogo.png");
                    var inlineLogo = new LinkedResource(imageFile);
                    inlineLogo.ContentId = "DFFD1A8F-5393-4A67-9531-CBA0854B00D2";

                    var view = AlternateView.CreateAlternateViewFromString(mailBody, null, "text/html");
                    view.LinkedResources.Add(inlineLogo);
                    var alternateView = view;


                    bool isBodyHtml = true;
                    EmailDetails emailDetails = new EmailDetails()
                    {
                        Subject = subject,
                        IsBodyHtml = isBodyHtml,
                        alternateView = alternateView,
                        Body = bodyHtmlFile,
                        ToMail = mailRequest.ToMail,

                    };

                    var isMailSent = SendEmail(emailDetails);
                    return isMailSent;
                    LUA.ActivityPerformed = LUA.ActivityPerformed + " - EMAIL SUCCESS";
                    MobileDataBase.UpdateUserActivityDetailsDS(LUA);


                    CommonFunctions.LogEmailHandlingAsBackup("eServices-" + NotifTypeLog, mailRequest.MailKeyValue, mailRequest.ToMail, "", true, mailRequest.Name, mailRequest.ServiceName);

                }
                catch (Exception ex)
                {
                    CommonFunctions.LogUserActivity("SendEmail api", "", "", "", "", ex.Message.ToString());
                    LUA.ActivityPerformed = LUA.ActivityPerformed + " - EMAIL FAILURE - ";// + //todo: check if needed
                                                                                          //HttpContext.Current.Request.ServerVariables["LOCAL-ADDR"].ToString();
                    LUA.OtherAdditionalInfo = LUA.OtherAdditionalInfo + " - FAILED - " + ex.Message.ToString();
                    MobileDataBase.UpdateUserActivityDetailsDS(LUA);
                    CommonFunctions.LogEmailHandlingAsBackup("eServices-" + NotifTypeLog, mailRequest.MailKeyValue, mailRequest.ToMail, "", false, mailRequest.Name, mailRequest.ServiceName);


                }

            }
            return false;
        }

        public bool SendEmail(EmailDetails emailDetails)
        {
            if (emailDetails == null)
                return false;

            try
            {
                SmtpClient smtpClient = new SmtpClient()
                {
                    Port = 25,
                    Credentials = new NetworkCredential(
                        "RMSnoreply",
                        "P@ssw0rd",
                        "kgachq"),
                    EnableSsl = true,
                    Host = "mailflow.kgac.com.kw",// _configurations.emailConfiguration.ExchangeServer,
                    Timeout = 10000,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                MailMessage mailMessage = new MailMessage
                {
                    From = new MailAddress("noreply@Customs.gov.kw"),
                    Subject = emailDetails.Subject,
                    Body = emailDetails.Body,
                    IsBodyHtml = emailDetails.IsBodyHtml
                };

                foreach (var recipient in emailDetails.ToMail.Split(','))
                {
                    var trimmed = recipient.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                        mailMessage.To.Add(trimmed);
                }

                if (emailDetails.alternateView != null)
                {
                    mailMessage.AlternateViews.Add(emailDetails.alternateView);
                }

                smtpClient.Send(mailMessage);

                return true;
            }
            catch
            {
                // log the exception
                return false;
            }
        }

        public ETradeAPI.SMSBox.SendingSMSResult SendSMS(SmsDetails smdDetails)
        {
            string UserName, Password, SenderText;
            smdDetails.SMSMessage = "Etrade OTP: " + smdDetails.MobileKeyValue;

            int CustomerID;
            bool IsBlink, IsFlash;

            SenderText = _configurations.SmsConfiguration.SenderText;
            UserName = _configurations.SmsConfiguration.UserName;
            Password = _configurations.SmsConfiguration.Password;
            CustomerID = Convert.ToInt16(_configurations.SmsConfiguration.CustomerID);

            IsBlink = _configurations.SmsConfiguration.IsBlink;
            IsFlash = _configurations.SmsConfiguration.IsFlash;

            ETradeAPI.SMSBox.SoapUser UserDet = new ETradeAPI.SMSBox.SoapUser { Username = UserName, Password = Password, CustomerId = CustomerID };
            ETradeAPI.SMSBox.SendingSMSRequest SMSReq = new ETradeAPI.SMSBox.SendingSMSRequest
            {
                User = UserDet,
                SenderText = SenderText,
                MessageBody = smdDetails.SMSMessage,
                RecipientNumbers = smdDetails.MobileNumber,
                IsBlink = IsBlink,
                IsFlash = IsFlash
            };
            ETradeAPI.SMSBox.MessagingSoapClient SMS = new ETradeAPI.SMSBox.MessagingSoapClient("MessagingSoap");
            ETradeAPI.SMSBox.SendingSMSResult result = SMS.SendSMS(SMSReq);
            string[] RejectedMSISDN = result.RejectedNumbers;
            return result;

        }
    }

}
