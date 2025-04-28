using System.Net.Mail;

namespace sahelIntegrationIA.EmailService
{
    public class EmailDetails
    {
        public string Subject { get; set; }
        public string ToMail { get; set; }
        public string Body { get; set; }
        public bool IsBodyHtml { get; set; }
        public AlternateView? alternateView { get; set; }
    }

}
