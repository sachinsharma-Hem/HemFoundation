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
        public static void StudentEmailOTPVerification(string emailid, string username, string otp)
        {
            try
            {
                string message = CreateOtpVerificationEmailBody(username, otp);
               
                EmailManager.SendEmailAws(emailid, "OTP For Verification on HemVirtue", message,null,null,null);

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        static string CreateOtpVerificationEmailBody(string userName, string otp)
        {
            var htmlBody = $@"<!DOCTYPE html>
            <html>
            <head>
                <meta charset=""utf-8"">
                <title>OTP Verification - HEM Virtues</title>
                <style>
                    body {{
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        background-color: #f4f7fb;
                        margin: 0;
                        padding: 0;
                        -webkit-font-smoothing: antialiased;
                    }}
                    .email-wrapper {{
                        width: 100%;
                        background-color: #f4f7fb;
                        padding: 40px 0;
                    }}
                    .email-content {{
                        max-width: 500px;
                        margin: 0 auto;
                        background-color: #ffffff;
                        border-radius: 12px;
                        overflow: hidden;
                        box-shadow: 0 4px 15px rgba(0, 0, 0, 0.05);
                        border: 1px solid #e2e8f0;
                    }}
                    .email-header {{
                        background: linear-gradient(135deg, #4f46e5 0%, #3b82f6 100%);
                        padding: 25px;
                        text-align: center;
                        color: #ffffff;
                    }}
                    .email-header h2 {{
                        margin: 0;
                        font-size: 22px;
                        font-weight: 700;
                        letter-spacing: 0.5px;
                    }}
                    .email-body {{
                        padding: 30px;
                        color: #334155;
                        font-size: 15px;
                        line-height: 1.6;
                    }}
                    .otp-box {{
                        background-color: #f8fafc;
                        border: 2px dashed #cbd5e1;
                        border-radius: 8px;
                        text-align: center;
                        padding: 15px;
                        margin: 25px 0;
                    }}
                    .otp-code {{
                        font-size: 32px;
                        font-weight: 800;
                        color: #4f46e5;
                        letter-spacing: 6px;
                    }}
                    .email-footer {{
                        background-color: #f8fafc;
                        padding: 20px 30px;
                        text-align: center;
                        font-size: 12px;
                        color: #94a3b8;
                        border-top: 1px solid #f1f5f9;
                    }}
                </style>
            </head>
            <body>
                <div class=""email-wrapper"">
                    <div class=""email-content"">
                        <!-- Header -->
                        <div class=""email-header"">
                            <h2>HEM Virtues</h2>
                        </div>
                        
                        <!-- Body -->
                        <div class=""email-body"">
                            <p style=""margin-top: 0;"">Dear <strong>{userName}</strong>,</p>
                            
                            <p>You have requested to verify your account or perform a secure action on the HEM Virtues portal. Please use the One-Time Password (OTP) below:</p>
                            
                            <!-- Highlighted OTP Box -->
                            <div class=""otp-box"">
                                <div class=""otp-code"">{otp}</div>
                            </div>
                            
                            <p style=""font-size: 13px; color: #64748b;"">This OTP is valid for a limited time. If you did not request this verification, please safely ignore this email.</p>
                            
                            <p style=""margin-bottom: 0; margin-top: 25px;"">Best regards,<br>
                            <strong>HEMA Foundation</strong></p>
                        </div>
                        
                        <!-- Footer -->
                        <div class=""email-footer"">
                            &copy; {DateTime.Now.Year} HEM Virtues. All rights reserved.
                        </div>
                    </div>
                </div>
            </body>
            </html>";

            return htmlBody;
        }
    }
}