using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DailyConnectSharp
{
    public partial class Form1 : Form
    {
        public static String PrefPath()
        {
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            String exePath = Uri.UnescapeDataString(uri.Path);
            return Path.ChangeExtension(exePath, ".json");

        }

        public Form1()
        {
            InitializeComponent();
        }

        private void msgCallback(String msg)
        {
            textBox1.AppendText(msg + Environment.NewLine);
        }

        public static string GetResponseContent(WebResponse resp)
        {
            if (resp == null) return "";
            using (StreamReader reader = new StreamReader(resp.GetResponseStream())) return reader.ReadToEnd();
        }

        public void FormToPrefs(Prefs pref)
        {
            pref.Username = tbUserName.Text;
            pref.Password = tbPassword.Text;
        }
        public void PrefsToForm(Prefs pref)
        {
            tbUserName.Text = pref.Username;
            tbPassword.Text = pref.Password;
        }

        public String PrettyfyString(String content)
        {
            try
            {
                JObject jsObj = JsonConvert.DeserializeObject<JObject>(content);
                return JsonConvert.SerializeObject(jsObj, Formatting.Indented);
            }
            catch
            {
                return content;
            }
        }

        public bool PerformRequest(CookieContainer cookieContainer, String url, String content, out String respCont)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.CookieContainer = cookieContainer;

            request.Method = "POST";


            // set request body
            request.ContentType = "application/x-www-form-urlencoded";
            using (StreamWriter writer = new StreamWriter(request.GetRequestStream())) writer.Write(content);

            // GetResponse raises an exception on http status code 400
            // We can pull response out of the exception and continue on our way            
            try
            {
                HttpWebResponse resp = request.GetResponse() as HttpWebResponse;
                respCont = GetResponseContent(resp);
                msgCallback($"Content {resp.StatusCode} - {resp.StatusDescription}");
                msgCallback(PrettyfyString(respCont));
                msgCallback("Cookie");

                foreach (Cookie cook in cookieContainer.GetCookies(new Uri(url)))
                {
                    msgCallback($"{cook.Name}={cook.Value}");
                }
            }
            catch (WebException ex)
            {
                throw new Exception(String.Format("Error processing request: {0}, Response: {1}", ex.Message, GetResponseContent(ex.Response)));
            }
            return true;
        }

        private void btGo_Click(object sender, EventArgs e)
        {
            Prefs cp = new Prefs();
            FormToPrefs(cp);
            File.WriteAllText(PrefPath(), JsonConvert.SerializeObject(cp, Formatting.Indented));


            String resStr = "";
            CookieContainer cookieContainer = new CookieContainer();
            PerformRequest(cookieContainer, "https://www.dailyconnect.com/Cmd?cmd=UserAuth", "email=" + cp.Username + "&pass=" + cp.Password, out resStr);
            PerformRequest(cookieContainer, "https://www.dailyconnect.com/CmdW", "cmd=UserInfoW", out resStr);

            UserInfoW userInfo = JsonConvert.DeserializeObject<UserInfoW>(resStr);


            foreach (Child child in userInfo.myKids)
            {
                msgCallback($"{child.Name} - {child.Id}");
                PerformRequest(cookieContainer, "https://www.dailyconnect.com/CmdW", "cmd=KidGetSummary&Kid="+child.Id+"&pdt=171223", out resStr);
            }


        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Prefs pref = (File.Exists(PrefPath())) ? JsonConvert.DeserializeObject<Prefs>(File.ReadAllText(PrefPath())) : new Prefs();
            PrefsToForm(pref);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Prefs cp = new Prefs();
            FormToPrefs(cp);
            File.WriteAllText(PrefPath(), JsonConvert.SerializeObject(cp, Formatting.Indented));
        }
    }

    public class Child
    {
        public String Name { get; set; }
        public String Id { get; set; }
        public String Dummy { get; set; }
    }

    public class UserInfoW
    {
        public Child[] myKids { get; set; }
        public UserInfoW()
        {
            myKids = new Child[0];
        }
    }

    public class Prefs
    {
        public String Username { get; set; }
        public String Password { get; set; }
        public Prefs()
        {
            Username = "";
            Password = "";
        }
    }
}
