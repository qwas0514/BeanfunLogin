using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Web;
using Newtonsoft.Json;

namespace BeanfunLogin
{
    public partial class BeanfunClient : WebClient
    {

        private string RegularLogin(string id, string pass, string skey)
        {
            try
            {
                string response = this.DownloadString("https://tw.newlogin.beanfun.com/login/id-pass_form.aspx?skey=" + skey);
                Regex regex = new Regex("id=\"__VIEWSTATE\" value=\"(.*)\" />");
                if (!regex.IsMatch(response))
                    {this.errmsg = "LoginNoViewstate"; return null;}
                string viewstate = regex.Match(response).Groups[1].Value;

                regex = new Regex("id=\"__EVENTVALIDATION\" value=\"(.*)\" />");
                if (!regex.IsMatch(response))
                    { this.errmsg = "LoginNoEventvalidation"; return null; }
                string eventvalidation = regex.Match(response).Groups[1].Value;
                regex = new Regex("id=\"__VIEWSTATEGENERATOR\" value=\"(.*)\" />");
                if (!regex.IsMatch(response))
                { this.errmsg = "LoginNoViewstateGenerator"; return null; }
                string viewstateGenerator = regex.Match(response).Groups[1].Value;

                NameValueCollection payload = new NameValueCollection();
                payload.Add("__EVENTTARGET", "");
                payload.Add("__EVENTARGUMENT", "");
                payload.Add("__VIEWSTATE", viewstate);
                payload.Add("__VIEWSTATEGENERATOR", viewstateGenerator);
                payload.Add("__EVENTVALIDATION", eventvalidation);
                payload.Add("t_AccountID", id);
                payload.Add("t_Password", pass);
                payload.Add("btn_login", "登入");

                response = Encoding.UTF8.GetString(this.UploadValues("https://tw.newlogin.beanfun.com/login/id-pass_form.aspx?skey=" + skey, payload));

                regex = new Regex("akey=(.*)");
                if (!regex.IsMatch(this.ResponseUri.ToString()))
                { this.errmsg = "LoginNoAkey"; return null; }
                string akey = regex.Match(this.ResponseUri.ToString()).Groups[1].Value;

                return akey;
            }
            catch (Exception e)
            {
                this.errmsg = "LoginUnknown\n\n" + e.Message + "\n" + e.StackTrace;
                return null;
            }
        }

        public class QRCodeClass
        {
            public string skey;
            public string value;
            public string viewstate;
            public string eventvalidation;
            public Bitmap bitmap;
            public bool oldAppQRCode;
        }

        public QRCodeClass GetQRCodeValue(string skey, bool oldAppQRCode)
        {
            string response = this.DownloadString("https://tw.newlogin.beanfun.com/login/qr_form.aspx?skey=" + skey );
            Regex regex = new Regex("id=\"__VIEWSTATE\" value=\"(.*)\" />");
            if (!regex.IsMatch(response))
            { this.errmsg = "LoginNoViewstate"; return null; }
            string viewstate = regex.Match(response).Groups[1].Value;

            regex = new Regex("id=\"__EVENTVALIDATION\" value=\"(.*)\" />");
            if (!regex.IsMatch(response))
            { this.errmsg = "LoginNoEventvalidation"; return null; }
            string eventvalidation = regex.Match(response).Groups[1].Value;

            //Thread.Sleep(3000);

            string value;
            string strEncryptData;
            Stream stream;
            if (oldAppQRCode)
            {
                string qrdata = this.DownloadString($"https://tw.newlogin.beanfun.com/generic_handlers/get_qrcodeData.ashx?skey={skey}&startGame=");
                regex = new Regex("\"strEncryptData\": \"(.*)\"}");
                if (!regex.IsMatch(qrdata))
                { this.errmsg = "LoginNoQrcodedata"; return null; }
                value = regex.Match(qrdata).Groups[1].Value;
                strEncryptData = Uri.UnescapeDataString(value);

                stream = this.OpenRead($"http://tw.newlogin.beanfun.com/qrhandler.ashx?u={value}");
            }
            else
            {
                regex = new Regex("\\$\\(\"#theQrCodeImg\"\\).attr\\(\"src\", \"../(.*)\" \\+ obj.strEncryptData\\);");
                if (!regex.IsMatch(response))
                { this.errmsg = "LoginNoHash"; return null; }
                value = regex.Match(response).Groups[1].Value;

                response = this.DownloadString($"https://tw.newlogin.beanfun.com/generic_handlers/get_qrcodeData.ashx?skey={skey}");
                //regex = new Regex("\"strEncryptData\": \"(.*)\"}");
                //if (!regex.IsMatch(response))
                //{ this.errmsg = "LoginIntResultError"; return null; }

                //strEncryptData = regex.Match(response).Groups[1].Value;
                var definitions = new { intResult = "", strResult = "", strEncryptData = "", strEncryptBCDOData = "" };
                var json = JsonConvert.DeserializeAnonymousType(response, definitions);
                strEncryptData = json.strEncryptData;

                stream = this.OpenRead($"https://tw.newlogin.beanfun.com/{value}{strEncryptData}");
            }

            QRCodeClass res = new QRCodeClass();
            res.skey = skey;
            res.viewstate = viewstate;
            res.eventvalidation = eventvalidation;
            res.value = strEncryptData;
            res.bitmap = new Bitmap(stream);
            res.oldAppQRCode = oldAppQRCode;

            return res;
        }

        private string QRCodeLogin(QRCodeClass qrcodeclass)
        {
            try
            {
                string skey = qrcodeclass.skey;
                
                this.Headers.Set("Referer", @"https://tw.newlogin.beanfun.com/login/qr_form.aspx?skey=" + skey);
                this.redirect = false;
                byte[] tmp2 = this.DownloadData("https://tw.newlogin.beanfun.com/login/qr_step2.aspx?skey=" + skey);
                this.redirect = true;
                string response2 = Encoding.UTF8.GetString(tmp2);
                Debug.Write(response2);
                Regex regex2 = new Regex("akey=(.*)&authkey");
                if (!regex2.IsMatch(response2))
                { this.errmsg = "AKeyParseFailed"; return null; }
                string akey = regex2.Match(response2).Groups[1].Value;

                regex2 = new Regex("authkey=(.*)&");
                if (!regex2.IsMatch(response2))
                { this.errmsg = "authkeyParseFailed"; return null; }
                string authkey = regex2.Match(response2).Groups[1].Value;
                Debug.WriteLine(authkey);
                string test = this.DownloadString("https://tw.newlogin.beanfun.com/login/final_step.aspx?akey="+akey+"&authkey="+ authkey+"&bfapp=1");
                return akey;
            }
            catch (Exception e)
            {
                this.errmsg = "LoginUnknown\n\n" + e.Message + "\n" + e.StackTrace;
                return null;
            }
        }

        public int QRCodeCheckLoginStatus(QRCodeClass qrcodeclass)
        {
            try
            {
                string skey = qrcodeclass.skey;
                string result;
                this.Headers.Set("Referer", @"https://tw.newlogin.beanfun.com/login/qr_form.aspx?skey=" + skey);

                NameValueCollection payload = new NameValueCollection();
                payload.Add(qrcodeclass.oldAppQRCode ? "data" : "status", qrcodeclass.value);
                //Debug.WriteLine(qrcodeclass.value);

                string response = Encoding.UTF8.GetString(this.UploadValues(qrcodeclass.oldAppQRCode ? "https://tw.bfapp.beanfun.com/api/Check/CheckLoginStatus" : "https://tw.newlogin.beanfun.com/generic_handlers/CheckLoginStatus.ashx", payload));
                JObject jsonData;
                try { jsonData = JObject.Parse(response); }
                catch { this.errmsg = "LoginJsonParseFailed"; return -1; }

                result = (string)jsonData["ResultMessage"];
                Debug.WriteLine(result);
                if (result == "Failed")
                    return 0;
                else if (result == "Token Expired")
                {
                    //this.errmsg = "登入逾時，請重新取得QRCode";
                    return -2;
                }
                else if (result == "Success")
                    return 1;
                else
                {
                    this.errmsg = response;
                    return -1;
                }
            }
            catch (Exception e)
            {
                this.errmsg = "Network Error on QRCode checking login status\n\n" + e.Message + "\n" + e.StackTrace;
            }

            return -1;
        }

        public string GetSessionkey()
        {
            string response = this.DownloadString("https://tw.beanfun.com/beanfun_block/bflogin/default.aspx?service=999999_T0");
            //this.DownloadString(this.ResponseHeaders["Location"]);
            //this.DownloadString(this.ResponseHeaders["Location"]);
            //response = this.ResponseHeaders["Location"];
            response = this.ResponseUri.ToString();
            if (response == null)
            { this.errmsg = "LoginNoResponse"; return null; }
            Regex regex = new Regex("skey=(.*)&display");
            if (!regex.IsMatch(response))
            { this.errmsg = "LoginNoSkey"; return null; }
            return regex.Match(response).Groups[1].Value;
        }

        public void Login(string id, string pass, int loginMethod, QRCodeClass qrcodeClass = null, string service_code = "610074", string service_region = "T9")
        {
            this.webtoken = null;
            try
            {
                string response = null;
                string skey = null;
                string akey = null;
                if (loginMethod == (int)LoginMethod.QRCode)
                {
                    skey = qrcodeClass.skey;
                }
                else
                {
                    skey = GetSessionkey();
                }

                switch (loginMethod)
                {
                    case (int)LoginMethod.Regular:
                        akey = RegularLogin(id, pass, skey);
                        break;
                    case (int)LoginMethod.QRCode:
                        akey = QRCodeLogin(qrcodeClass);
                        break;
                    default:
                        this.errmsg = "LoginNoMethod";
                        return;
                }
                if (akey == null)
                    return;

                NameValueCollection payload = new NameValueCollection();
                payload.Add("SessionKey", skey);
                payload.Add("AuthKey", akey);
                Debug.WriteLine(skey);
                Debug.WriteLine(akey);
                response = Encoding.UTF8.GetString(this.UploadValues("https://tw.beanfun.com/beanfun_block/bflogin/return.aspx", payload));
                Debug.WriteLine(response);
                response = this.DownloadString("https://tw.beanfun.com/" + this.ResponseHeaders["Location"]);
                Debug.WriteLine(response);
                Debug.WriteLine(this.ResponseHeaders);

                this.webtoken = this.GetCookie("bfWebToken");
                if (this.webtoken == "")
                { this.errmsg = "LoginNoWebtoken"; return; }

                GetAccounts(service_code, service_region, false);
            }
            catch (Exception e)
            {
                if (e is WebException)
                {
                    this.errmsg = "網路連線錯誤，請檢查官方網站連線是否正常。" + e.Message;
                }
                else
                {
                    this.errmsg = "LoginUnknown\n\n" + e.Message + "\n" + e.StackTrace;
                }
                return;
            }
        }

        public void Logout()
        {
            string response = this.DownloadString("https://tw.beanfun.com/generic_handlers/remove_bflogin_session.ashx");
            //response = this.DownloadString("https://tw.newlogin.beanfun.com/logout.aspx?service=999999_T0");
            NameValueCollection payload = new NameValueCollection();
            payload.Add("web_token", "1");
            this.UploadValues("https://tw.newlogin.beanfun.com/generic_handlers/erase_token.ashx", payload);
        }
    }
}
