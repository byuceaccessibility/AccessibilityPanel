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
    using Newtonsoft.Json;

    public class DocumentParser : RParserBase
    {
        //Class for Document Parsing
        public DocumentParser(string path)
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

            ProcessDocuments(PageDocument);
            TimeRunning.Stop();
            lock (AddTime)
            {
                Time += TimeRunning.Elapsed;
            }
        }
        private string GetDocumentDownload(string path)
        {
            HttpWebRequest req = WebRequest.CreateHttp(path);
            req.Method = "GET";
            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            {
                using (Stream responseStream = res.GetResponseStream())
                {
                    using (StreamReader myStreamReader = new StreamReader(responseStream, Encoding.UTF8))
                    {
                        string responseJSON = myStreamReader.ReadToEnd();
                        dynamic json = Newtonsoft.Json.Linq.JObject.Parse(responseJSON);
                        return json.url;
                    }
                }
            }
        }
        private void ProcessDocuments(DataToParse PageDocument)
        {
            var document_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//a[@api-data-returntype='File']");
            if (document_list == null)
            {   // List is empty
                return;
            }
            Parallel.ForEach(document_list, doc =>
            {
                var fileDownloadPath = GetDocumentDownload(doc.Attributes["api-data-endpoint"].Value));
                // Add Data
            });
        }
    }
}
