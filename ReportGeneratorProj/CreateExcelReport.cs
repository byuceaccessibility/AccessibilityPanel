namespace ReportGenerators
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using OfficeOpenXml;
    using System.IO;
    using My.StringExtentions;
    using Newtonsoft.Json;
    using System.Reflection;

    public class CreateExcelReport
    {   //Class to take care of creating the report
        public CreateExcelReport(string destination_path)
        {
            string json = "";
            string path = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + @"\options.json";
            using (StreamReader r = new StreamReader(path))
            {
                json = r.ReadToEnd();
            }
            Options = JsonConvert.DeserializeObject<My.PanelOptions>(json);
            PathToExcelTemplate = Options.ExcelTemplatePath;
            //Need to have the destination for the report input at creation. This is the entire path including file name
            this.Destination = destination_path;
            //Create the excel object from the template and set helper variables to be used accross functions
            this.Excel = new ExcelPackage(new FileInfo(PathToExcelTemplate));
            this.Cells = Excel.Workbook.Worksheets[1].Cells;
            this.RowNumber = 4;            
        }
        private My.PanelOptions Options;
        private string Destination;
        private string PathToExcelTemplate;
        private ExcelPackage Excel;
        private ExcelRange Cells;
        private int RowNumber;
        private class JsonDataFormat
        {
            [JsonProperty("Completed?")]
            public bool Completed { get; set; }
            public string Location { get; set; }
            [JsonProperty("Issue Type")]
            public string IssueType { get; set; }
            [JsonProperty("Descriptive Error")]
            public string DescriptiveError { get; set; }
            public string Notes { get; set; }
            public string html { get; set; }
            public string Url { get; set; }
        }
        public void CreateReport(List<PageData> A11yData, List<PageData> MediaData, List<PageData> LinkData)
        {   //Public method to create the report from any input (can put null in place of any of the lists if you only need a certain input).
            if (null != A11yData)
            {
                AddA11yData(A11yData);
            }
            if (null != MediaData)
            {
                AddMediaData(MediaData);
            }
            if (null != LinkData)
            {
                AddLinkData(LinkData);
            }
            //Need to make sure the destination directory exists
            var test_path = new DirectoryInfo(Path.GetDirectoryName(Destination));
            if (!(test_path.Exists))
            {
                test_path.Create();
            }
            //Get a dynamicly named report just in case one of the same name already exists
            var i = 1;
            while (new FileInfo(Destination).Exists)
            {
                var new_destination = Destination.Replace(".xlsx", $"_V{i}.xlsx");
                if (!(new FileInfo(new_destination).Exists))
                {
                    Destination = new_destination;
                }
                i++;
            }
            List<JsonDataFormat> json = new List<JsonDataFormat>();
            var numIssues = 0;
            var row = 4;
            string dataDir = Options.JsonDataDir;
            Cells = Excel.Workbook.Worksheets[1].Cells;
            while (Cells[row, 2].Value != null && (String)Cells[row, 2].Value != "")
            {                
                var data = new JsonDataFormat
                {
                    Completed = (String)Cells[row, 2].Value == "Not Started" ? false : true,
                    Location = (String)Cells[row, 3].Value,
                    Url = Cells[row, 3]?.Hyperlink?.ToString(),
                    IssueType = (String)Cells[row, 4].Value,
                    DescriptiveError = (String)Cells[row, 5].Value,
                    Notes = (String)Cells[row, 6].Value,
                    html = (String)Cells[row, 10].Value
                };
                json.Add(data);
                numIssues++;
                row++;
            }
            using (StreamWriter file = new StreamWriter(System.IO.Path.Combine(dataDir, System.IO.Path.GetFileName(Destination).Replace(".xlsx", ".json")), false))
            {
                JsonSerializer serializer = new JsonSerializer
                {
                    Formatting = Formatting.Indented
                };
                serializer.Serialize(file, json);
            }
            //Save and dispose
            Excel.SaveAs(new FileInfo(Destination));
            Excel.Dispose();
        }
        private void AddA11yData(List<PageData> data_list)
        {   //This is the most complicated one to add to the excel document and so uses the helper function
            //All of the string inputs are from the excel document validation and should possibly be made dynamic as if they don't match it will throw an error.
            //data_list = data_list.Select(d => d as PageA11yData).Distinct().ToList<PageData>();
            RowNumber = 4;
            Cells = Excel.Workbook.Worksheets[1].Cells;
            foreach (var data in data_list)
            {
                Cells[RowNumber, 2].Value = "Not Started";
                Cells[RowNumber, 3].Value = data.Location.CleanSplit("/").LastOrDefault().CleanSplit("\\").LastOrDefault();
                Cells[RowNumber, 3].Hyperlink = new System.Uri(Regex.Replace(data.Location, "api/v\\d/", ""));        
                switch ((data as PageA11yData).Issue.ToLower())
                {
                    case "adjust link text":
                        A11yAddToCell("Link", "Non-Descriptive Link", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "javascript links are not accessible":
                        A11yAddToCell("Link", "JavaScript Link", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "broken link":
                        A11yAddToCell("Link", "Broken Link", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "empty link tag":
                        A11yAddToCell("Link", "Broken Link", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "check document accessibility":
                        A11yAddToCell("Link", "Check Document Accessibility", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "needs a title":
                        A11yAddToCell("Semantics", "Missing title/label", $"{data.Element} needs a title attribute\nID: {data.Id}", html: (data as PageA11yData).html);
                        break;
                    case "no alt attribute":
                        A11yAddToCell("Image", "No Alt Attribute", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "alt text may need adjustment":
                        A11yAddToCell("Image", "Non-descriptive alt tags", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "alt text may need adjustment or no alt attribute":
                        A11yAddToCell("Image", "Non-descriptive alt tags", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "ordered list":
                        A11yAddToCell("Semantics", "Non-native HTML tags", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "check if header is meant to be invisible and is not a duplicate":
                        A11yAddToCell("Semantics", "Improper Headings", $"Invisible header:\n{data.Text}", html: (data as PageA11yData).html);
                        break;
                    case "no transcript found":
                        A11yAddToCell("Media", "Transcript Needed", data.Text, 5, 5, 5, html: (data as PageA11yData).html);
                        break;
                    case "table contains streched cells":
                        A11yAddToCell("Table", "Contains stretched cells", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "missing headers":
                        A11yAddToCell("Table", "Missing headers", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "scope attributes missing/misused":
                        A11yAddToCell("Table", "Scope attributes missing/misused", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "empty table":
                        A11yAddToCell("Table", "Empty Table", data.Text, html: (data as PageA11yData).html);
                        break;
                    case "complex table":
                        A11yAddToCell("Table", "Complex Table", data.Text, html: (data as PageA11yData).html);
                        break;
                    //case "revise table":
                    //    A11yAddToCell("Table", "", data.Text, html: (data as PageA11yData).html);
                    //    break;
                    case "<i>/<b> tags should be <em>/<strong> tags":
                        A11yAddToCell("Semantics", "Bad use of <i> and/or <b>", (data as PageA11yData).Issue, html: (data as PageA11yData).html);
                        break;
                    case "flash is inaccessible":
                        A11yAddToCell("Misc", "", $"{data.Text}\n{(data as PageA11yData).Issue}", 5, 5, 5, html: (data as PageA11yData).html);
                        break;
                    case "does not meet aa color contrast":
                        A11yAddToCell("Color", "Doesn't meet contrast ratio", $"{(data as PageA11yData).Issue}\n{data.Text}", html: (data as PageA11yData).html);
                        break;
                    case "onclick attributes are not keyboard accessible":
                        A11yAddToCell("Keyboard", "Page/Element not navigable", $"{(data as PageA11yData).Issue}\n{data.Text}", html: (data as PageA11yData).html);
                        break;
                    default:
                        A11yAddToCell("", "", (data.Element + "\n" + data.Text + "\n" + (data as PageA11yData).Issue), html: (data as PageA11yData).html);
                        break;
                }
                RowNumber++;
            }
        }
        private void A11yAddToCell(string issue_type, string descriptive_error, string notes, int severity = 1, int occurence = 1, int detection = 1, string html = "")
        {   //Helper function to insert data
            Cells[RowNumber, 4].Value = issue_type;
            Cells[RowNumber, 5].Value = descriptive_error;
            Cells[RowNumber, 6].Value = notes;
            Cells[RowNumber, 7].Value = severity;
            Cells[RowNumber, 8].Value = occurence;
            Cells[RowNumber, 9].Value = detection;
            Cells[RowNumber, 10].Value = html;
        }
        private void AddMediaData(List<PageData> data_list)
        {
            //Insert all of the media data
            RowNumber = 4;
            Cells = Excel.Workbook.Worksheets[2].Cells;
            Excel.Workbook.Worksheets[2].Column(4).Style.Numberformat.Format = "#############";
            //Excel.Workbook.Worksheets[2].Column(6).Style.Numberformat.Format = "hh:mm:ss";
            //Excel.Workbook.Worksheets[2].Column(11).Style.Numberformat.Format = "hh:mm:ss";
            //Excel.Workbook.Worksheets[2].Column(12).Style.Numberformat.Format = "hh:mm:ss";
            
            foreach (var data in data_list)
            {
                Cells[RowNumber, 2].Value = data.Element;
                Cells[RowNumber, 3].Value = data.Location.CleanSplit("/").LastOrDefault().CleanSplit("\\").LastOrDefault();
                Cells[RowNumber, 3].Hyperlink = new System.Uri(Regex.Replace(data.Location, "api/v\\d/", ""));
                if ((from cell in Cells["D:D"] where cell.Value?.ToString() == data.Id select true).Count(c => c == true) > 0
                    && data.Id != ""
                    && data.Id != null)
                {   //Need to check for duplicate videos so they can be marked and not have the time double up in the total
                    Cells[RowNumber, 4].Value = "Duplicate Video:\n" + data.Id;
                }
                else
                {
                    Cells[RowNumber, 4].Value = data.Id;
                }
                Cells[RowNumber, 5].Value = Regex.Replace((data as PageMediaData).MediaUrl, "^//", "https://");
                try
                {
                    Cells[RowNumber, 5].Hyperlink = new System.Uri(Regex.Replace((data as PageMediaData).MediaUrl, "^//", "https://"));
                }
                catch
                {
                    //do nothing
                }
                Cells[RowNumber, 6].Value = (data as PageMediaData).VideoLength;
                Cells[RowNumber, 7].Value = data.Text;
                Cells[RowNumber, 8].Value = (data as PageMediaData).Transcript ? "Yes" : "No";
                Cells[RowNumber, 9].Value = (data as PageMediaData).CC ? "Yes" : "No";
                RowNumber++;
            }

        }

        private void AddLinkData(List<PageData> data_list)
        {   //Add all of the link data
            Cells = Excel.Workbook.Worksheets[3].Cells;
            RowNumber = 4;
            foreach (var data in data_list)
            {
                Cells[RowNumber, 2].Value = data.Location.CleanSplit("/").LastOrDefault().CleanSplit("\\").LastOrDefault();
                Cells[RowNumber, 2].Hyperlink = new System.Uri(Regex.Replace(data.Location, "api/v\\d/", ""));
                Cells[RowNumber, 3].Value = data.Element;
                if (data.Element.Contains("http"))
                {   //If it is a link make it a hyperlink so it can be clicked easier.
                    try
                    {
                        Cells[RowNumber, 3].Hyperlink = new System.Uri(data.Element);
                    }
                    catch
                    {
                        //Just don't do anything since the link is not a valid url format
                    }
                }
                Cells[RowNumber, 4].Value = data.Text;
                RowNumber++;
            }
        }
    }
}
