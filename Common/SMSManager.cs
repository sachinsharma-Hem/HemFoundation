using System;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace HemFoundation.Common
{
    public class SMSManager
    {
        private readonly IConfiguration _configuration;
        private static string WHatsapp_ApiKey;
        private static string templeteid;

        public SMSManager(IConfiguration configuration)
        {
            _configuration = configuration;

        }

        public string SendSMS(string mobile, string message)
        {
            string smsApiKey = _configuration["AppSettings:SMS_ApiKey"] ?? "";
            string smsSender = _configuration["AppSettings:SMS_Sender"] ?? "";
            string templateId = _configuration["AppSettings:template_id"] ?? "";
            string entityId = _configuration["AppSettings:entity_id"] ?? "";

            string result = string.Empty;
            try
            {
                string url = $"https://api-alerts.kaleyra.com/v4/?api_key={smsApiKey}&method=sms&message={message}&to={mobile}&sender={smsSender}&entity_id={entityId}&template_id={templateId}";

                using (var client = new WebClient())
                {
                    result = client.DownloadString(url);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SMS sending failed: " + ex.ToString());
            }
            return result;
        }

        public void StudentRegistration(string mobileNumber, string firstName, string userName, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(userName))
                {
                    userName = mobileNumber;
                }
                if (string.IsNullOrEmpty(password))
                {
                    password = mobileNumber;
                }

                string sms2 = WebUtility.UrlEncode("Namaste " + firstName + "," + Environment.NewLine);
                sms2 += WebUtility.UrlEncode("Welcome You for registration on Hemvirtues.com - A Learning Revolution. Learn value based courses through various Activities with rewards and recognition." + Environment.NewLine);
                sms2 += WebUtility.UrlEncode("Your Login Details" + Environment.NewLine);
                sms2 += WebUtility.UrlEncode("UserName : " + userName + Environment.NewLine);
                sms2 += WebUtility.UrlEncode("Password : " + password + Environment.NewLine);
                sms2 += WebUtility.UrlEncode("Click Now - www.hemvirtues.com" + Environment.NewLine);
                sms2 += WebUtility.UrlEncode("Call/ W'App - 7228001342  HEMA Foundation");

                SendSMS(mobileNumber, sms2);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("StudentRegistration SMS failed: " + ex.ToString());
            }
        }

        public  string WhatsAppsendSMSForOTP(string mobile, string otp)
        {
            string result = string.Empty;

            try
            {

                // Safe configuration reading without trailing space issues
                String WHatsapp_ApiKey = _configuration["AppSettings:WhatsApp_ApiKey"] ?? "";
                String templeteid = _configuration["AppSettings:OTPTempleteID"] ?? "";
                if (string.IsNullOrEmpty(templeteid))
                {
                    templeteid = "code_hema_1";
                }

                string url = "http://wa.iconicsolution.co.in/wapp/api/send/bytemplate?apikey=" + WHatsapp_ApiKey + "&templatename=" + templeteid + "&mobile=" + mobile + "&dvariables=" + otp;

                System.Net.WebClient web = new System.Net.WebClient();
                System.IO.StreamReader webReader = new System.IO.StreamReader(web.OpenRead(url));
                result = webReader.ReadToEnd();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WhatsApp OTP Error: " + ex.Message);
            }
            return result;
        }
    }
}