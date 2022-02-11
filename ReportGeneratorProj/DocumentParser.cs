namespace ReportGenerators
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using My.StringExtensions;
    using Newtonsoft.Json;
    using System.Text;
    using My.CanvasApi;

    public class DocumentParser : RParserBase
    {
        //Class for Document Parsing
        public DocumentParser() { }

        public override void ProcessContent(Dictionary<string, string> page_info)
        {
            var url = page_info.Keys.ElementAt(0);
            if (page_info[url] == null) { return; }
            if (Options.FilesToIgnore.Contains(url.ToLower())) { return; }
            var PageDocument = new DataToParse(url, page_info[url]);

            ProcessDocuments(PageDocument);
        }

        private void ProcessDocuments(DataToParse PageDocument)
        {
            var document_list = PageDocument.Doc
                .DocumentNode
                .SelectNodes("//a[contains(@href,'/files/')]");
            if (document_list == null)
            {   // List is empty
                return;
            }
            Parallel.ForEach(document_list, doc =>
            {
                string url = doc.Attributes["href"].Value;
                url = Regex.Replace(url, @"^(.*?courses\/\d+\/files\/\d+)(.*?)$", "$1");
                CanvasFile file = CanvasApi.GetFileInformation(url);
                try
                {
                    if (new Regex("Transcript", RegexOptions.IgnoreCase).IsMatch(file.filename))
                    {
                        // Do not add transcripts
                    }
                    else
                    {
                        lock (Data)
                        {
                            Data.Add(new PageData(
                                PageDocument.Location,
                                file.url,
                                "",
                                file.display_name));
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Failed to get File infromation");
                }
            });
        }
    }
}
