using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using My.CanvasApi;
using Newtonsoft.Json;
using System.Reflection;

namespace ReportGenerators
{
    public class CourseInfo
    {
        //Class that will contain all of the courses info, including the URL and HTML body for each item
        //This CourseInfo class will also use the CanvasAPI static class in order to fill it with the course information upon construction (this is the longest time consuming part of the program)
        //Can't really make it multithreaded as the CanvasAPI has an access limit that would be used up to quickly if we run multiple requests at the same time.
        //Should maybe make the fill object a mehtod instead of just inside the constructor.
        public class ItemInfo
        {
            public string type { get; set; }
            public CanvasPage page { get; set; }
            public CanvasDiscussionTopic discussion { get; set; }
            public CanvasAssignment assignment { get; set; }
            public CanvasQuiz quiz { get; set; }
            public List<CanvasQuizQuesiton> quiz_questions { get; set; }
            public String getContent(string question_id, string answer_id, bool comment)
            {
                switch (type)
                {
                    case "Page":
                        return page.body;
                    case "Discussion":
                        return discussion.message;
                    case "Assignment":
                        return assignment.description;
                    case "Quiz":
                        if(question_id == "")
                        {
                            return quiz.description;
                        }
                        var q = quiz_questions[int.Parse(question_id)];
                        if(answer_id == "")
                        {
                            return q.question_text;
                        }
                        if(comment)
                        {
                            return q.answers[int.Parse(answer_id)].answer_comments;
                        }else
                        {
                            return q.answers[int.Parse(answer_id)].html;
                        }
                    default:
                        return "";
                }
            }

            public ItemInfo SaveContent(int course_id, string new_html, string question_id, string answer_id, bool comment)
            {
                switch (type)
                {
                    case "Page":
                        CanvasPage new_page = CanvasApi.PostNewPageContent(course_id, page.url, new_html);
                        page = new_page;
                        break;
                    case "Discussion":
                        CanvasDiscussionTopic new_topic = CanvasApi.PostNewDiscussionMessage(course_id, discussion.id, new_html);
                        discussion = new_topic;
                        break;
                    case "Assignment":
                        CanvasAssignment new_assignment = CanvasApi.PostNewAssignmentDescription(course_id, assignment.id, new_html);
                        assignment = new_assignment;
                        break;
                    case "Quiz":
                        if (question_id == "")
                        {
                            CanvasQuiz new_quiz = CanvasApi.PostNewQuizDescription(course_id, quiz.id, new_html);
                            quiz = new_quiz;
                            break;
                        }                        
                        var q = quiz_questions[int.Parse(question_id)];
                        if (answer_id == "")
                        {
                            CanvasQuizQuesiton new_q = CanvasApi.PostNewQuizQuestionText(course_id, quiz.id, q.id, new_html);
                            q = new_q;
                            break;
                        }
                        if (comment)
                        {
                            q.answers[int.Parse(answer_id)].answer_comments = new_html;
                            CanvasQuizQuesiton new_q = CanvasApi.PostNewQuizQuestionAnswerComment(course_id, quiz.id, q.id, q.answers);
                            quiz_questions[int.Parse(question_id)] = new_q;
                        }
                        else
                        {
                            q.answers[int.Parse(answer_id)].html = new_html;
                            CanvasQuizQuesiton new_q = CanvasApi.PostNewQuizQuestionAnswer(course_id, quiz.id, q.id, q.answers);
                            quiz_questions[int.Parse(question_id)] = new_q;
                        }
                        break;
                    default:
                        break;
                }
                return this;
            }
        }
        public CourseInfo(string course_path)
        {
            //Constructor for if a directory path is input
            this.CourseIdOrPath = course_path;
            string[] array = course_path.Split('\\');
            this.CourseName = array.Take(array.Length - 1).LastOrDefault();
            this.CourseCode = array.Take(array.Length - 1).LastOrDefault();
            string json = "";
            string path = Assembly.GetEntryAssembly().Location.Contains("source") ? @"C:\Users\jwilli48\Desktop\AccessibilityTools\A11yPanel\options.json" :
                                System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + @"\options.json";
            using (StreamReader r = new StreamReader(path))
            {
                json = r.ReadToEnd();
            }
            My.PanelOptions Options = JsonConvert.DeserializeObject<My.PanelOptions>(json);
            PageHtmlList = new List<Dictionary<string, string>>();
            Parallel.ForEach(Directory.GetFiles(course_path, "*.html", SearchOption.TopDirectoryOnly), file =>
            {
               // Console.WriteLine(file.CleanSplit("\\").LastOrDefault());
                string location = string.Empty;
                switch (Path.GetPathRoot(file))
                {
                    case "I:\\":
                        location = $"{Options.IDriveContentUrl}/{file.Replace("I:\\", "")}";
                        break;
                    case "Q:\\":
                        location = $"{Options.QDriveContentUrl}/{file.Replace("Q:\\", "")}";
                        break;
                    default:
                        location = $"file:///{file}";
                        break;
                }
                var temp_dict = new Dictionary<string, string>
                {
                    [location] = File.ReadAllText(file)
                };
                lock (PageHtmlList)
                {
                    PageHtmlList.Add(temp_dict);
                }
            });
        }
             
        public CourseInfo(int course_id)
        {
            //Constructor for a canvas course ID number
            this.CourseIdOrPath = course_id;
            //Get the course information with API
            CanvasCourse course_info = CanvasApi.GetCanvasCourse(course_id);
            CourseName = course_info.name;
            CourseCode = course_info.course_code;
            //Need to make sure the HtmlList is initialized so we can store all of the info
            PageHtmlList = new List<Dictionary<string, string>>();
            PageInfoList = new List<Dictionary<string, ItemInfo>>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = -1 };
            //Begin to loop through all modules of the course
            Parallel.ForEach(CanvasApi.GetCanvasModules(course_id), options, module =>
            {
               //Console.WriteLine(module.name);
                //Loop through all the items for each module
                Parallel.ForEach(CanvasApi.GetCanvasModuleItems(course_id, module.id), options, item =>
                {
                    wait_for_rate_limit:
                    //The object to connect the item location and its HTML body
                    Dictionary<string, string> LocationAndBody = new Dictionary<string, string>();
                    Dictionary<string, ItemInfo> PageInfoItem = new Dictionary<string, ItemInfo>();
                    if(item.url == null)
                    {
                        return;
                    }
                    PageInfoItem[item.url] = new ItemInfo();
                    PageInfoItem[item.url].type = item.type;
                    //Console.WriteLine(item.title);
                    try //This try block is just in case we are not authroized to access any of these pages
                    {
                        switch (item.type)
                        { //Need to see what type of item it is to determine request needed
                            case "Page":
                                CanvasPage page = CanvasApi.GetCanvasPage(course_id, item.page_url);
                                LocationAndBody[item.url] = page.body;
                                PageInfoItem[item.url].page = page;
                                break;
                            case "Discussion":
                                CanvasDiscussionTopic discussion = CanvasApi.GetCanvasDiscussionTopics(course_id, item.content_id);
                                LocationAndBody[item.url] = discussion.message;
                                PageInfoItem[item.url].discussion = discussion;
                            break;
                            case "Assignment":
                                CanvasAssignment assignment = CanvasApi.GetCanvasAssignments(course_id, item.content_id);
                                LocationAndBody[item.url] = assignment.description;
                                PageInfoItem[item.url].assignment = assignment;
                            break;
                            case "Quiz":
                                CanvasQuiz quiz = CanvasApi.GetCanvasQuizzes(course_id, item.content_id);
                                LocationAndBody[item.url] = quiz.description;
                                PageInfoItem[item.url].quiz = quiz;
                                PageInfoItem[item.url].quiz_questions = new List<CanvasQuizQuesiton>();
                                try
                                { //Quizes require more as we need to gather question and answer info
                                    //Again may be able to see basic quiz but not authorized for quiz questions, this the try block.
                                    //Loop through all questions for specific quiz
                                    int question_number = 0;
                                    foreach (CanvasQuizQuesiton question in CanvasApi.GetCanvasQuizQuesitons(course_id, item.content_id))
                                    {
                                        PageInfoItem[item.url].quiz_questions.Add(question);
                                        Dictionary<string, string> LAndB = new Dictionary<string, string>();
                                        LAndB[item.url+$"?question_num={question_number}"] = question.question_text; 
                                        lock(PageHtmlList)
                                        {
                                            PageHtmlList.Add(LAndB);
                                        }
                                        int answer_num = 0;
                                        int answer_comment_num = 0;
                                        //Loop through all answers in the quiz
                                        foreach (CanvasQuizQuestionAnswers answer in question.answers)
                                        {
                                            Dictionary<string, string> answerDict = new Dictionary<string, string>();
                                            answerDict[item.url + $"?question_num={question_number}&answer_num={answer_num}"] = "\n" + answer?.html;
                                            lock (PageHtmlList)
                                            {
                                                PageHtmlList.Add(answerDict);
                                            }
                                            Dictionary<string, string> commentDict = new Dictionary<string, string>();
                                            commentDict[item.url + $"?question_num={question_number}&answer_comment={answer_comment_num}"] = "\n" + answer?.comments_html;
                                            lock (PageHtmlList)
                                            {
                                                PageHtmlList.Add(commentDict);
                                            }
                                            answer_comment_num++;
                                            answer_num++;
                                        }
                                        question_number++;
                                    }
                                }
                                catch (Exception e)
                                {
                                    //Check if the exception was an unauthorized request
                                    if (e.Message.Contains("Unauthorized"))
                                    {
                                        //Console.WriteLine("ERROR: (401) Unauthorized, can not search quiz questions. Skipping...");
                                    }
                                    else
                                    {
                                        //Console.WriteLine("{0}", e);
                                    }
                                }
                            break;
                            default:
                                //Console.WriteLine($"Not Supported:\n{item.type}");
                                LocationAndBody["Empty"] = null;
                                break;
                        }
                        //Add the location and HTML body to the List
                        lock (PageHtmlList)
                        {
                            PageHtmlList.Add(LocationAndBody);
                        }
                        lock (PageInfoList)
                        {
                            PageInfoList.Add(PageInfoItem);
                        }
                    }
                    catch (Exception e)
                    {
                        //Check if it was unauthorized
                        if (e.Message.Contains("Unauthorized"))
                        {
                            //Console.WriteLine($"ERROR: (401) Unauthorized, can not search:\n{item.title}\n{item.type}");
                        }
                        else if (e.Message.Contains("403"))
                        {
                            Console.WriteLine($"ERROR: (403) Forbidden (Rate Limit Exceedd)");
                            goto wait_for_rate_limit;
                        }
                        else
                        {
                            //Console.WriteLine("{0}", e);
                        }
                    }
               });
           });
        }
        public dynamic CourseIdOrPath { get; }
        public string CourseName { get; }
        public string CourseCode { get; }
        public List<Dictionary<string, string>> PageHtmlList { get; set; }        
        public List<Dictionary<string, ItemInfo>> PageInfoList { get; set; }
    }
    public class PageData : IEquatable<PageData>
    {
        //Base clas for holding issues / data from a single page
        public PageData(string input_location, string input_element, string input_id, string input_text)
        {
            this.Location = input_location;
            this.Element = input_element;
            this.Id = input_id;
            this.Text = input_text;
        }
        public string Location { get; }
        public string Element { get; }
        public string Id { get; }
        public string Text { get; }
        public override string ToString()
        {
            var props = typeof(PageData).GetProperties();
            var sb = new StringBuilder();
            foreach (var p in props)
            {
                sb.AppendLine(p.Name + ": " + p.GetValue(this, null));
            }
            return sb.ToString();
        }

        public bool Equals(PageData other)
        {
            if (Object.ReferenceEquals(other, null)) return false;
            if (Object.ReferenceEquals(this, other)) return true;

            return ToString() == other.ToString();
        }

        public override int GetHashCode()
        {
            int hashToString = ToString() == null ? 0 : ToString().GetHashCode();
            return hashToString;
        }
    }

    public class PageA11yData : PageData , IEquatable<PageA11yData>
    {
        //Exxtension of class for accessibility params desired
        public PageA11yData(string location, string element, string id, string text, string issue, int severity, string html = "") : base(location, element, id, text)
        {
            this.Issue = issue;
            this.Severity = severity;
            this.html = html;
        }
        public string Issue { get; }
        public int Severity { get; }
        public string html { get; }
        public override string ToString()
        {
            var props = typeof(PageA11yData).GetProperties();
            var sb = new StringBuilder();
            foreach (var p in props)
            {
                sb.AppendLine(p.Name + ": " + p.GetValue(this, null));
            }
            return sb.ToString();
        }

        public bool Equals(PageA11yData other)
        {
            if (Object.ReferenceEquals(other, null)) return false;
            if (Object.ReferenceEquals(this, other)) return true;

            return ToString() == other.ToString();
        }

        public override int GetHashCode()
        {
            int hashToString = ToString() == null ? 0 : ToString().GetHashCode();
            return hashToString;
        }
    }
    public class PageMediaData : PageData , IEquatable<PageMediaData>
    {
        //Extension of class for Media data from a page
        public PageMediaData(string location, string element, string id, string text, string media_url, TimeSpan video_length, bool transcript, bool cc = false) : base(location, element, id, text)
        {
            MediaUrl = media_url;
            VideoLength = video_length;
            Transcript = transcript;
            CC = cc;
        }
        public string MediaUrl { get; }
        public TimeSpan VideoLength { get; }
        public bool Transcript { get; }
        public bool CC { get; }
        public override string ToString()
        {
            var props = typeof(PageMediaData).GetProperties();
            var sb = new StringBuilder();
            foreach (var p in props)
            {
                sb.AppendLine(p.Name + ": " + p.GetValue(this, null));
            }
            return sb.ToString();
        }

        public bool Equals(PageMediaData other)
        {
            if (Object.ReferenceEquals(other, null)) return false;
            if (Object.ReferenceEquals(this, other)) return true;

            return ToString() == other.ToString();
        }

        public override int GetHashCode()
        {
            int hashToString = ToString() == null ? 0 : ToString().GetHashCode();
            return hashToString;
        }
    }
}
