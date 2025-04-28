using eServices.APIs.UserApp.OldApplication.Models;
using eServices.APIs.UserApp.OldApplication;
using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mail;
using System.Net.Security;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.EmailService
{

    public class CommunicationService
    {
        private readonly IRequestLogger _requestLogger;
        public IBaseConfiguration _configuration { get; }

        public CommunicationService(
            IBaseConfiguration Configurations,
            IRequestLogger requestLogger)
        {
            _configuration = Configurations;
            _requestLogger = requestLogger;
        }

        public bool PrepareMailRequest(PrepareMailRequest mailRequest)
        {
            _requestLogger.LogInformation(
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
                _requestLogger.LogInformation(
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



            if (_configuration.emailConfiguration.EnableSendingMail)
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
                    _requestLogger.LogInformation(
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
                    Port = _configuration.emailConfiguration.Port,
                    Credentials = new NetworkCredential(
                        _configuration.emailConfiguration.UserName,
                        _configuration.emailConfiguration.Password,
                        _configuration.emailConfiguration.DomainName),
                    EnableSsl = true,
                    Host = _configuration.emailConfiguration.ExchangeServer,
                    Timeout = 10000,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                MailMessage mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration.emailConfiguration.FromEmail),
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

            SenderText = _configuration.SmsConfiguration.SenderText;
            UserName = _configuration.SmsConfiguration.UserName;
            Password = _configuration.SmsConfiguration.Password;
            CustomerID = Convert.ToInt16(_configuration.SmsConfiguration.CustomerID);

            IsBlink = _configuration.SmsConfiguration.IsBlink;
            IsFlash = _configuration.SmsConfiguration.IsFlash;

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
