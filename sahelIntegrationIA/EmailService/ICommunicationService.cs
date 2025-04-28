namespace sahelIntegrationIA.EmailService
{
    public interface ICommunicationService
    {
        public bool SendEmail(EmailDetails emailDetails);
        public bool PrepareMailRequest(PrepareMailRequest mailRequest);
        public ETradeAPI.SMSBox.SendingSMSResult SendSMS(SmsDetails smdDetails);
    }

}
