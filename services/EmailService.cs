using MimeKit;
using System.Net;
using MailKit.Net.Smtp;

namespace meesuanruam_service.services
{
    public class EmailService
    {

        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<bool> sendMail(string toEmail, string subject, string message)
        {
            try{

                var emailMessage = new MimeMessage();
                emailMessage.From.Add(new MailboxAddress(_config["SmtpSettings:SenderName"], _config["SmtpSettings:SenderEmail"]));
                emailMessage.To.Add(new MailboxAddress("", toEmail));
                emailMessage.Subject = subject;
                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = message;
                emailMessage.Body = bodyBuilder.ToMessageBody();
                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(_config["SmtpSettings:Server"], int.Parse(_config["SmtpSettings:Port"]), MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_config["SmtpSettings:Username"], _config["SmtpSettings:Password"]);
                    await client.SendAsync(emailMessage);
                    await client.DisconnectAsync(true);
                }

                //var smtpClient = new SmtpClient(_config["EmailSender:SmtpHost"])
                //{
                //    Port = 587,
                //    Credentials = new NetworkCredential(_config["EmailSender:SmtpCredential:UserName"], _config["EmailSender:SmtpCredential:Password"]),
                //    EnableSsl = true,
                //};

                //var mailMessage = new MailMessage
                //{
                //    From = new MailAddress("meesuanruam.system@gmail.com"),
                //    Subject = "subject",
                //    Body = "<p>Hello</p>",
                //    IsBodyHtml = true,
                //};
                //mailMessage.To.Add("mineonner@gmail.com");
                //smtpClient.Send(mailMessage);

                return true;
            }
            catch (Exception ex)
            {
                var a = ex;
                return false;
            }

 
        }
    }
}
