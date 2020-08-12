using System;
using System.Collections.Generic;
using System.Linq;
using My.StringExtentions;
using System.IO;
using HtmlAgilityPack;
using RestSharp;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using My.SeleniumExtentions;
using System.Xml;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Newtonsoft.Json;
using System.Reflection;

namespace My.VideoParser
{
    public class YoutubeData
    {   //Class to get data from the Google API
        //Needs the item object first that then contains the info
        public class ItemData
        {
            public class ContentDetails
            {
                public string duration { get; set; }
                public bool caption { get; set; }
                public bool licensedContent { get; set; }
            }
            public ContentDetails contentDetails { get; set; }
            public string id { get; set; }
        }
        public List<ItemData> items { get; set; }
    }

    public static class VideoParser
    {
        static VideoParser()
        {
            string json = "";
            string path = Assembly.GetEntryAssembly().Location.Contains("source") ? @"C:\Users\jwilli48\Desktop\AccessibilityTools\A11yPanel\options.json" :
                                System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + @"\options.json";
            using (StreamReader r = new StreamReader(path))
            {
                json = r.ReadToEnd();
            }
            Options = JsonConvert.DeserializeObject<My.PanelOptions>(json);
            Options.FilesToIgnore.ForEach(f => f = f.ToLower());
            GoogleApi = Options.GoogleApi;
        }
        private static My.PanelOptions Options;
        //Static class to be used to parse information for any videos.
        private static string GoogleApi;
        public static bool CheckTranscript(HtmlNode element)
        {
            if (element.OuterHtml.ToLower().Contains("transcript"))
            {
                return true;
            }
            if(element.NextSibling != null && element.NextSibling.OuterHtml.ToLower().Contains("transcript"))
            {
                return true;
            }
            while (element.NextSibling != null && !element.NextSibling.OuterHtml.ToLower().Contains("iframe"))
            {
                element = element.NextSibling;
                if (element.OuterHtml.ToLower().Contains("transcript"))
                {
                    return true;
                }
            }

            while (element.ParentNode != null && !(element.ParentNode.Name == "#document"))
            {
                element = element.ParentNode;
            }
            if(element.OuterHtml.ToLower().Contains("transcript"))
            {
                return true;
            }
            while (element.NextSibling != null && !element.NextSibling.OuterHtml.ToLower().Contains("iframe"))
            {
                element = element.NextSibling;
                if (element.OuterHtml.ToLower().Contains("transcript"))
                {
                    return true;
                }
            }
            return false;
        }
        
        public static bool CheckTranscript(HtmlNode element, out string YesOrNo)
        {   //If you want a string Yes or No output instead of a bool
            if(CheckTranscript(element))
            {
                YesOrNo = "Yes";
                return true;
            } else
            {
                YesOrNo = "No";
                return false;
            }            
        }
        public static string GetYTChannelName(string id)
        {
            var youTube = new YouTubeService(new BaseClientService.Initializer() { ApiKey = GoogleApi });
            Dictionary<String, String> parameters = new Dictionary<string, string>
            {
                ["part"] = "snippet"
            };
            var channelsListRequest = youTube.Channels.List("snippet");
            channelsListRequest.Id = id;
            var channelListResponse = channelsListRequest.Execute();

            return channelListResponse.Items[0].Snippet.Title;
        }
        //Below is the functions to get the video length from various ID inputs. Uses Selenium ChromeDriver to get some of them without an API.
        //They all return a timespan object with the length of the video. 
        //It will return a timespan of 0 if the length could not be found.
        public static TimeSpan GetYoutubeVideoLength(string video_id)
        {
            string url = $"https://www.googleapis.com/youtube/v3/videos?id={video_id}&key={GoogleApi}&part=contentDetails";
            var restClient = new RestClient(url);
            var request = new RestRequest(Method.GET);
            var response = restClient.Execute<YoutubeData>(request);
            return XmlConvert.ToTimeSpan(response.Data.items[0].contentDetails.duration);
        }
        public static TimeSpan GetBrightcoveVideoLength(string video_id, ChromeDriver chrome, WebDriverWait wait, out bool cc, out string text)
        {
            chrome.Url = $"https://studio.brightcove.com/products/videocloud/media/videos/search/{video_id}";
            string length;
            text = "";
            try
            {
                length = wait.UntilElementIsVisible(By.CssSelector("div[class*=\"Thumbnail\"]")).Text;
            }
            catch
            {
                Console.WriteLine("Video not found");
                length = "00:00";
                cc = false;
                return new TimeSpan(0);
            }
            if((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            length = (length as string).RollOverTime();
            try
            {
                chrome.FindElementsByCssSelector("div.name").Where(c => c.Text.Contains(video_id)).FirstOrDefault().FindElement(By.TagName("a")).Click();
                cc = !wait.UntilElementExist(By.CssSelector("section#textTracksPanel")).Text.Contains("There are no text tracks");
                
            }
            catch
            {
                cc = false;
            }
            try
            {
                var NameElement = wait.UntilElementExist(By.CssSelector("div[id*=\"video-name\"]"));
                text = "\nVideo name: " + NameElement.Text;
            }
            catch
            {
                text = "\nFailed to find video name";
            }
            if (!TimeSpan.TryParseExact(length, @"h\:mm\:ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
        public static TimeSpan GetBYUMediaSiteVideoLength(string video_id, ChromeDriver chrome, WebDriverWait wait, out bool cc)
        {
            chrome.Url = $"https://byu.mediasite.com/Mediasite/Play/{video_id}";
            dynamic length = null;
            try
            {
                wait.UntilElementIsVisible(By.CssSelector(".play-button")).Click();
                while ("0:00" == length || "" == length || null == length)
                {
                    length = wait.UntilElementIsVisible(By.CssSelector("span[class*=\"duration\"]")).Text;
                }
            }
            catch
            {
                try
                {
                    while ("0:00" == length || "" == length || null == length)
                    {
                        length = wait.UntilElementIsVisible(By.CssSelector("span[class*=\"duration\"]")).Text;
                    }
                }
                catch
                {
                    Console.WriteLine("Video not found");
                    length = "00:00";
                    cc = false;
                    return new TimeSpan(0);
                }
            }

            if ((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            length = (length as string).RollOverTime();
            cc = wait.UntilElementExist(By.CssSelector("button.cc.ui-button")).GetAttribute("aria-disabled") == "false" ? true : false;
            if (!TimeSpan.TryParseExact(length, "hh':'mm':'ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
        public static TimeSpan GetPanoptoVideoLength(string video_id, ChromeDriver chrome, WebDriverWait wait, out bool cc)
        {
            chrome.Url = $"https://byu.hosted.panopto.com/Panopto/Pages/Embed.aspx?id={video_id}&amp;v=1";
            while ((string)chrome.ExecuteScript("return document.readyState") != "complete") { };
            dynamic length = null;
            try
            {
                while (chrome.FindElementsByCssSelector("[id=copyrightNoticeContainer]").FirstOrDefault().Displayed) { };
                wait.UntilElementIsVisible(By.CssSelector("div#title"));
                wait.UntilElementIsVisible(By.CssSelector("div#playIconContainer")).Click();

                length = wait.UntilElementIsVisible(By.CssSelector("span[class*=\"duration\"]")).Text;
            }
            catch
            {
                Console.WriteLine("Video not found");
                length = "00:00";
                cc = false;
                return new TimeSpan(0);
            }
            if ((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            length = (length as string).RollOverTime();
            cc = !wait.UntilElementExist(By.CssSelector("div.fp-menu.fp-subtitle-menu")).GetAttribute("outerHTML").ToLower().Contains("no subtitles");
            if (!TimeSpan.TryParseExact(length, @"h\:mm\:ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
        public static TimeSpan GetPanoptoVideoViewerLength(string video_id, ChromeDriver chrome, WebDriverWait wait, out bool cc)
        {
            chrome.Url = $"https://byu.hosted.panopto.com/Panopto/Pages/Viewer.aspx?tid={video_id}";
            while ((string)chrome.ExecuteScript("return document.readyState") != "complete") { };
            dynamic length = null;
            try
            {
                length = wait.UntilElementIsVisible(By.CssSelector("div#timeRemaining")).Text.Replace("-", "");
            }
            catch
            {
                Console.WriteLine("Video not found");
                length = "00:00";
                cc = false;
                return new TimeSpan(0);
            }
            if((length as string).Length < 5)
            {
                length = "0" + length;
            }
            if ((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            length = (length as string).RollOverTime();
            cc = wait.UntilElementIsVisible(By.CssSelector("div#transcriptTabHeader")).Enabled;
            if (!TimeSpan.TryParseExact(length, @"h\:mm\:ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
        public static TimeSpan GetAlexanderStreetVideoLength(string video_id, ChromeDriver chrome, WebDriverWait wait, out bool cc)
        {
            chrome.Url = $"https://search.alexanderstreet.com/view/work/bibliographic_entity|video_work|{video_id}";
            dynamic length;
            try
            {
                length = wait.UntilElementIsVisible(By.CssSelector("span.fulltime")).Text;
            }
            catch
            {
                Console.WriteLine("Video not found");
                length = "00:00";
                cc = false;
                return new TimeSpan(0);
            }
            if ((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            length = (length as string).RollOverTime();
            cc = wait.UntilElementIsVisible(By.CssSelector("ul.tabs")).Text.ToLower().Contains("transcript");
            if (!TimeSpan.TryParseExact(length, @"h\:mm\:ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
        public static TimeSpan GetAlexanderStreenLinkLength(string video_id, ChromeDriver chrome, WebDriverWait wait, out bool cc)
        {
            chrome.Url = $"https://search.alexanderstreet.com/view/work/bibliographic_entity|video_work|{video_id}";
            dynamic length;
            try
            {
                length = wait.UntilElementIsVisible(By.CssSelector("span.fulltime")).Text;
            }
            catch
            {
                Console.WriteLine("Video not found");
                length = "00:00";
                cc = false;
                return new TimeSpan(0);
            }
            if ((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            length = (length as string).RollOverTime();
            cc = wait.UntilElementIsVisible(By.CssSelector("ul.tabs")).Text.ToLower().Contains("transcript");
            if (!TimeSpan.TryParseExact(length, @"h\:mm\:ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
        public static TimeSpan GetKanopyVideoLength(string video_id, ChromeDriver chrome, WebDriverWait wait, out bool cc)
        {
            chrome.Url = $"https://byu.kanopy.com/embed/{video_id}";
            dynamic length;
            try
            {
                wait.UntilElementIsVisible(By.CssSelector("button.vjs-big-play-button")).Click();
                length = wait.UntilElementIsVisible(By.CssSelector("div.vjs-remaining-time-display"))
                                .Text
                                .Split('-')
                                .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                .LastOrDefault();
            }
            catch
            {
                Console.WriteLine("Video not found");
                length = "00:00";
                cc = false;
                return new TimeSpan(0);
            }
            if ((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            length = (length as string).RollOverTime();
            cc = wait.UntilElementIsVisible(By.CssSelector("span[data-title='Press play to launch the captions")).Displayed;
            if (!TimeSpan.TryParseExact(length, @"h\:mm\:ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
        public static TimeSpan GetKanopyLinkLength(string video_id, ChromeDriver chrome, WebDriverWait wait, out bool cc)
        {
            chrome.Url = $"https://byu.kanopy.com/video/{video_id}";
            dynamic length;
            try
            {
                wait.UntilElementIsVisible(By.CssSelector("button.vjs-big-play-button")).Click();
                length = wait.UntilElementIsVisible(By.CssSelector("div.vjs-remaining-time-display"))
                                .Text
                                .Split('-')
                                .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                .LastOrDefault();
            }
            catch
            {
                Console.WriteLine("Video not found");
                length = "00:00";
                cc = false;
                return new TimeSpan(0);
            }
            if ((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            length = (length as string).RollOverTime();
            cc = wait.UntilElementIsVisible(By.CssSelector("span[data-title='Press play to launch the captions")).Displayed;
            if (!TimeSpan.TryParseExact(length, @"h\:mm\:ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
        public static TimeSpan GetAmbroseVideoLength(string video_id, ChromeDriver chrome, WebDriverWait wait)
        {
            //Just realized I never implemented this function in my powershell program
            return new TimeSpan(0);
            chrome.Url = $"";
            dynamic length;
            try
            {

            }
            catch
            {
                Console.WriteLine("Video not found");
                length = "00:00";
            }
            if ((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            if (!TimeSpan.TryParseExact(length, @"h\:mm\:ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
        public static TimeSpan GetFacebookVideoLength(string video_id, ChromeDriver chrome, WebDriverWait wait)
        {
            chrome.Url = $"https://www.facebook.com/video/embed?video_id={video_id}";
            dynamic length;
            try
            {
                wait.UntilElementIsVisible(By.CssSelector("img")).Click();
                length = wait.UntilElementIsVisible(By.CssSelector("div[playbackdurationtimestamp]")).Text.Replace('-', '0');
            }
            catch
            {
                try
                {
                    //If that didn't work then try refreshing the page (I kept running into false negatives) and try again
                    chrome.Navigate().Refresh();
                    wait.UntilElementIsVisible(By.CssSelector("img")).Click();
                    length = wait.UntilElementIsVisible(By.CssSelector("div[playbackdurationtimestamp]")).Text.Replace('-', '0');
                }
                catch
                {
                    Console.WriteLine("Video not found");
                    length = "00:00";
                }
            }
            if ((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            length = (length as string).RollOverTime();
            if (!TimeSpan.TryParseExact(length, @"h\:mm\:ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
        public static TimeSpan GetDailyMotionVideoLength(string video_id, ChromeDriver chrome, WebDriverWait wait)
        {
            chrome.Url = $"https://www.dailymotion.com/embed/video/{video_id}";
            dynamic length;
            try
            {
                wait.UntilElementIsVisible(By.CssSelector("button[aria-label*=\"Playback\"]")).Click();
                length = wait.UntilElementIsVisible(By.CssSelector("span[aria-label*=\"Duration\"]")).Text;
            }
            catch
            {
                Console.WriteLine("Video not found");
                length = "00:00";
            }
            if ((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            length = (length as string).RollOverTime();
            if (!TimeSpan.TryParseExact(length, @"h\:mm\:ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
        public static TimeSpan GetVimeoVideoLength(string video_id, ChromeDriver chrome, WebDriverWait wait)
        {
            chrome.Url = $"https://player.vimeo.com/video/{video_id}";
            dynamic length;
            try
            {
                length = wait.UntilElementIsVisible(By.CssSelector("div.timecode")).Text;
            }
            catch
            {
                Console.WriteLine("Video not found");
                length = "00:00";
            }
            if ((length as string).Count(c => c == ':') < 2)
            {
                length = "00:" + length;
            }
            length = (length as string).RollOverTime();
            if (!TimeSpan.TryParseExact(length, @"h\:mm\:ss", null, out TimeSpan video_length))
            {
                return new TimeSpan(0);
            }
            return video_length;
        }
    }
}
