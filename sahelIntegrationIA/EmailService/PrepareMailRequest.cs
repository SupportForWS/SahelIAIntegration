namespace sahelIntegrationIA.EmailService
{
    public class PrepareMailRequest
    {
        public string Action { get; set; }
        public string ToMail { get; set; }
        public string Status { get; set; }
        public string Name { get; set; }
        public string MailKeyValue { get; set; }
        public string ServiceName { get; set; }
        public string ExceptionDetails { get; set; }
    }

}
