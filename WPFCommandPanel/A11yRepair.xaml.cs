using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using Microsoft.Win32;
using My;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using System.Linq;
using System;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using HtmlAgilityPack;
using System.Windows;
using My.DatagridExtensions;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Data;
using TidyManaged;
using My.StringExtentions;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using ReportGenerators;
using System.ComponentModel;
using My.CanvasApi;
using System.Threading.Tasks;
using OfficeOpenXml;

namespace WPFCommandPanel
{
    /// <summary>
    /// Interaction logic for A11yRepair.xaml
    /// </summary>
    public partial class A11yRepair : Page
    {
        /// <summary>
        /// Object to notify the DataGrid of issues that the data source has changed
        /// </summary>
        public CollectionViewSource ViewSource { get; set; }
        /// <summary>
        /// Object to contain all the accessibility issue data
        /// </summary>
        public ObservableCollection<A11yData> data { get; set; }
        /// <summary>
        /// Path of the current report being displayed in the DataGrid
        /// </summary>
        public String curReportFile { get; set; }
        private System.Diagnostics.Stopwatch MoveGridRowTimer = new System.Diagnostics.Stopwatch();
        /// <summary>
        /// Inits the data sources and sets the datagrids itemsource
        /// </summary>
        public A11yRepair()
        {
            InitializeComponent();
            data = new ObservableCollection<A11yData>();
            this.ViewSource = new CollectionViewSource();
            ViewSource.Source = this.data;
            IssueGrid.ItemsSource = ViewSource.View;
            MoveGridRowTimer.Start();
        }
        private CourseInfo course;
        public void SetCourse(CourseInfo new_course)
        {
            course = new_course;
        }
        Boolean isCanvas = false;
        private void LoadCourse(object sender, DoWorkEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                var template = MainWindow.AppWindow.Template;
                var control = (LoadingSpinner)template.FindName("spinner", MainWindow.AppWindow);
                control.Visibility = Visibility.Visible;
            });

            // if its a canvas course
            string CourseName;
            var text = this.Dispatcher.Invoke(() =>
            {
                return directory.Text;
            });
            if (int.TryParse(text, out int course_id))
            {                       
                if(CanvasApi.CurrentDomain == null || CanvasApi.CurrentDomain == "")
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show("No domain chosen.");
                    });
                    return;
                }
                CourseInfo temp_course;
                if(course == null || course.CourseIdOrPath != course_id)
                {
                    temp_course = new CourseInfo(course_id);
                    this.Dispatcher.Invoke(() =>
                    {
                        course = temp_course;
                    });
                }else
                {
                    temp_course = course;
                }                
                
                CourseName = temp_course.CourseCode;
                isCanvas = true;
            }else
            {
                string[] array = text.Split('\\');
                CourseName = array.Take(array.Length - 1).LastOrDefault();
                isCanvas = false;
            }
            
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.InitialDirectory = MainWindow.panelOptions.JsonDataDir;
            openFileDialog.FileName = "*" + CourseName.CleanSplit('-').FirstOrDefault() + "*";
            openFileDialog.Filter = "Json Files|*.json";
            if (openFileDialog.ShowDialog() == true)
            {                
                this.Dispatcher.Invoke(() =>
                {
                    string json = "";
                    using (StreamReader r = new StreamReader(openFileDialog.FileName))
                    {
                        json = r.ReadToEnd();
                    }
                    curReportFile = openFileDialog.FileName;
                    var tempdata = JsonConvert.DeserializeObject<ObservableCollection<A11yData>>(json);
                    data.Clear();
                    for (int i = tempdata.Count - 1; i >= 0; i--)
                    {
                        if (tempdata[i].Location == null || tempdata[i].Location == "")
                        {
                            continue;
                        }
                        data.Add(tempdata[i]);
                    }
                    ViewSource.View.Refresh();
                });                
            }            
        }
        private void LoadCourseFinished(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                var template = MainWindow.AppWindow.Template;
                var control = (LoadingSpinner)template.FindName("spinner", MainWindow.AppWindow);
                control.Visibility = Visibility.Hidden;
            });
        }
        /// <summary>
        /// Keydown event for the course path text box. 
        /// Takes the input HTML directory and finds a matching JSON report for it.
        /// User chooses which report to display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Submit_TextBox(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                BackgroundWorker worker = new BackgroundWorker
                {
                    WorkerReportsProgress = true
                };
                worker.DoWork += LoadCourse;
                worker.RunWorkerCompleted += LoadCourseFinished;
                worker.RunWorkerAsync();
            }
        }

        /// <summary>
        /// Event for search for report button, does the same as pressing enter in the text box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Search_Button(object sender, RoutedEventArgs e)
        {
            BackgroundWorker worker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            worker.DoWork += LoadCourse;
            worker.RunWorkerCompleted += LoadCourseFinished;
            worker.RunWorkerAsync();
        }

        /// <summary>
        /// Contains the file path and an HTML dom object
        /// </summary>
        public class DataToParse
        {   
            public DataToParse(string location)
            {
                Location = location;
                Doc = new HtmlAgilityPack.HtmlDocument();
                Doc.Load(location);
            }
            public DataToParse(string location, HtmlAgilityPack.HtmlDocument doc)
            {
                Location = location;
                Doc = doc;
            }
            public DataToParse(string location, string html)
            {
                Location = location;
                Doc = new HtmlAgilityPack.HtmlDocument();
                Doc.LoadHtml(html);
            }
            public string Location;
            public HtmlAgilityPack.HtmlDocument Doc;
        }
        /// <summary>
        /// Current page being displayed in the browser
        /// </summary>
        private DataToParse curPage;

        /// <summary>
        /// Current node being edited. Should have a red box highlighting it in the browser
        /// </summary>
        private HtmlNode curNode;
        private Dictionary<string, CourseInfo.ItemInfo> curCanvasItem;
        /// <summary>
        /// Finds the current node based on the current selected issue
        /// Uses an XPath selector that was saved when the report was generated
        /// </summary>
        private void SetCurrentNode()
        {
            // Get current selected issue
            A11yData row;
            try
            {
                row = (A11yData)IssueGrid.SelectedItem;
            }catch
            {
                return;
            }
            
            if (row == null || row.Location == null)
            {
                browser.Url = "";
                curPage = null;
                curNode = null;
                editor.Clear();
                return;
            }

            // Get issues HTML file
            if(isCanvas)
            {
                var pageInfo = course.PageInfoList.Where(item => item.Keys.Count(loc => loc.Contains(row.Location.CleanSplit("?").FirstOrDefault())) > 0).FirstOrDefault();
                string question_id = row.Location.CleanSplit("?").LastOrDefault().CleanSplit("&").FirstOrDefault().CleanSplit("=").LastOrDefault();
                if(!row.Location.Contains("question_num"))
                {
                    question_id = "";
                }
                string answer_id = row.Location.CleanSplit("?").LastOrDefault().CleanSplit("&").LastOrDefault().CleanSplit("=").LastOrDefault();
                if(!row.Location.Contains("answer_num"))
                {
                    answer_id = "";
                }
                bool comment = row.Location.Contains("answer_comment");
                var location = pageInfo.Keys.ElementAt(0);
                curCanvasItem = pageInfo;
                curPage = new DataToParse(location, curCanvasItem[location].getContent(question_id,answer_id, comment));
            }else
            {
                var url = (directory.Text + "\\" + row.Location.Split('/').LastOrDefault());
                curPage = new DataToParse(url);
            }

            

            // Find the issue in the page
            // TODO: Simplify switch statement based on new XPath selector
            switch (row.IssueType)
            {
                case "Link":
                    switch (row.DescriptiveError)
                    {
                        case "Non-Descriptive Link":
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode(row.html);
                            break;
                        case "JavaScript Link":
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode(row.html);
                            break;
                        case "Broken Link":
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode(row.html);
                            break;
                        default:
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode("//body");
                            break;
                    }
                    break;
                case "Semantics":
                    switch (row.DescriptiveError)
                    {
                        case "Missing title/label":
                            ;
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode(row.html);
                            break;
                        case "Improper Headings":
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode(row.html);
                            break;
                        case "Bad use of <i> and/or <b>":
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode("//body");
                            break;
                        default:
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode("//body");
                            break;
                    }
                    break;
                case "Image":
                    switch (row.DescriptiveError)
                    {
                        case "No Alt Attribute":
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode(row.html);
                            break;
                        case "Non-Descriptive alt tags":
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode(row.html);
                            break;
                        default:
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode("//body");
                            break;
                    }
                    break;
                case "Media":
                    switch (row.DescriptiveError)
                    {
                        case "Transcript Needed":
                            if (row.Notes.Contains("Video number"))
                            {
                                curNode = curPage.Doc.DocumentNode.SelectSingleNode(row.html);
                            }
                            else if (row.Notes.Contains("BrightCove video with id"))
                            {
                                curNode = curPage.Doc.DocumentNode.SelectSingleNode(row.html);
                            }
                            break;
                        default:
                            curNode = curPage.Doc.DocumentNode.SelectSingleNode("//body");
                            break;
                    }

                    break;
                case "Table":
                    int tableIndex = int.Parse(row.Notes.Split(':')[0].Split(' ').LastOrDefault()) - 1;
                    curNode = curPage.Doc.DocumentNode.SelectNodes("//table")[tableIndex];
                    break;
                case "Misc":
                    curNode = curPage.Doc.DocumentNode.SelectSingleNode("//body");
                    break;
                case "Color":
                    curNode = curPage.Doc.DocumentNode.SelectSingleNode(row.html);
                    break;
                case "Keyboard":
                    curNode = curPage.Doc.DocumentNode.SelectSingleNode(row.html);
                    break;
                default:
                    curNode = curPage.Doc.DocumentNode.SelectSingleNode("//body");
                    break;
            }
            if(curNode == null)
            {
                System.Windows.MessageBox.Show("Issue was not found, report data may be old. You probably want to generate a new report.");
                return;
            }

            // Use a stream as the TidyManaged.Document FromString method does not correctly use encoding.
            MemoryStream str = new MemoryStream(Encoding.UTF8.GetBytes(curNode.OuterHtml));
            using (TidyManaged.Document my_doc = Document.FromStream(str))
            {
                my_doc.ShowWarnings = false;
                my_doc.Quiet = true;
                my_doc.OutputXhtml = true;
                my_doc.OutputXml = true;
                my_doc.IndentBlockElements = AutoBool.Yes;
                my_doc.IndentAttributes = false;
                my_doc.IndentCdata = true;
                my_doc.AddVerticalSpace = false;
                my_doc.WrapAt = 0;
                my_doc.OutputBodyOnly = AutoBool.Yes;
                my_doc.IndentWithTabs = true;
                my_doc.CleanAndRepair();
                editor.Text = my_doc.Save();
                editor.ScrollToHome();
            }

            // Highlight the element
            var style = curNode.GetAttributeValue("style", "");
            if (style == "")
            {
                style = "border: 5px solid red";
            }
            else
            {
                style += "; border: 5px solid red;";
            }
            curNode.Id = "focus_this";
            curNode.SetAttributeValue("style", style);

            // Reload HTML and scroll to the issue
            browser.LoadHtmlAndWait(curPage.Doc.DocumentNode.OuterHtml);
            browser.QueueScriptCall($"var el = document.getElementById('focus_this'); el.scrollIntoView({{behavior: 'smooth' , block: 'center', inline: 'center'}});");
        }

        /// <summary>
        /// SelectedCellsChanged event for DataGrid
        /// Sets the current node to be the new selected cell
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IssueGrid_Selected(object sender, SelectedCellsChangedEventArgs e)
        {
            SetCurrentNode();
        }

        /// <summary>
        /// Save the file.
        /// Right now called with ctr-enter on editor, or clicking the save to file button.
        /// Also reloads the HTML and scrolls to element (but element no longer has red border).
        /// </summary>
        private void SaveFile()
        {
            if (curPage == null)
            {               
                return;
            }
            var newNode = HtmlNode.CreateNode(editor.Text);
            curNode.ParentNode.ReplaceChild(newNode, curNode);
            curNode = newNode;
            A11yData selectedItem = (A11yData)IssueGrid.SelectedItem;
            var index = data.IndexOf(selectedItem);
            data[index].Completed = !data[index].Completed;
            if(isCanvas)
            {
                string location = curCanvasItem.Keys.ElementAt(0);
                int pageInfoIndex = course.PageInfoList.IndexOf(curCanvasItem);
                string question_id = selectedItem.Location.CleanSplit("?").LastOrDefault().CleanSplit("&").FirstOrDefault().CleanSplit("=").LastOrDefault();
                if (!selectedItem.Location.Contains("question_num"))
                {
                    question_id = "";
                }
                string answer_id = selectedItem.Location.CleanSplit("?").LastOrDefault().CleanSplit("&").LastOrDefault().CleanSplit("=").LastOrDefault();
                if (!selectedItem.Location.Contains("answer_num"))
                {
                    answer_id = "";
                }
                bool comment = selectedItem.Location.Contains("answer_comment");
                course.PageInfoList[pageInfoIndex][location] = curCanvasItem[location]
                    .SaveContent(course.CourseIdOrPath, 
                        curPage.Doc.DocumentNode.OuterHtml,
                        question_id,
                        answer_id,
                        comment,
                        out bool saved
                        );
                if(!saved)
                {
                    System.Windows.MessageBox.Show("Failed to save item to canvas");
                }
            }
            else
            {
                curPage.Doc.Save(curPage.Location);
            }
            if (data.Count <= IssueGrid.SelectedIndex + 1)
            {              
            }
            else
            {
                if (MoveGridRowTimer.ElapsedMilliseconds > 300)
                {
                    MoveGridRowTimer.Restart();
                    IssueGrid.SelectedIndex = IssueGrid.SelectedIndex + 1;
                }
            }
            ViewSource.View.Refresh();
            SetCurrentNode();
            //ViewSource.View.Refresh();
            //browser.LoadHtmlAndWait(curPage.Doc.DocumentNode.OuterHtml);
            //browser.QueueScriptCall($"var el = document.getElementById('focus_this'); el.scrollIntoView({{behavior: 'smooth' , block: 'center', inline: 'center'}});");           
        }

        /// <summary>
        /// Function to save the current node, dispalying changes in the browser.
        /// Does not actually save the HTML to the file.
        /// </summary>
        private void SaveNode()
        {
            if (curNode == null)
            {
                return;
            }
            var newNode = HtmlNode.CreateNode(editor.Text);
            var style = newNode.GetAttributeValue("style", "");
            if (style == "")
            {
                style = "border: 5px solid red";
            }
            else
            {
                style += "; border: 5px solid red;";
            }
            newNode.Id = "focus_this";
            newNode.SetAttributeValue("style", style);
            curNode.ParentNode.ReplaceChild(newNode, curNode);
            curNode = newNode;
            browser.LoadHtmlAndWait(curPage.Doc.DocumentNode.OuterHtml);
            browser.QueueScriptCall($"var el = document.getElementById('focus_this'); el.scrollIntoView({{behavior: 'smooth' , block: 'center', inline: 'center'}});");
        }

        
        /// <summary>
        /// Key down event for the editor
        /// ctrl-s will save node (preview)
        /// ctrl-enter will save to file + check the issue as completed
        /// ctrl-NumPad2 move down one issue in datagrid
        /// ctrl-Numpad8 move up one issue in datagrid
        /// TODO: make timer for how fast issues can be moved. Currently moves to fast
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void editor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if(e.Key == Key.S)
                {
                    SaveNode();
                    e.Handled = true;                    
                }
                else if(e.Key == Key.Enter)
                {
                    SaveFile();
                    e.Handled = true;
                }
                else if(e.Key == Key.NumPad2)
                {
                    if(data.Count <= IssueGrid.SelectedIndex + 1)
                    {
                        e.Handled = true;
                    }else
                    {
                        if(MoveGridRowTimer.ElapsedMilliseconds > 300)
                        {
                            MoveGridRowTimer.Restart();
                            IssueGrid.SelectedIndex = IssueGrid.SelectedIndex + 1;
                        }                       
                    }
                }else if(e.Key == Key.NumPad8)
                {
                    if(IssueGrid.SelectedIndex == 0)
                    {
                        e.Handled = true;
                    }else
                    {
                        if (MoveGridRowTimer.ElapsedMilliseconds > 300)
                        {
                            MoveGridRowTimer.Restart();
                            IssueGrid.SelectedIndex = IssueGrid.SelectedIndex - 1;
                        }
                    }
                }
            }
        }        

        /// <summary>
        /// Saves the JSON report. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveReport_Button(object sender, RoutedEventArgs e)
        {         
            if(curReportFile == null)
            {
                e.Handled = true;                
                return;
            }
            using (StreamWriter file = new StreamWriter(System.IO.Path.Combine(curReportFile), false))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(file, data);
            }
        }

        /// <summary>
        /// Save To File button click event. Saves the code changes to the HTML file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveIssue_Button(object sender, RoutedEventArgs e)
        {
            SaveFile();
            e.Handled = true;
        }

        /// <summary>
        /// Saves the changes to the current node and displayes in browser. Does not save to the HTML file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Preview_Button(object sender, RoutedEventArgs e)
        {
            SaveNode();
            e.Handled = true;
        }

        /// <summary>
        /// Resets any changes to the file by reloading it from the HTML file directly.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RefreshNode_Button(object sender, RoutedEventArgs e)
        {
            SetCurrentNode();
        }

        /// <summary>
        /// Images do not properly display. This button will display an image if the current issue is an <img/> tag.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenImage_Button(object sender, RoutedEventArgs e)
        {
            if(curNode?.Name != "img")
            {
                return;
            }            
            var imagePath = Path.Combine(Path.GetDirectoryName(curPage.Location), curNode.GetAttributeValue("src", null));
            if(imagePath == null)
            {
                System.Windows.MessageBox.Show($"Image source seems to be null");
            }
            ImagePopUp.IsOpen = true;
            try
            {
                var bitmap = new BitmapImage(new Uri(imagePath));
                ImagePopUp.Width = bitmap.Width + 6;
                ImagePopUp.Height = bitmap.Height + 6;
                ImagePopUp.DisplayImage.Source = bitmap;
            }
            catch
            {
                System.Windows.MessageBox.Show($"Error opening the image at {imagePath}");
            }
        }

        /// <summary>
        /// Opens the full HTML of the page the current issue is on.
        /// Scrolls the editor to the code for that issue as well.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenHTML_Button(object sender, RoutedEventArgs e)
        {
            if (curPage == null)
            {
                e.Handled = true;
                return;
            }
            editor.WordWrap = false;
            var newNode = HtmlNode.CreateNode(editor.Text);
            var textToMatch = Array.ConvertAll(editor.Text.CleanSplit("\r\n"), s => s.Replace("\t",""));
            curNode.ParentNode.ReplaceChild(newNode, curNode);
            curNode = newNode;
            MemoryStream str = new MemoryStream(Encoding.UTF8.GetBytes(curPage.Doc.DocumentNode.OuterHtml));            
            using (TidyManaged.Document my_doc = Document.FromStream(str))
            {
                my_doc.CharacterEncoding = EncodingType.Utf8;
                my_doc.InputCharacterEncoding = EncodingType.Utf8;
                my_doc.OutputCharacterEncoding = EncodingType.Utf8;
                my_doc.ShowWarnings = false;
                my_doc.Quiet = true;
                my_doc.OutputXhtml = true;
                my_doc.OutputXml = true;
                my_doc.IndentBlockElements = AutoBool.Yes;
                my_doc.IndentAttributes = false;
                my_doc.IndentCdata = true;
                my_doc.AddVerticalSpace = false;                
                my_doc.OutputBodyOnly = AutoBool.Yes;
                my_doc.IndentWithTabs = true;
                my_doc.WrapAt = 0;
                my_doc.CleanAndRepair();              
                editor.Text = "<body>\r\n" + my_doc.Save() + "\r\n</body>";
            }
            str.Close();
            var compareText = editor.Text.CleanSplit("\r\n");
            int lineNum = 0;
            for(int i = 0; i < compareText.Length - textToMatch.Length; i++)
            {
                bool foundMatch = false;
                if(compareText[i].Contains(textToMatch[0]))
                {
                    foundMatch = true;
                    for (int j = 0; j < textToMatch.Length; j++)
                    {
                        if (!compareText[i+j].Contains(textToMatch[j]))
                        {
                            foundMatch = false;
                        }
                    }
                }
                if(foundMatch)
                {
                    lineNum = i;
                    break;
                }
            }
            double vertOffset = (editor.TextArea.TextView.DefaultLineHeight) * lineNum;
            editor.ScrollToVerticalOffset(vertOffset);
            editor.WordWrap = true;
            curNode = curPage.Doc.DocumentNode.SelectSingleNode("//body");
        }

        /// <summary>
        /// Saves the JSON report everytime an issue is marked as compelted or marked as not completed.
        /// TODO: Combine code with SaveReport event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (curReportFile == null)
            {
                e.Handled = true;
                return;
            }
            using (StreamWriter file = new StreamWriter(System.IO.Path.Combine(curReportFile), false))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(file, data);
            }
        }

        private void OpenInBrowser(object sender, RoutedEventArgs e)
        {
            A11yData selectedItem = (A11yData)IssueGrid.SelectedItem;
            System.Diagnostics.Process.Start(selectedItem.url);
        }


        private void SyncToExcel(object sender, DoWorkEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                var template = MainWindow.AppWindow.Template;
                var control = (LoadingSpinner)template.FindName("spinner", MainWindow.AppWindow);
                control.Visibility = Visibility.Visible;
            });
            string excelPath = MainWindow.panelOptions.ReportPath + "\\" + Path.GetFileNameWithoutExtension(curReportFile) + ".xlsx";
            ExcelPackage Excel = new ExcelPackage(new FileInfo(excelPath));
            ExcelRange cells = Excel.Workbook.Worksheets[2].Cells;
            int rowNumber = 4;
            int curDataItem = data.Count() - 1;
            while (cells[rowNumber, 2].Value != null && (String)cells[rowNumber, 2].Value != "")
            {
                if(curDataItem < 0)
                {
                    break;
                }
                cells[rowNumber, 2].Value = data[curDataItem].Completed ? "Complete" : "Not Started";
                curDataItem--;
                rowNumber++;
            }
            Excel.Save();
            Excel.Dispose();
        }
        private void SyncToExcelFinish(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                var template = MainWindow.AppWindow.Template;
                var control = (LoadingSpinner)template.FindName("spinner", MainWindow.AppWindow);
                control.Visibility = Visibility.Hidden;
            });
        }
        private void SyncToExcel(object sender, RoutedEventArgs e)
        {
            BackgroundWorker worker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            worker.DoWork += SyncToExcel;
            worker.RunWorkerCompleted += LoadCourseFinished;
            worker.RunWorkerAsync();
        }
    }
}
