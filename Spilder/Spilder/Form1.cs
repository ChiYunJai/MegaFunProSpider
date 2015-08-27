using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Threading;

namespace Spilder
{
    public partial class Form1 : Form
    {
        #region 一些參數
        public static bool stopFlag = false;
        public static string indexURL, loginURL, listContentURL, thanksURL;
        //FormHashCode
        public static string formhash1, formhash2;
        //referer
        public static string referer, loginsubmit, tid, post_safecode, indexContent;

        string re_url;
        //別學區網址
        string dofuuURL;
        //帳號、密碼
        string account, password, folder, dirPath;

        private void btnStart_Click(object sender, EventArgs e)
        {
            richTextBox1.AppendText("開始爬蟲\n");
            richTextBox1.AppendText(Spilder());
        }

        int page;
        WebClient url;
        #endregion

        public Form1()
        {
            InitializeComponent();
        }

        string Spilder()
        {
            //初始化參數
            init();

            //建立資料夾
            createFolder();
            
            //建立Index頁
            FileStream indexfl = new FileStream(dirPath + "index.html", FileMode.Create);
            indexfl.Close();

            #region 登入
            //先取得HASH
            formhash1 = getFormHash(url, loginURL);

            CookieContainer cookieContainer = new CookieContainer();
            //登入頁面
            string URI = loginURL;
            HttpWebRequest request = WebRequest.Create(URI) as HttpWebRequest;
            request.Method = "GET";
            request.KeepAlive = false;

            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            System.IO.Stream responseStream = response.GetResponseStream();
            System.IO.StreamReader reader = new System.IO.StreamReader(responseStream, Encoding.UTF8);
            string srcString = reader.ReadToEnd();

            //設定登入的參數
            string para1 = System.Web.HttpUtility.UrlEncode(formhash1);
            string para2 = System.Web.HttpUtility.UrlEncode(referer);
            string para3 = System.Web.HttpUtility.UrlEncode("username");
            string para4 = System.Web.HttpUtility.UrlEncode(account);
            string para5 = System.Web.HttpUtility.UrlEncode(password);
            string para6 = System.Web.HttpUtility.UrlEncode("");
            string para7 = System.Web.HttpUtility.UrlEncode("");

            string formatString =
                     "formhash={0}&referer={1}&loginfield={2}&username={3}&password={4}&questionid={5}&answer={6}";
            string postString =
                     string.Format(formatString, para1, para2, para3, para4, para5, para6, para7);

            byte[] postData = Encoding.ASCII.GetBytes(postString);

            string loginU = "http://megafunpro.com/member.php?mod=logging&action=login&loginsubmit=yes&loginhash=" + formhash1 + "&inajax=1";

            request = WebRequest.Create(loginU) as HttpWebRequest;
            request.Method = "POST";
            request.KeepAlive = false;
            request.ContentType = "application/x-www-form-urlencoded";
            request.CookieContainer = cookieContainer;
            request.ContentLength = postData.Length;

            System.IO.Stream outputStream = request.GetRequestStream();
            outputStream.Write(postData, 0, postData.Length);
            outputStream.Close();

            response = request.GetResponse() as HttpWebResponse;
            responseStream = response.GetResponseStream();
            reader = new System.IO.StreamReader(responseStream, Encoding.GetEncoding("utf-8"));
            srcString = reader.ReadToEnd();
            #endregion
            //開始爬蟲
            for (int i = 1; i <= page; i++)
            {
                if (!stopFlag)
                {
                    return "爬蟲結束";
                }
                #region 取得連結
                //取得第i頁
                URI = dofuuURL + "&page=" + i;
                request = WebRequest.Create(URI) as HttpWebRequest;
                request.Method = "GET";
                request.KeepAlive = false;
                request.CookieContainer = cookieContainer;

                response = request.GetResponse() as HttpWebResponse;
                responseStream = response.GetResponseStream();
                reader = new System.IO.StreamReader(responseStream, Encoding.UTF8);
                srcString = reader.ReadToEnd();

                //利用正規表達式取得每篇文章的tid
                string patterm = @"(tid=+\d{6})";
                Regex regex = new Regex(patterm, RegexOptions.IgnoreCase);
                int threadlist = srcString.IndexOf("threadlist");
                srcString = srcString.Substring(threadlist);
                var matches = regex.Matches(srcString).OfType<Match>().Select(m => m.Value).Distinct();

                formhash2 = getFormHash(url, indexURL);
                #endregion

                //根據取得的文章id(tid)，每一篇進去點讚+複製內容
                foreach (var item in matches)
                {
                    if (!stopFlag)
                    {
                        return "爬蟲結束";
                    }
                    tid = item;

                    string hash;
                    string content;

                    #region 取得感謝頁的post_safecode、re_url
                    //取得文章點讚的網址
                    URI = thanksURL + tid;
                    request = WebRequest.Create(URI) as HttpWebRequest;
                    request.Method = "GET";
                    request.KeepAlive = false;
                    request.CookieContainer = cookieContainer;

                    response = request.GetResponse() as HttpWebResponse;
                    responseStream = response.GetResponseStream();
                    reader = new System.IO.StreamReader(responseStream, Encoding.UTF8);
                    srcString = reader.ReadToEnd();

                    //讚過就不用再讚了
                    if (srcString.IndexOf("您已經給他讚了") == -1)
                    {
                        hash = getFormHash(srcString);

                        //某次論壇更新後，點讚新增兩個安全參數
                        int post_safecodeStart = srcString.IndexOf("post_safecode\" value=\"") + 22;
                        int post_safecodeEnd = srcString.IndexOf("\"", post_safecodeStart);
                        post_safecode = srcString.Substring(post_safecodeStart, post_safecodeEnd - post_safecodeStart);

                        int re_urlStart = srcString.IndexOf("re_url\" value=\"") + 15;
                        int re_urlEnd = srcString.IndexOf("\"", re_urlStart);
                        re_url = srcString.Substring(re_urlStart, re_urlEnd - re_urlStart);

                        //設定點讚post需要的參數
                        para1 = System.Web.HttpUtility.UrlEncode(tid.Substring(4));
                        para2 = System.Web.HttpUtility.UrlEncode(hash);
                        para3 = System.Web.HttpUtility.UrlEncode(post_safecode);
                        para4 = System.Web.HttpUtility.UrlEncode(re_url);
                        para5 = System.Web.HttpUtility.UrlEncode("");
                        para6 = System.Web.HttpUtility.UrlEncode("3");
                        para7 = System.Web.HttpUtility.UrlEncode("true");

                        formatString =
                                 "tid={0}&formhash={1}&post_safecode={2}&re_url={3}&saying={4}&num={5}&thanksubmit={6}";
                        postString =
                                 string.Format(formatString, para1, para2, para3, para4, para5, para6, para7);

                        postData = Encoding.ASCII.GetBytes(postString);

                        string thanks = "http://megafunpro.com/plugin.php?id=thanksplugin:thanks&action=thanks&" + tid;

                        request = WebRequest.Create(thanks) as HttpWebRequest;
                        request.Method = "POST";
                        request.KeepAlive = false;
                        request.ContentType = "application/x-www-form-urlencoded";
                        request.CookieContainer = cookieContainer;
                        request.ContentLength = postData.Length;

                        outputStream = request.GetRequestStream();
                        outputStream.Write(postData, 0, postData.Length);
                        outputStream.Close();

                        response = request.GetResponse() as HttpWebResponse;
                        responseStream = response.GetResponseStream();
                        reader = new System.IO.StreamReader(responseStream, Encoding.GetEncoding("utf-8"));
                        srcString = reader.ReadToEnd();
                    }

                    #endregion
                    URI = listContentURL + tid;
                    request = WebRequest.Create(URI) as HttpWebRequest;
                    request.Method = "GET";
                    request.KeepAlive = false;
                    request.CookieContainer = cookieContainer;

                    response = request.GetResponse() as HttpWebResponse;
                    responseStream = response.GetResponseStream();
                    reader = new System.IO.StreamReader(responseStream, Encoding.UTF8);
                    srcString = reader.ReadToEnd();

                    int post_rate = srcString.IndexOf("post_rate_div_");
                    post_rate = srcString.IndexOf("<div", post_rate);
                    int comment_ = srcString.IndexOf("comment_", post_rate);
                    content = srcString.Substring(post_rate, comment_ - post_rate - 9);

                    string patterm2 = @"http:\/\/megafunprodontlearn.com\/.+(\.jpg)";
                    Regex regex2 = new Regex(patterm2, RegexOptions.IgnoreCase);
                    var temps = regex2.Matches(content).OfType<Match>().Select(m => m.Value).Distinct();

                    FileStream fl = new FileStream(dirPath + tid.Substring(4) + ".html", FileMode.Create);
                    fl.Close();
                    using (StreamWriter sw = new StreamWriter(dirPath + tid.Substring(4) + ".html", false, Encoding.GetEncoding("utf-8")))
                    {
                        sw.Write(content);
                    }

                    using (StreamWriter sw = new StreamWriter(dirPath + "index.html", true, Encoding.GetEncoding("utf-8")))
                    {
                        foreach (var items in temps)
                        {
                            string tmp = string.Format(indexContent, "./" + tid.Substring(4), items) + "\r\n";
                            sw.Write(tmp);
                        }

                    }
                    System.Threading.Thread.Sleep(5000);
                }

            }
            return "爬蟲結束";
        }

        void createFolder()
        {
            FolderBrowserDialog path = new FolderBrowserDialog();
            path.ShowDialog();
            dirPath = path.SelectedPath + "\\" + folder + "\\";
            labPath.Text = dirPath;
            Application.DoEvents();
            if (Directory.Exists(dirPath))
            {
            }
            else
            {
                Directory.CreateDirectory(dirPath);
                Console.WriteLine("The directory {0} was created.", dirPath);
            }
        }

        void init()
        {
            stopFlag = true;

            //取得帳號
            account = txtAcc.Text;
            //取得密碼
            password = txtPass.Text;
            //日期為資料夾名稱
            folder = txtDate.Text;
            //要爬的頁數
            page = txtPage.Text.Equals("") ? 0 : Convert.ToInt32(txtPage.Text);
            //登入、首頁、點讚等相關網頁網址
            listContentURL = "http://megafunpro.com/forum.php?mod=viewthread&";
            indexURL = "http://megafunpro.com/forum.php";
            loginURL = "http://megafunpro.com/member.php?mod=logging&action=login&referer=http://megafunpro.com/forum.php";
            referer = "http://megafunpro.com/forum.php";
            dofuuURL = "http://megafunpro.com/forum.php?mod=forumdisplay&fid=421&orderby=dateline&filter=author&orderby=dateline";
            thanksURL = "http://megafunpro.com/plugin.php?id=thanksplugin:thanks&action=thanks&";
            loginsubmit = "true";
            indexContent = "<a target=\"_blank\" href=\"./{0}.html\" title=\"Detail\"><img src=\"{1}\"></a><br /><br />";
            url = new WebClient();
        }

        string getFormHash(WebClient ct, string url)
        {
            string downloadString = ct.DownloadString(url);
            int formhash1 = downloadString.IndexOf("formhash\" value=\"") + 17;
            int formhash2 = downloadString.IndexOf("\"", formhash1);
            return downloadString.Substring(formhash1, formhash2 - formhash1);
        }

        string getFormHash(string url)
        {
            int formhash1 = url.IndexOf("formhash\" value=\"") + 17;
            int formhash2 = url.IndexOf("\"", formhash1);
            return url.Substring(formhash1, formhash2 - formhash1);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            txtDate.Text = DateTime.Now.ToString("MMdd");
            txtPage.Text = "2";
        }
    }
}
