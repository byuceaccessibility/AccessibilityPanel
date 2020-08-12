namespace ReportGenerators
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using RestSharp;
    using System.Text.RegularExpressions;
    using System.Web;
    using My.StringExtentions;
    using My.VideoParser;
    using OpenQA.Selenium.Chrome;
    using OpenQA.Selenium.Support.UI;
    using My.SeleniumExtentions;

    /// <summary>
    /// Object to receive color contrast response
    /// </summary>
    public class ColorContrast
    {   //Helper structure to get the information from the WebAIM color contrast API.
        public double ratio { get; set; }
        public string AA { get; set; }
        public string AALarge { get; set; }
        public override string ToString()
        {   //We want it to print the information provided.
            var props = typeof(ColorContrast).GetProperties();
            var sb = new StringBuilder();
            foreach (var p in props)
            {
                sb.AppendLine(p.Name + ": " + p.GetValue(this, null));
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Main class for creating accessibility report
    /// </summary>
    public class A11yParser : RParserBase
    {
        public A11yParser()
        {
            PathToChromedriver = Options.ChromeDriverPath;
        }
        private string PathToChromedriver;    
        /// <summary>
        /// Main function to begin processing pages. Calls all the other functions. 
        /// </summary>
        /// <param name="page_info"></param>
        public override void ProcessContent(Dictionary<string, string> page_info)
        {            
            //Function to begin processing a page and storing the data within the Data list (see RParserBase class)
            //Make sure page is not empty
            var url = page_info.Keys.ElementAt(0);  //key is the url, points to the body (HTML)
            if (page_info[url] == null)
            {
                return;
            }
            //Ignore certain pages
            if(Options.FilesToIgnore.Contains(url.ToLower()))
            {
                return;
            }

            // No longer in use as we need to save a valid XPath. JavaScript messes it up. 

            //// Go through the online version to make sure there aren't JS issues
            //if (new Regex(@"iscontent\.byu\.edu|isdev\.byu\.edu|file:///").IsMatch(url))
            //{   //Get the HTML from a browser so we can see if any accessibility issues were created with JavaScript.
            //    //This is run in a multi-threaded environment, so we want to limit how long we use the browser as it could dramtically slow things down if it tries to open to many.
            //    var chromeDriverService = ChromeDriverService.CreateDefaultService(PathToChromedriver);
            //    chromeDriverService.HideCommandPromptWindow = true;
            //    var ChromeOptions = new ChromeOptions();
            //    ChromeOptions.AddArguments("headless", "muteaudio");
            //    var Chrome = new ChromeDriver(chromeDriverService, ChromeOptions);
            //    var Wait = new WebDriverWait(Chrome, new TimeSpan(0, 0, 5));
            //    Chrome.Url = url;
            //    if (Chrome.isAlertPresent())
            //    {
            //        Chrome.SwitchTo().Alert().Dismiss();
            //        Chrome.SwitchTo().Window(Chrome.CurrentWindowHandle);
            //    }
            //    Wait.UntilPageLoads();
            //    if (Chrome.isAlertPresent())
            //    {
            //        Chrome.SwitchTo().Alert().Dismiss();
            //        Chrome.SwitchTo().Window(Chrome.CurrentWindowHandle);
            //    }
            //    var Online_PageDocument = new DataToParse(url, Chrome.FindElementByTagName("body").GetAttribute("outerHTML"));
            //    Chrome.Quit();
            //    ProcessLinks(Online_PageDocument);
            //    ProcessImages(Online_PageDocument);
            //    ProcessIframes(Online_PageDocument);
            //    ProcessTables(Online_PageDocument);
            //    ProcessBrightcoveVideoHTML(Online_PageDocument);
            //    ProcessHeaders(Online_PageDocument);
            //    ProcessSemantics(Online_PageDocument);
            //    ProcessVideoTags(Online_PageDocument);
            //    ProcessFlash(Online_PageDocument);
            //    ProcessColor(Online_PageDocument);
            //    ProcessMathJax(Online_PageDocument);
            //    ProcessOnclicks(Online_PageDocument);
            //    ProcessAudioElements(Online_PageDocument);
            //}
            //else
            //{   //For a canvas course. I want to avoid doing the above as this would require having to use Duo to log into BYU account to access the Canvas course.
            //    //May have to do the above if I find out JavaScript is being used within Canvas.
            //    //Set our current document (creates an HTML dom from the pages body)
                var PageDocument = new DataToParse(url, page_info[url]);
                //Process the elements of the page from the HTML
                ProcessLinks(PageDocument);
                ProcessImages(PageDocument);
                ProcessIframes(PageDocument);
                ProcessTables(PageDocument);
                ProcessBrightcoveVideoHTML(PageDocument);
                ProcessHeaders(PageDocument);
                ProcessSemantics(PageDocument);
                ProcessVideoTags(PageDocument);
                ProcessFlash(PageDocument);
                ProcessColor(PageDocument);
                ProcessMathJax(PageDocument);
                ProcessOnclicks(PageDocument);
                ProcessAudioElements(PageDocument);
            //}
            
        }

        private void ProcessLinks(DataToParse PageDocument)
        {
            //Get all links within page
            var link_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//a");
            //Make sure its not null
            if (link_list == null)
            {
                return;
            }
            //Loop through all links
            foreach (var link in link_list)
            {
                if (link.Attributes["onclick"] != null)
                {   //Onclick links are not accessible
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Link", "", link.Attributes["onclick"].Value, "JavaScript links are not accessible", 1, link.XPath));
                    }
                }
                else if (link.Attributes["href"] == null)
                {   //Links should have an href
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Link", "", link.OuterHtml, "Empty link tag", 1, link.XPath));
                    }
                }
                if (link.InnerHtml.Contains("<img"))
                {   //If it is an image ignore it for now, need to check alt text
                    continue;
                }
                if (link.InnerText == null || link.InnerText == "")
                {   //See if it is a link without text
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Link", "", $"Invisible link with no text\n{link.OuterHtml}", "Adjust Link Text", 1, link.XPath));
                    }
                }
                else if (new Regex("^ ?here", RegexOptions.IgnoreCase).IsMatch(link.InnerText))
                {   //If it begins with the word here probably not descriptive link text
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Link", "", link.InnerText, "Adjust Link Text", 1, link.XPath));
                    }
                }
                else if (new Regex("^ ?[A-Za-z\\.]+ ?$").IsMatch(link.InnerText))
                {   //If it is a single word
                    if (link_list.Where(s => s.InnerText == link.InnerText).Count() > 1)
                    {   //And if the single word is used for more then one link
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "Link", "", link.InnerText, "Adjust Link Text", 1, link.XPath));
                        }
                    }
                }
                else if (new Regex("http|www\\.|Link|Click", RegexOptions.IgnoreCase).IsMatch(link.InnerText))
                {   //See if it is just a url
                    if (new Regex("Links to an external site").IsMatch(link.InnerText))
                    {   //This is commonly used in Canvas, we just ignore it
                        continue;
                    }
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Link", "", link.InnerText, "Adjust Link Text", 1, link.XPath));
                    }
                }
            }
        }

        private void ProcessImages(DataToParse PageDocument)
        {
            //Get list of images
            var image_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//img");
            //Make sure it is not null
            if (image_list == null)
            {
                return;
            }
            //Loop through all images
            foreach (var image in image_list)
            {
                var alt = image.Attributes["alt"]?.Value;
                //Get the alt text
                if (String.IsNullOrEmpty(alt))
                {   //Empty alt text should be manually checked for decortive qualities
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Image", "", image.OuterHtml, "Alt text may need adjustment or No alt attribute", 1, image.XPath));
                    }
                }
                else if (new Regex("banner", RegexOptions.IgnoreCase).IsMatch(alt))
                {   //Banners shouldn't have alt text
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Image", "", alt, "Alt text may need adjustment", 1, image.XPath));
                    }
                }
                else if (new Regex("Placeholder", RegexOptions.IgnoreCase).IsMatch(alt))
                {   //Placeholder probably means the alt text was forgotten to be changed
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Image", "", alt, "Alt text may need adjustment", 1, image.XPath));
                    }
                }
                else if (new Regex("\\.jpg", RegexOptions.IgnoreCase).IsMatch(alt))
                {   //Make sure it is not just the images file name
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Image", "", alt, "Alt text may need adjustment", 1, image.XPath));
                    }
                }
                else if (new Regex("\\.png", RegexOptions.IgnoreCase).IsMatch(alt))
                {
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Image", "", alt, "Alt text may need adjustment", 1, image.XPath));
                    }
                }
                else if (new Regex("http", RegexOptions.IgnoreCase).IsMatch(alt))
                {   //It should not be a url
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Image", "", alt, "Alt text may need adjustment", 1, image.XPath));
                    }
                }
                else if (new Regex("LaTeX:", RegexOptions.IgnoreCase).IsMatch(alt))
                {   //Should not be latex (ran into this a couple of times)
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Image", "", alt, "Alt text may need adjustment", 1, image.XPath));
                    }
                }
            }
        }
        private void ProcessTables(DataToParse PageDocument)
        {
            //Get all tables
            var table_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//table");
            //Make sure it isn't null
            if (table_list == null)
            {
                return;
            }
            //Count the tables so we can know which table on the page has the issues
            var table_num = 1;
            foreach (var table in table_list)
            {
                //Get list of headers, data cells, how many rows, and any stretched cells
                var table_headers = table.SelectNodes(".//th");
                var table_data_cells = table.SelectNodes(".//td");
                var table_rows = table.SelectNodes(".//tr");
                var stretched_cells = table.SelectNodes(".//*[@colspan]");
                //Init the issue string
                string issues = "";
                //See if there are any stretchedcells
                if (stretched_cells != null)
                {
                    issues += "\nStretched table cell(s) should be a <caption> title for the table";
                }
                //See how many rows there are, if there is 3 or more and there are no headers then flag it as needing headers

                var num_rows = table_rows?.Count() == null ? 0 : table_rows.Count();
                if (num_rows >= 3)
                {
                    if (table_headers == null)
                    {
                        issues += "\nTable has no headers (headers should have scope tags)";
                    }
                }
                //See how many headers have scopes, should be the same number as the number of headers
                var scope_headers = table_headers?.Count(c => c.Attributes["scope"] != null);
                if((table_headers != null) 
                    && (table_headers.Count() > 0) 
                    && (scope_headers == null || scope_headers != table_headers.Count()))
                {
                    issues += "\nTable headers should have a scope attribute";
                }
                //See if any data cells have scopes when they should not
                var scope_cells = table_data_cells?.Count(c => c.Attributes["scope"] != null);
                if (scope_cells != null && scope_cells > 0)
                {
                    issues += "\nNon-header table cells should not have scope attributes";
                }
                if(!table.HasChildNodes)
                {
                    issues += "\nEmpty table should be removed";
                }

                var numRowsWithMultipleHeaders = table_rows?.Count(c => c.ChildNodes.Where(child => child.Name == "th").Count() > 1 );
                if (numRowsWithMultipleHeaders != null && numRowsWithMultipleHeaders > 1) {
                    issues += "\nTables should not have multiple header rows, they should be split into seperate tables or have the headers combined";
                }
                //If any issues were found then add it to the list
                if (issues != null && issues != "")
                {
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Table", "", $"Table number {table_num}:{issues}", "Revise table", 1));
                    }
                }
                table_num++;
            }
        }
        private void ProcessIframes(DataToParse PageDocument)
        {
            //Get list of iframes
            var iframe_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//iframe");
            //Make sure its not null
            if (iframe_list == null)
            {
                return;
            }
            //Keep track of what iframe we are on
            var iframe_number = 1;
            foreach (var iframe in iframe_list)
            {
                //Get the source attribute, every iframe should have one
                var src = "";
                if(iframe.Attributes["src"] == null)
                {
                    if(iframe.Attributes["data-src"] != null)
                    {   //found that sometimes a data-src is used istead of the normal src
                        src = iframe.Attributes["data-src"].Value;
                    }
                    else
                    {
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location,
                                                    "Iframe",
                                                    "Can't find source",
                                                    "Unable to find iframe source",
                                                    "Iframes should all have a soruce",
                                                    3,
                                                    iframe.XPath));
                        }
                        continue;
                    }
                   
                }
                else
                {
                    src = iframe.Attributes["src"].Value;
                }
                if (iframe.Attributes["title"] == null)
                {
                    //Only real accessiblity issue we can check is if it has a title or not
                    if (new Regex("youtube", RegexOptions.IgnoreCase).IsMatch(src))
                    {
                        //Get the youtube information
                        var uri = new Uri(src);
                        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                        var videoId = string.Empty;
                        if (query.AllKeys.Contains("v"))
                        {
                            videoId = query["v"];
                        }
                        else
                        {
                            videoId = uri.Segments.LastOrDefault();
                        }
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "Youtube Video", videoId, "", "Needs a title", 1, iframe.XPath));
                        }
                    }
                    else if (new Regex("brightcove", RegexOptions.IgnoreCase).IsMatch(src))
                    {
                        //Get brightcove info
                        var videoId = src.CleanSplit("=").LastOrDefault().CleanSplit("&").FirstOrDefault();
                        if (!src.Contains("https:"))
                        {   //Make sure it has the https on it
                            src = $"https:{src}";
                        }
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "Brightcove Video", videoId, "", "Needs a title", 1, iframe.XPath));
                        }
                    }
                    else if (new Regex("H5P", RegexOptions.IgnoreCase).IsMatch(src))
                    {   //H5P can just be added
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "H5P", "", "", "Needs a title", 1, iframe.XPath));
                        }
                    }
                    else if (new Regex("byu\\.mediasite", RegexOptions.IgnoreCase).IsMatch(src))
                    {   //Get id
                        var videoId = src.CleanSplit("/").LastOrDefault();
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "BYU Mediasite Video", videoId, "", "Needs a title", 1, iframe.XPath));
                        }
                    }
                    else if (new Regex("panopto", RegexOptions.IgnoreCase).IsMatch(src))
                    {
                        var videoId = src.CleanSplit("id=").LastOrDefault().CleanSplit('&').FirstOrDefault();
                                            
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "Panopto Video", videoId, "", "Needs a title", 1, iframe.XPath));
                        }
                    }
                    else if (new Regex("alexanderstreet", RegexOptions.IgnoreCase).IsMatch(src))
                    {
                        var videoId = src.Split(new string[] { "token/" }, StringSplitOptions.RemoveEmptyEntries)
                                            .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                            .LastOrDefault();
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "AlexanderStreen Video", videoId, "", "Needs a title", 1, iframe.XPath));
                        }
                    }
                    else if (new Regex("kanopy", RegexOptions.IgnoreCase).IsMatch(src))
                    {
                        var videoId = src.Split(new string[] { "embed/" }, StringSplitOptions.RemoveEmptyEntries)
                                            .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                            .LastOrDefault();
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "Kanopy Video", videoId, "", "Needs a title", 1, iframe.XPath));
                        }
                    }
                    else if (new Regex("ambrosevideo", RegexOptions.IgnoreCase).IsMatch(src))
                    {
                        var videoId = src.Split('?')
                                            .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                            .LastOrDefault()
                                            .Split('&')
                                            .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                            .FirstOrDefault();
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "Ambrose Video", videoId, "", "NEeds a title", 1, iframe.XPath));
                        }
                    }
                    else if (new Regex("facebook", RegexOptions.IgnoreCase).IsMatch(src))
                    {
                        var videoId = new Regex("\\d{17}").Match(src).Value;
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "Facebook Video", videoId, "", "Needs a title", 1, iframe.XPath));
                        }
                    }
                    else if (new Regex("dailymotion", RegexOptions.IgnoreCase).IsMatch(src))
                    {
                        var videoId = src.Split('/')
                                            .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                            .LastOrDefault();
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "Facebook Video", videoId, "", "Needs a title", 1, iframe.XPath));
                        }
                    }
                    else if (new Regex("vimeo", RegexOptions.IgnoreCase).IsMatch(src))
                    {
                        var videoId = src.Split('/')
                                            .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                            .LastOrDefault()
                                            .Split('?')
                                            .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                            .FirstOrDefault();
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "Vimeo Video", videoId, "", "Needs a title", 1, iframe.XPath));
                        }
                    }
                    else
                    {
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "Iframe", "", "", "Needs a title", 1, iframe.XPath));
                        }
                    }
                }
                if (new Regex("brightcove|byu\\.mediasite|panopto|vimeo|dailymotion|facebook|ambrosevideo|kanopy|alexanderstreet", RegexOptions.IgnoreCase).IsMatch(src))
                {   //If it is a video then need to check if there is a transcript
                    if (!VideoParser.CheckTranscript(iframe))
                    {
                        lock (Data)
                        {
                            Data.Add(new PageA11yData(PageDocument.Location, "Transcript", "", $"Video number {iframe_number} on page", "No transcript found", 5, iframe.XPath));
                        }
                    }
                }
                iframe_number++;
            }
        }
        private void ProcessBrightcoveVideoHTML(DataToParse PageDocument)
        {   //A lot of our HTML templates use a div + javascript to insert the brightcove video.
            var brightcove_list = PageDocument.Doc
                .DocumentNode
                ?.SelectNodes(@"//div[@id]")
                ?.Where(e => new Regex("\\d{13}").IsMatch(e.Id));
            if (brightcove_list == null)
            {
                return;
            }
            foreach (var video in brightcove_list)
            {
                if (!VideoParser.CheckTranscript(video))
                {
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Transcript", video.Attributes["id"].Value, $"No transcript found for BrightCove video with id:\n{video.Attributes["id"].Value}", "No transcript found", 5, video.XPath));
                    }
                }
            }
        }
        private void ProcessHeaders(DataToParse PageDocument)
        {   //Process all the headers on the page
            var header_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//h1 | //h2 | //h3 | //h4 | //h5 | //h6");
            if (header_list == null)
            {
                return;
            }
            foreach (var header in header_list)
            {
                if (header.Attributes["class"]?.Value?.Contains("screenreader-only") == true)
                {
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Header", "", header.OuterHtml, "Check if header is meant to be invisible", 1, header.XPath));
                    }
                }
            }
        }
        private void ProcessSemantics(DataToParse PageDocument)
        {
            //Process page semantics (i and b tags).
            var i_or_b_tag_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//i | //b");
            if (i_or_b_tag_list == null)
            {
                return;
            }
            else if (i_or_b_tag_list.Count() > 0)
            {
                //Flag if any i or b tags are found
                lock (Data)
                {
                    Data.Add(new PageA11yData(PageDocument.Location, "<i> or <b> tags", "", "Page contains <i> or <b> tags", "<i>/<b> tags should be <em>/<strong> tags", 1));
                }
            }
        }

        private void ProcessVideoTags(DataToParse PageDocument)
        {   //process any video tags
            var videotag_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//video");
            if (videotag_list == null)
            {
                return;
            }
            foreach (var videotag in videotag_list)
            {
                string src, videoId;
                if(videotag.Attributes["src"] == null)
                {
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Inline Media Video", videotag.OuterHtml, "Something may be wrong with this video...", "Check video", 3, videotag.XPath));
                    }
                    src = "";
                    videoId = "Unable to find ... ";
                }
                else
                {
                    src = videotag.Attributes["src"].Value;
                    videoId = src.Split('=')
                                        .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                        .ElementAt(1)
                                        .Split('&')
                                        .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                                        .FirstOrDefault();
                }

                if (!VideoParser.CheckTranscript(videotag))
                {
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Inline Media Video", videoId, "Inline Media Video\n", "No transcript found", 5, videotag.XPath));
                    }
                }
            }
        }

        private void ProcessFlash(DataToParse PageDocument)
        {   //If any flash is found it is automatically marked as inaccessible
            var flash_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//object[contains(@id, \"flash\")]");
            if (flash_list == null)
            {
                return;
            }
            else if (flash_list.Count() > 0)
            {
                //Flash shouldn't be used anywhere
                lock (Data)
                {
                    Data.Add(new PageA11yData(PageDocument.Location, "Flash Element", "", $"{flash_list.Count()} embedded flash element(s) on this page", "Flash is inaccessible", 5));
                }
            }

        }
        private void ProcessColor(DataToParse PageDocument)
        {   //Process any elements that have a color style
            var colored_element_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//*[contains(@style, \"color\")]");
            if (colored_element_list == null)
            {
                return;
            }
            foreach (var color in colored_element_list)
            {
                
                System.Web.UI.CssStyleCollection style = new System.Web.UI.WebControls.Panel().Style; //Get styles of element
                style.Value = color.Attributes["style"].Value; 
                var background_color = style["background-color"];   //Grab background color
                if (background_color == null)   //If there is no background color look at parent elements first
                {
                    var helper = color;
                    while(helper.ParentNode != null)
                    {
                        helper = helper.ParentNode;
                        System.Web.UI.CssStyleCollection check = new System.Web.UI.WebControls.Panel().Style;
                        check.Value = helper.Attributes["style"]?.Value;
                        if(check.Value != null)
                        {
                            background_color = check["background-color"];
                            if(background_color != null)
                            {   //once we find a background-color then we can stop looking
                                break;
                            }
                        }
                    }
                    //if its still empty set to default
                    if(background_color == null)
                    {
                        //Default background color is white
                        background_color = "#FFFFFF";
                    }
                }

                var foreground_color = style["color"];
                if (foreground_color == null)
                {
                    //Check parent elements for foreground color if the current did not have one
                    var helper = color;
                    while (helper.ParentNode != null)
                    {
                        helper = helper.ParentNode;
                        System.Web.UI.CssStyleCollection check = new System.Web.UI.WebControls.Panel().Style;
                        check.Value = helper.Attributes["style"]?.Value;
                        if (check.Value != null)
                        {
                            foreground_color = check["color"];
                            if (foreground_color != null)
                            {
                                break;
                            }
                        }
                    }
                    //If its still empty set to default color
                    if (foreground_color == null)
                    {
                        //Default text color is black
                        foreground_color = "#000000";
                    }
                }
                /////////// To reduce number of false positives / negatives the next bit looks at any elements between the current and the actual text to see if it changes
                //////////  Often run into elements that immediately have their color overwritten and is never used
                var check_children = color;
                while (check_children.FirstChild != null && check_children.FirstChild.Name != "#text")
                {   //Checks till we run into the text if there are any color changes.
                    //May not work very well if it is something like <p style="color: base;">asdasdsa<span style="color: NewColor;">asdasd</span>asdasd<span style="color: DiffColor;">asdasd</span></p>
                    check_children = check_children.FirstChild;
                    System.Web.UI.CssStyleCollection check = new System.Web.UI.WebControls.Panel().Style;
                    check.Value = check_children.Attributes["style"]?.Value;
                    if (check["color"] != null)
                    {
                        foreground_color = check["color"];
                    }
                    if(check["background-color"] != null)
                    {
                        background_color = check["background-color"];
                    }
                }
                
                if (!background_color.Contains("#"))
                {   //If it doesn't have a # then it is a known named color, needs to be converted to hex
                    //the & 0xFFFFFF cuts off the A of the ARGB
                    int rgb = System.Drawing.Color.FromName(background_color.FirstCharToUpper()).ToArgb() & 0xFFFFFF;
                    background_color = string.Format("{0:x6}", rgb);
                }
                if (!foreground_color.Contains('#'))
                {   //If it doesn't have a # then it is a known named color, needs to be converted to hex
                    int rgb = System.Drawing.Color.FromName(foreground_color.FirstCharToUpper()).ToArgb() & 0xFFFFFF;
                    foreground_color = string.Format("{0:x6}", rgb);
                }
                //The API doesn't like having the #
                foreground_color = foreground_color.Replace("#", "");
                background_color = background_color.Replace("#", "");
                var restClient = new RestClient($"https://webaim.org/resources/contrastchecker/?fcolor={foreground_color}&bcolor={background_color}&api");
                var request = new RestRequest(Method.GET);
                //Will return single color object with parameters we want
                var response = restClient.Execute<ColorContrast>(request).Data;
                var text = string.Empty;
                //See if we can get the inner text so we can identify the element if there was an issue found
                if (color.InnerText != null)
                {
                    text = "\"" + HttpUtility.HtmlDecode(color.InnerText) + "\"\n";
                }
                if(text == string.Empty || text == "\"\"")
                {   //if there was no text just assume it isn't an issue. Will be almost impossible to find it anyway.
                    continue;
                }
                if (response.AA != "pass")
                {   //Add it if it doesn't pass AA standards
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Color Contrast", "", $"{text}Color: {foreground_color}\nBackgroundColor: {background_color}\n{response.ToString()}", "Does not meet AA color contrast", 1, color.XPath));
                    }
                }
            }
        }

        private void ProcessMathJax(DataToParse PageDocument)
        {
            var mathjax_span_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//span[@class=\"MathJax_SVG\"]");
            if (mathjax_span_list == null)
            {
                return;
            }
            foreach (var span in mathjax_span_list)
            {   //// MathJax needs an aria-label to be accessible
                var aria_label = span.Attributes["aria-label"];
                if (aria_label == null)
                {
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "MathJax", "", "MathJax SVG spans need an aria-label", "", 1));
                    }
                }
                else if (aria_label.Value == "Equation")
                {   //// Seems we commonly just have the single word "Equation" for every MathJax element as the aria-label
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location,
                                                "MathJax",
                                                "",
                                                $"Title: \"{span.Attributes["title"]?.Value}\"",
                                                "MathJax SVG needs a descriptive aria-label (usually the text in the title should be moved to the Aria-Label attribute)",
                                                3,
                                                span.XPath));
                    }
                }

            }
        }

        private void ProcessOnclicks(DataToParse PageDocument)
        {   //// Some courses use a ton of inaccessible OnClick elements in place of everything
            var onclick_elements = PageDocument.Doc
                                               .DocumentNode
                                               .SelectNodes("//*[@onclick]");
            if(onclick_elements == null)
            {
                return;
            }
            foreach(var el in onclick_elements)
            {
                if(el.Name == "a")
                {   //// Check links elsewhere, dont need to double check
                    continue;
                }
                lock(Data)
                {
                    Data.Add(new PageA11yData(PageDocument.Location,
                                          "Onclick Element",
                                          "",
                                          el.OuterHtml,
                                          "Onclick attributes are not keyboard accessible",
                                          3,
                                          el.XPath));
                }
            }
        }

        private void ProcessAudioElements(DataToParse PageDocument)
        {   //// Audio Recordings should have transcripts for deaf people
            var audio_elements = PageDocument.Doc
                                            .DocumentNode
                                            .SelectNodes("//a[contains(@class, 'instructure_audio_link')]");
            if(audio_elements == null)
            {
                return;
            }
            foreach(var el in audio_elements)
            {
                if (!VideoParser.CheckTranscript(el))
                {
                    lock (Data)
                    {
                        Data.Add(new PageA11yData(PageDocument.Location, "Inline Audio Recording", "", el.InnerText, "No transcript found", 5, el.XPath));
                    }
                }
            }
        }
    }
}
