namespace ReportGenerators
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenQA.Selenium.Chrome;
    using OpenQA.Selenium.Support.UI;
    using System.Text.RegularExpressions;
    using System.IO;
    using OpenQA.Selenium;
    using My.SeleniumExtentions;
    using System.Management.Automation;
    using My.StringExtentions;
    using My.VideoParser;
    using System.Reflection;
    using Newtonsoft.Json;

    public class MediaParser : RParserBase
    {   //Object to parse course pages for media elements (main user of the VideoParser class)
        private string PathToChromedriver;
        private My.PanelOptions Options;
        //Class to do a media report
        public MediaParser()
        {
            string json = "";
            string path = Assembly.GetEntryAssembly().Location.Contains("source") ? @"C:\Users\jwilli48\Desktop\AccessibilityTools\A11yPanel\options.json" :
                                System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + @"\options.json";
            using (StreamReader r = new StreamReader(path))
            {
                json = r.ReadToEnd();
            }
            Options = JsonConvert.DeserializeObject<My.PanelOptions>(json);
            PathToChromedriver = Options.ChromeDriverPath;
            //Need to create the ChromeDriver for this class to find video lengths
            var chromeDriverService = ChromeDriverService.CreateDefaultService(PathToChromedriver);
            chromeDriverService.HideCommandPromptWindow = true;
            var ChromeOptions = new ChromeOptions();
            //ChromeOptions.AddArguments("headless", "muteaudio");
            Chrome = new ChromeDriver(chromeDriverService, ChromeOptions);
            Wait = new WebDriverWait(Chrome, new TimeSpan(0, 0, 5));                        
        }
        ~MediaParser()
        {   //Need to make sure we dispose of the ChromeDriver when this class is disposed
            Chrome.Quit();
        }
        //Gen a media report
        public ChromeDriver Chrome { get; set; }
        public WebDriverWait Wait { get; set; }
        private bool LoggedIntoBrightcove = false;
        public override void ProcessContent(Dictionary<string, string> page_info)
        {   //Main function to begin process of a page
            lock (Chrome)
            {
                lock (Wait)
                {
                    if (!LoggedIntoBrightcove)
                    {   //Need to make sure the ChromeDriver is logged into brightcove
                        //Since I already had a powershell script to do this I just load and run that script
                        string BrightCoveUserName = Options.BrightCoveCred["Username"];
                        var password = Options.BrightCoveCred["Password"];

                        Chrome.Url = "https://signin.brightcove.com/login?redirect=https%3A%2F%2Fstudio.brightcove.com%2Fproducts%2Fvideocloud%2Fmedia";
                        Wait.UntilElementIsVisible(By.CssSelector("input[name*=\"email\"]")).SendKeys(BrightCoveUserName);
                        Wait.UntilElementIsVisible(By.CssSelector("input[id*=\"password\"]")).SendKeys(password);
                        Wait.UntilElementIsVisible(By.CssSelector("button[id*=\"signin\"]")).Submit();

                        LoggedIntoBrightcove = true;
                    }
                }
            }

            if (page_info[page_info.Keys.ElementAt(0)] == null)
            {   //Just return if there is no page / page is empty
                return;
            }
            //Set the current document
            var PageDocument = new DataToParse(page_info.Keys.ElementAt(0), page_info[page_info.Keys.ElementAt(0)]);
            //Process each of the media elements
            ProcessLinks(PageDocument);
            ProcessIframes(PageDocument);
            ProcessVideoTags(PageDocument);
            ProcessBrightcoveVideoHTML(PageDocument);
        }
        private void ProcessLinks(DataToParse PageDocument)
        {
            var link_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//a");
            if (link_list == null)
            {
                return;
            }
            var BrightCoveVideoName = "";
            foreach (var link in link_list)
            {   //Make sure there is an href
                if (link.Attributes["href"] == null)
                {
                    continue;
                }
                if (link.GetClasses().Contains("video_link"))
                {   //See if it is an embded canvas video link
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                    "Canvas Video Link",
                                                    "",
                                                    "Inline Media:\nUnable to find title, CC, or video length for this type of video",
                                                    link.Attributes["href"].Value,
                                                    new TimeSpan(0),
                                                    VideoParser.CheckTranscript(link),
                                                    false));
                    }
                }
                else if(link.GetClasses().Contains("instructure_audio_link"))
                {
                    lock(Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                    "Canvas Audio Link",
                                                    "",
                                                    link.InnerText,
                                                    link.Attributes["href"].Value,
                                                    new TimeSpan(0),
                                                    VideoParser.CheckTranscript(link),
                                                    false));
                    }
                }
                else if (new Regex("youtu\\.?be", RegexOptions.IgnoreCase).IsMatch(link.Attributes["href"].Value))
                {   //See if it is a youtube video
                    var uri = new Uri((link.Attributes["href"].Value));
                    
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var video_id = string.Empty;
                    if (query.AllKeys.Contains("v"))
                    {
                        video_id = query["v"];
                    }
                    else
                    {
                        video_id = uri.Segments.LastOrDefault();
                    }
                    //Get time from video
                    TimeSpan video_length;
                    bool channel = false;
                    string title = "";
                    try
                    {
                        video_id = video_id.Split('?')
                                            .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                            .FirstOrDefault();
                        video_id = video_id.Split('/')
                                            .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                            .LastOrDefault();
                        video_id = video_id.Split('#')
                                            .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                            .FirstOrDefault();
                        if (uri.Segments.Contains("channel/"))
                        {
                            title = VideoParser.GetYTChannelName(video_id);
                            video_length = new TimeSpan(0);
                            channel = true;
                        }
                        else
                        {
                            video_length = VideoParser.GetYoutubeVideoLength(video_id);
                        }
                    }
                    catch
                    {
                        //Time is 0 if it failed
                        Console.WriteLine("Video not found");
                        video_length = new TimeSpan(0);
                    }
                    string video_found = string.Empty;
                    if (video_length == new TimeSpan(0) && !channel)
                    {   //make sure we apend video not found for the excel doc
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                        if(channel)
                        {
                            video_found = $"\nLinks to a channel named: {title}";
                        }
                    }

                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                    "YouTube Link",
                                                    video_id,
                                                    link.InnerText + video_found,
                                                    link.Attributes["href"].Value,
                                                    video_length,
                                                    true,
                                                    true));
                    }
                }
                else if (link.Attributes["href"].Value.Contains("alexanderstreet"))
                {
                    string video_id = link.Attributes["href"].Value.Split('/')
                                                                    .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                                                    .LastOrDefault();
                    TimeSpan video_length;
                    bool cc;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetAlexanderStreenLinkLength(video_id, Chrome, Wait, out cc);
                        }
                    }

                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                    "AlexanderStreet Link",
                                                    video_id,
                                                    link.InnerText + video_found,
                                                    link.Attributes["href"].Value,
                                                    video_length,
                                                    cc,
                                                    cc));
                    }
                }
                else if (link.Attributes["href"].Value.Contains("kanopy"))
                {
                    string video_id = link.Attributes["href"].Value.Split('/')
                                                                    .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                                                    .LastOrDefault();
                    TimeSpan video_length;
                    bool cc;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetKanopyLinkLength(video_id, Chrome, Wait, out cc);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "Kanopy Link",
                                                                        video_id,
                                                                        link.InnerText + video_found,
                                                                        link.Attributes["href"].Value,
                                                                        video_length,
                                                                        cc,
                                                                        cc));
                    }
                }
                else if (link.Attributes["href"].Value.Contains("byu.mediasite"))
                {
                    string video_id = link.Attributes["href"].Value.Split('/')
                                                                    .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                                                    .LastOrDefault();
                    TimeSpan video_length;
                    bool cc;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetBYUMediaSiteVideoLength(video_id, Chrome, Wait, out cc);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "ByuMediasite Link",
                                                                        video_id,
                                                                        link.InnerText + video_found,
                                                                        link.Attributes["href"].Value,
                                                                        video_length,
                                                                        VideoParser.CheckTranscript(link),
                                                                        cc));
                    }
                }
                else if (link.Attributes["href"].Value.Contains("panopto"))
                {
                    string video_id = link.Attributes["href"].Value.Split('/')
                                                                    .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                                                    .LastOrDefault();
                    video_id = video_id.CleanSplit("id=").LastOrDefault().CleanSplit("&").FirstOrDefault();

                    TimeSpan video_length;
                    bool cc;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            if(link.Attributes["href"].Value.Contains("Viewer.aspx"))
                            {
                                video_length = VideoParser.GetPanoptoVideoViewerLength(video_id, Chrome, Wait, out cc);
                            }
                            else
                            {
                                video_length = VideoParser.GetPanoptoVideoLength(video_id, Chrome, Wait, out cc);
                            }
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                    "Panopto Link",
                                                    video_id,
                                                    link.InnerText + video_found,
                                                    link.Attributes["href"].Value,
                                                    video_length,
                                                    VideoParser.CheckTranscript(link),
                                                    cc));
                    }
                }
                else if (link.Attributes["href"].Value.Contains("bcove"))
                {
                    string video_id;
                    TimeSpan video_length;
                    bool cc;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            Chrome.Url = link.Attributes["href"].Value;
                            try
                            {
                                Wait.UntilElementIsVisible(By.CssSelector("iframe"));
                            }catch
                            {

                            }
                            

                            video_id = Chrome.Url.Split('=')
                                                        .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                                        .LastOrDefault();
                            video_length = VideoParser.GetBrightcoveVideoLength(video_id, Chrome, Wait, out cc, out BrightCoveVideoName);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "Bcove Link",
                                                                        video_id,
                                                                        link.InnerText + video_found + BrightCoveVideoName,
                                                                        link.Attributes["href"].Value,
                                                                        video_length,
                                                                        VideoParser.CheckTranscript(link),
                                                                        cc));
                    }
                }
            }
        }
        private void ProcessIframes(DataToParse PageDocument)
        {   //Process all the iframes for media elements
            var iframe_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//iframe");
            if (iframe_list == null)
            {
                return;
            }
            var BrightCoveVideoName = "";
            foreach (var iframe in iframe_list)
            {
                string title = "";

                if (iframe.Attributes["title"] == null)
                {
                    title = "No title attribute found";
                }
                else
                {
                    title = iframe.Attributes["title"].Value;
                }

                if (iframe.Attributes["src"] == null)
                {
                    continue;
                }

                if (iframe.Attributes["src"].Value.Contains("youtube"))
                {
                    var uri = new Uri((iframe.Attributes["src"].Value));
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var video_id = string.Empty;
                    if (query.AllKeys.Contains("v"))
                    {
                        video_id = query["v"];
                    }
                    else
                    {
                        video_id = uri.Segments.LastOrDefault();
                    }

                    TimeSpan video_length;
                    try
                    {
                        video_id = video_id.CleanSplit("?").FirstOrDefault();
                        video_id = video_id.CleanSplit("%").FirstOrDefault();
                        video_length = VideoParser.GetYoutubeVideoLength(video_id);
                    }
                    catch
                    {
                        Console.WriteLine("Video not found");
                        video_length = new TimeSpan(0);
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "YouTube Video",
                                                                        video_id,
                                                                        title + video_found,
                                                                        iframe.Attributes["src"].Value,
                                                                        video_length,
                                                                        true,
                                                                        true));
                    }
                }
                else if (iframe.Attributes["src"].Value.Contains("brightcove"))
                {
                    string video_id = iframe.Attributes["src"].Value.CleanSplit("=").LastOrDefault().CleanSplit("&")[0];
                    TimeSpan video_length;
                    bool cc;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetBrightcoveVideoLength(video_id, Chrome, Wait, out cc, out BrightCoveVideoName);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = iframe.Attributes["src"].Value.Contains("playlistId") ? "\nVideo playlist. Manual check for transcripts needed." : "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                    "Brightcove Video",
                                                    video_id,
                                                    title + video_found + BrightCoveVideoName,
                                                    iframe.Attributes["src"].Value,
                                                    video_length,
                                                    VideoParser.CheckTranscript(iframe),
                                                    cc));
                    }
                }
                else if (iframe.Attributes["src"].Value.Contains("H5P"))
                {
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                "H5P",
                                                                "",
                                                                title,
                                                                iframe.Attributes["src"].Value,
                                                                new TimeSpan(0),
                                                                true,
                                                                true));
                    }
                }
                else if (iframe.Attributes["src"].Value.Contains("byu.mediasite"))
                {
                    string video_id = iframe.Attributes["src"].Value.CleanSplit("/").LastOrDefault();
                    if (String.IsNullOrEmpty(video_id))
                    {
                        video_id = iframe.Attributes["src"].Value.CleanSplit("/").Reverse().Skip(1).FirstOrDefault();
                    }
                    TimeSpan video_length;
                    bool cc;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetBYUMediaSiteVideoLength(video_id, Chrome, Wait, out cc);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "BYU Mediasite Video",
                                                                        video_id,
                                                                        title + video_found,
                                                                        iframe.Attributes["src"].Value,
                                                                        video_length,
                                                                        VideoParser.CheckTranscript(iframe),
                                                                        cc));
                    }
                }
                else if (iframe.Attributes["src"].Value.Contains("panopto"))
                {
                    string video_id = iframe.Attributes["src"].Value.CleanSplit("id=").LastOrDefault().CleanSplit("&").FirstOrDefault();
                    TimeSpan video_length;
                    bool cc;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetPanoptoVideoLength(video_id, Chrome, Wait, out cc);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "Panopto Video",
                                                                        video_id,
                                                                        title + video_found,
                                                                        iframe.Attributes["src"].Value,
                                                                        video_length,
                                                                        VideoParser.CheckTranscript(iframe),
                                                                        cc));
                    }
                }
                else if (iframe.Attributes["src"].Value.Contains("alexanderstreet"))
                {
                    string video_id = iframe.Attributes["src"].Value.CleanSplit("token/").LastOrDefault();
                    TimeSpan video_length;
                    bool cc;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetAlexanderStreetVideoLength(video_id, Chrome, Wait, out cc);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "AlexanderStreet Video",
                                                                        video_id,
                                                                        title + video_found,
                                                                        iframe.Attributes["src"].Value,
                                                                        video_length,
                                                                        VideoParser.CheckTranscript(iframe),
                                                                        cc));
                    }
                }
                else if (iframe.Attributes["src"].Value.Contains("kanopy"))
                {
                    string video_id = iframe.Attributes["src"].Value.CleanSplit("embed/").LastOrDefault();
                    TimeSpan video_length;
                    bool cc;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetKanopyVideoLength(video_id, Chrome, Wait, out cc);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "Kanopy Video",
                                                                        video_id,
                                                                        title + video_found,
                                                                        iframe.Attributes["src"].Value,
                                                                        video_length,
                                                                        VideoParser.CheckTranscript(iframe),
                                                                        cc));
                    }
                }
                else if (iframe.Attributes["src"].Value.Contains("ambrosevideo"))
                {
                    string video_id = iframe.Attributes["src"].Value.CleanSplit("?").LastOrDefault().CleanSplit("&")[0];
                    TimeSpan video_length;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetAmbroseVideoLength(video_id, Chrome, Wait);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "Ambrose Video",
                                                                        video_id,
                                                                        title + video_found,
                                                                        iframe.Attributes["src"].Value,
                                                                        video_length,
                                                                        VideoParser.CheckTranscript(iframe)));
                    }
                }
                else if (iframe.Attributes["src"].Value.Contains("facebook"))
                {
                    string video_id = new Regex("\\d{17}").Match(iframe.Attributes["src"].Value).Value;
                    TimeSpan video_length;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetFacebookVideoLength(video_id, Chrome, Wait);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "Facebook Video",
                                                                        video_id,
                                                                        title + video_found,
                                                                        iframe.Attributes["src"].Value,
                                                                        video_length,
                                                                        VideoParser.CheckTranscript(iframe)));
                    }
                }
                else if (iframe.Attributes["src"].Value.Contains("dailymotion"))
                {
                    string video_id = iframe.Attributes["src"].Value.CleanSplit("/").LastOrDefault();
                    TimeSpan video_length;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetDailyMotionVideoLength(video_id, Chrome, Wait);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "DailyMotion Video",
                                                                        video_id,
                                                                        title + video_found,
                                                                        iframe.Attributes["src"].Value,
                                                                        video_length,
                                                                        VideoParser.CheckTranscript(iframe)));
                    }
                }
                else if (iframe.Attributes["src"].Value.Contains("vimeo"))
                {
                    string video_id = iframe.Attributes["src"].Value.CleanSplit("/").LastOrDefault().CleanSplit("?")[0];
                    TimeSpan video_length;
                    lock (Chrome)
                    {
                        lock (Wait)
                        {
                            video_length = VideoParser.GetVimeoVideoLength(video_id, Chrome, Wait);
                        }
                    }
                    string video_found;
                    if (video_length == new TimeSpan(0))
                    {
                        video_found = "\nVideo not found";
                    }
                    else
                    {
                        video_found = "";
                    }
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                        "Vimeo Video",
                                                                        video_id,
                                                                        title + video_found,
                                                                        iframe.Attributes["src"].Value,
                                                                        video_length,
                                                                        VideoParser.CheckTranscript(iframe)));
                    }
                }
                else
                {
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                                         "Iframe",
                                                                         "",
                                                                         title,
                                                                         iframe.Attributes["src"].Value,
                                                                         new TimeSpan(0),
                                                                         true,
                                                                         true));
                    }
                }
            }
        }
        private void ProcessVideoTags(DataToParse PageDocument)
        {
            var video_tag_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//video");
            if (video_tag_list == null)
            {
                return;
            }

            foreach (var video in video_tag_list)
            {
                if (video.Attributes["src"] == null)
                {
                    lock (Data)
                    {
                        Data.Add(new PageMediaData(PageDocument.Location,
                                                    "Inline Media Video",
                                                    "",
                                                    $"Something may be wrong with this video...\n{video.OuterHtml}",
                                                    "",
                                                    new TimeSpan(0),
                                                    VideoParser.CheckTranscript(video)));
                    }
                    continue;
                }
                string video_id = video.Attributes["src"].Value.CleanSplit("=")[1].CleanSplit("&")[0];
                lock (Data)
                {
                    Data.Add(new PageMediaData(PageDocument.Location,
                                                "Inline Media Video",
                                                video_id,
                                                "Inline Media:\nUnable to find title or video length for this type of video",
                                                video.Attributes["src"].Value,
                                                new TimeSpan(0),
                                                VideoParser.CheckTranscript(video)));
                }
            }
        }

        private void ProcessBrightcoveVideoHTML(DataToParse PageDocument)
        {
            var brightcove_list = PageDocument.Doc
                .DocumentNode
                ?.SelectNodes(@"//div[@id]")
                ?.Where(e => new Regex("\\d{13}").IsMatch(e.Id));
            if (brightcove_list == null)
            {
                return;
            }
            string BrightCoveVideoName = "";
            foreach (var video in brightcove_list)
            {
                string video_id = new Regex("\\d{13}").Match(video.Id).Value;
                TimeSpan video_length;
                bool cc;
                lock (Chrome)
                {
                    lock (Wait)
                    {
                        video_length = VideoParser.GetBrightcoveVideoLength(video_id, Chrome, Wait, out cc, out BrightCoveVideoName);
                    }
                }
                lock (Data)
                {
                    Data.Add(new PageMediaData(PageDocument.Location,
                                                                "Brightcove Video",
                                                                video_id,
                                                                BrightCoveVideoName,
                                                                $"https://studio.brightcove.com/products/videocloud/media/videos/search/{video_id}",
                                                                video_length,
                                                                VideoParser.CheckTranscript(video),
                                                                cc));
                }
            }
        }
    }

}
