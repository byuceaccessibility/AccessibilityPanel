namespace ReportGenerators
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using My.StringExtentions;

    public class LinkParser : RParserBase
    {
        //class to do a link report
        public LinkParser(string path)
        {
            Directory = path;
        }
        private string Directory = string.Empty;
        public TimeSpan Time = new TimeSpan(0);
        private object AddTime = new object();
        public override void ProcessContent(Dictionary<string, string> page_info)
        {
            System.Diagnostics.Stopwatch TimeRunning = new System.Diagnostics.Stopwatch();
            TimeRunning.Start();
            if (page_info[page_info.Keys.ElementAt(0)] == null)
            {
                return;
            }
            var PageDocument = new DataToParse(page_info.Keys.ElementAt(0), page_info[page_info.Keys.ElementAt(0)]);

            ProcessLinks(PageDocument);
            ProcesImages(PageDocument);
            TimeRunning.Stop();
            lock (AddTime)
            {
                Time += TimeRunning.Elapsed;
            }

        }
        private bool TestUrl(string url)
        {   //Test if URL is working by pinging it and seeing the return status
            HttpWebRequest request;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(url);
            }
            catch
            {
                return false;
            }

            request.Method = "HEAD";
            request.Proxy = null;
            request.UseDefaultCredentials = true;
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (WebException)
            {
                return false;
            }
        }
        private bool TestPath(string path)
        {
            if (Directory == "None")
            {
                //Then we don't have a directory to compare against
                return true;
            }
            //Need to remove any HTML page location part of the file path as that will cause the test to fail.
            path = path.CleanSplit("#").FirstOrDefault();
            try
            {
                if (new Regex("^\\.\\.").IsMatch(path))
                {
                    path = Path.GetFullPath(Path.Combine(Directory, path));
                }
                else
                {
                    path = Path.GetFullPath(Path.Combine(Directory, path));
                }
            }
            catch
            {
                return false;
            }


            return File.Exists(path);
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
            Parallel.ForEach(link_list, link =>
            {   //Run it in parralell as this takes forever otherwise. Still somewhat slow when running into a bunch of links
                if (link.Attributes["href"] != null)
                {
                    if (new Regex("^#").IsMatch(link.Attributes["href"].Value))
                    {
                        //Do nothing
                    }
                    else if (new Regex("^mailto:", RegexOptions.IgnoreCase).IsMatch(link.Attributes["href"].Value))
                    {
                        //Do nothing
                    }
                    else if (new Regex("^javascript:", RegexOptions.IgnoreCase).IsMatch(link.Attributes["href"].Value))
                    {
                        lock (Data)
                        {
                            Data.Add(new PageData(PageDocument.Location,
                                                    link.Attributes["href"].Value,
                                                    "",
                                                    "JavaScript links are often not accessible \\ broken."));
                        }

                    }
                    else if (new Regex("http|^www\\.|.*?\\.com$|.*?\\.org$", RegexOptions.IgnoreCase).IsMatch(link.Attributes["href"].Value))
                    {
                        if (!TestUrl(link.Attributes["href"].Value))
                        {
                            lock (Data)
                            {
                                Data.Add(new PageData(PageDocument.Location,
                                                        link.Attributes["href"].Value,
                                                        "",
                                                        "Broken link, needs to be checked"));
                            }
                        }
                    }
                    else
                    {
                        if (!TestPath(link.Attributes["href"].Value))
                        {
                            lock (Data)
                            {
                                Data.Add(new PageData(PageDocument.Location,
                                                        link.Attributes["href"].Value,
                                                        "",
                                                        "File does not exist"));
                            }
                        }
                    }
                }
            });
        }
        private void ProcesImages(DataToParse PageDocument)
        {
            var image_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//img");
            if (image_list == null)
            {
                return;
            }
            Parallel.ForEach(image_list, image =>
            {   //This is pretty fast on its own, but why not run it parallel
                if (image.Attributes["src"] != null)
                {
                    if (new Regex("http|^www\\.|.*?\\.com$|.*?\\.org$", RegexOptions.IgnoreCase).IsMatch(image.Attributes["src"].Value))
                    {

                    }
                    else
                    {
                        if (!TestPath(image.Attributes["src"].Value))
                        {

                            lock (Data)
                            {
                                Data.Add(new PageData(PageDocument.Location,
                                                    image.Attributes["src"].Value,
                                                    "",
                                                    "Image does not exist"));
                            }

                        }
                    }
                }
            });
        }
    }
}
