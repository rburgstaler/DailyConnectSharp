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
using System.Threading;
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

        delegate void ThreadProcType();
        delegate void ThreadProcCaller(ThreadProcType AProc);
        private void ThreadProc(ThreadProcType AProc)
        {
            if (InvokeRequired)
            {
                ThreadProcCaller d = new ThreadProcCaller(ThreadProc);
                Invoke(d, new object[] { AProc });
            }
            else
            {
                AProc();
            }

        }


        public void ThreadMsg(String msg)
        {
            ThreadProc(
                delegate ()
                {
                    textBox1.AppendText(msg + Environment.NewLine);
                });
        }

        public Form1()
        {
            InitializeComponent();
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

        public bool PerformRequest(CookieContainer cookieContainer, String url, String content, out String respCont, DoMessage statusCallback)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.CookieContainer = cookieContainer;

            request.Method = "POST";
            if (statusCallback != null) statusCallback($"Url: {url}");

            // set request body
            request.ContentType = "application/x-www-form-urlencoded";
            using (StreamWriter writer = new StreamWriter(request.GetRequestStream())) writer.Write(content);
            if (statusCallback != null) statusCallback($"{content}");

            // GetResponse raises an exception on http status code 400
            // We can pull response out of the exception and continue on our way            
            try
            {
                HttpWebResponse resp = request.GetResponse() as HttpWebResponse;
                respCont = GetResponseContent(resp);
                if (statusCallback != null) statusCallback($"Content {resp.StatusCode} - {resp.StatusDescription}");
                if (statusCallback != null) statusCallback(PrettyfyString(respCont));
                if (statusCallback != null) statusCallback("Cookie");

                foreach (Cookie cook in cookieContainer.GetCookies(new Uri(url)))
                {
                    if(statusCallback != null) statusCallback($"{cook.Name}={cook.Value}");
                }
            }
            catch (WebException ex)
            {
                throw new Exception(String.Format("Error processing request: {0}, Response: {1}", ex.Message, GetResponseContent(ex.Response)));
            }
            return true;
        }

        public void ProcessData(Prefs cp)
        {
            String resStr = "";
            CookieContainer cookieContainer = new CookieContainer();
            PerformRequest(cookieContainer, "https://www.dailyconnect.com/Cmd?cmd=UserAuth", "email=" + cp.Username + "&pass=" + cp.Password, out resStr, null);
            PerformRequest(cookieContainer, "https://www.dailyconnect.com/CmdW", "cmd=UserInfoW", out resStr, null);

            UserInfoW userInfo = JsonConvert.DeserializeObject<UserInfoW>(resStr);


            foreach (UserInfoW_Child child in userInfo.myKids)
            {
                ThreadMsg($"{child.Name} - {child.Id}");
                PerformRequest(cookieContainer, "https://www.dailyconnect.com/CmdW", "cmd=KidGetSummary&Kid=" + child.Id + "&pdt=" + startDate.ToString("yyMMdd"), out resStr, null);
                KidGetSummary ks = JsonConvert.DeserializeObject<KidGetSummary>(resStr);
                //msgCallback(ks.summary.);
            }

            DateTime procDate = startDate;
            while (procDate.Date >= endDate.Date)
            {
                foreach (UserInfoW_Child child in userInfo.myKids)
                {
                    PerformRequest(cookieContainer, "https://www.dailyconnect.com/CmdW", "cmd=StatusList&Kid=" + child.Id + "&pdt=" + procDate.ToString("yyMMdd") + "&fmt=long&past=7", out resStr, null);
                    StatusList sl = JsonConvert.DeserializeObject<StatusList>(resStr);

                    foreach (StatusList_listitem statusItem in sl.list)
                    {
                        if (statusItem.Cat == (int)CatType.DropOff)
                        {
                            ThreadMsg(procDate.ToString("yyyy/MM/dd") + " " + child.Name + " " + statusItem.Utm + " " + statusItem.Txt);
                        }
                        //msgCallback($"{statusItem.Cat} - {statusItem.Utm} - {statusItem.Txt}");
                    }
                }
                procDate = procDate.Subtract(new TimeSpan(1, 0, 0, 0));
            }
        }

        private void btGo_Click(object sender, EventArgs e)
        {
            Prefs cp = new Prefs();
            FormToPrefs(cp);
            File.WriteAllText(PrefPath(), JsonConvert.SerializeObject(cp, Formatting.Indented));
            Thread thd = new Thread(new ThreadStart(
                delegate
                {
                    ProcessData(cp);
                    ThreadProc(
                        delegate ()
                        {
                            btGo.Enabled = true;
                        });
                }));
            btGo.Enabled = false;
            thd.Start();
        }

        DateTime startDate = DateTime.Now;
        //DateTime endDate = new DateTime(2017, 6, 26, 0, 0, 0);
        DateTime endDate = DateTime.Now.Subtract(new TimeSpan(7, 0, 0, 0, 0));

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

    public delegate void DoMessage(String msg);

    public enum CatType
    {
        DropOff = 101,
        PickUp = 102,
        Meal = 200,
        BM = 402,
        WetDiaper = 403,
        StartSleeping = 501,
        StopsSleeping = 502,
        Event = 700,
        Picture = 1000
    }

    public class StatusList_listitem
    {
        public String By { get; set; }
        public int Cat { get; set; }
        public String ccid { get; set; }
        public String d { get; set; }
        public String e { get; set; }
        public String Id { get; set; }
        public String isst { get; set; }
        public String Kid { get; set; }
        public String ms { get; set; }
        public String n { get; set; }
        public String p { get; set; }
        public String Pdt { get; set; }
        public String Photo { get; set; }
        public String s { get; set; }
        public String Txt { get; set; }
        public String uid_in { get; set; }
        public String uid_out { get; set; }
        public String Utm { get; set; }
        public StatusList_listitem()
        {
            Cat = 0;
        }
    }

    public class StatusList
    {
        public StatusList_listitem[] list { get; set; }
        public StatusList()
        {
            list = new StatusList_listitem[0];
        }
    }

    public class KidGetSummary_KidSummary
    {
        public String timeOfLastDiaper { get; set; }
        public String isSleeping { get; set; }
        public String nrOfSleep { get; set; }
        public String nrOfBMDiapers { get; set; }
        public String nrOfWetDiapers { get; set; }
        public String kidId { get; set; }
        public String longuestSleepDuration { get; set; }
        public String lastRoomIn { get; set; }
        public String nrOfDiapers { get; set; }
        public String timeOfLastSleeping { get; set; }
        public String timeOfLastFood { get; set; }
        public String totalSleepDuration { get; set; }
        public String day { get; set; }
    }

    public class KidGetSummary
    {
        public KidGetSummary_KidSummary summary { get; set; }
        public KidGetSummary()
        {
            summary = new KidGetSummary_KidSummary();
        }
    }

    public class UserInfoW_Child
    {
        public String Name { get; set; }
        public String Id { get; set; }
        public String Dummy { get; set; }
    }

    public class UserInfoW
    {
        public UserInfoW_Child[] myKids { get; set; }
        public UserInfoW()
        {
            myKids = new UserInfoW_Child[0];
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
