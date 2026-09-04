using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace HemFoundation.Services
{
    public class EmailManager
    {
        private static IConfiguration _configuration;

        // Make these fields STATIC so static methods can access them
        private static string host = "smtp.gmail.com";
        private static string userName = "";
        private static string password = "";
        private static string mailFrom = "admin@hemafoundation.org";
        private static string port = "587";
        private static string enableSsl = "true";

        // Call this once in Program.cs during startup to bind configuration
        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
            host = _configuration?["SmtpServer"] ?? "smtp.gmail.com";
            userName = _configuration?["SMTPUserName"] ?? "";
            password = _configuration?["SMTPPassword"] ?? "";
            port = _configuration?["SMTPPort"] ?? "587";
            enableSsl = _configuration?["SmtpEnableSsl"] ?? "true";
            mailFrom = _configuration?["EmailFrom"] ?? "admin@hemafoundation.org";
        }

        public static void SendEmailAws(string to, string subject, string body, List<IFormFile> attachments, string cc, string bcc)
        {
            try
            {
                using (var mail = new MailMessage())
                {
                    var smtpServer = new SmtpClient(host);
                    mail.From = new MailAddress(mailFrom);
                    mail.To.Add(to);
                    smtpServer.Port = int.Parse(port);
                    smtpServer.Credentials = new NetworkCredential(userName, password);
                    smtpServer.EnableSsl = bool.Parse(enableSsl);

                    if (attachments != null && attachments.Count > 0)
                    {
                        foreach (var attachment in attachments)
                        {
                            if (attachment != null && attachment.Length > 0)
                            {
                                string fileName = System.IO.Path.GetFileName(attachment.FileName);
                                var stream = attachment.OpenReadStream();
                                mail.Attachments.Add(new Attachment(stream, fileName));
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(cc)) { mail.CC.Add(cc); }
                    if (!string.IsNullOrEmpty(bcc)) { mail.Bcc.Add(bcc); }

                    mail.Subject = subject;
                    mail.Body = body;
                    mail.IsBodyHtml = true;

                    smtpServer.Send(mail);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}