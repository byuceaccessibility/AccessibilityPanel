using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.IO;
using System.ComponentModel;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Management.Automation;
using My.SeleniumExtentions;
using My.StringExtentions;
using System.Text.RegularExpressions;
using OpenQA.Selenium.Firefox;

namespace WPFCommandPanel
{
    public partial class CommandPanel : Page
    {
        public void OpenBrowserButton(object sender, EventArgs e)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += OpenBrowser;
            worker.RunWorkerAsync();

        }
        private void OpenBrowser(object sender, DoWorkEventArgs e)
        {
            var manager = new FirefoxProfileManager();
            var ffProfile = manager.GetProfile("default");
            var fds = FirefoxDriverService.CreateDefaultService(MainWindow.panelOptions.FirefoxDriverPath);
            fds.HideCommandPromptWindow = true;
            var options = new FirefoxOptions();
            options.Profile = ffProfile;
            chrome = new FirefoxDriver(fds, options, new TimeSpan(0,2,0));
            wait = new WebDriverWait(chrome, new TimeSpan(0, 0, 5));
        }

        private void Canvas_Click(object sender, EventArgs e)
        {
            chrome.Url = MainWindow.panelOptions.BYUOnlineCreds["BaseUri"];
        }

        private void MasterCanvas_Click(object sender, EventArgs e)
        {
            chrome.Url = MainWindow.panelOptions.BYUMasterCoursesCreds["BaseUri"];
        }

        private void TestCanvas_Click(object sender, EventArgs e)
        {
            chrome.Url = MainWindow.panelOptions.BYUISTestCreds["BaseUri"];
        }
        private void QuitProcess(object sender, EventArgs e)
        {
            QuitThread = true;
        }
        private void GoToAccessibility_Course(object sender, EventArgs e)
        {
            if (chrome == null)
            {
                //Open the browser to be controlled
                var manager = new FirefoxProfileManager();
                var ffProfile = manager.GetProfile("default");
                var fds = FirefoxDriverService.CreateDefaultService(MainWindow.panelOptions.FirefoxDriverPath);
                fds.HideCommandPromptWindow = true;
                var options = new FirefoxOptions();
                options.Profile = ffProfile;
                chrome = new FirefoxDriver(fds, options);
                wait = new WebDriverWait(chrome, new TimeSpan(0, 0, 10));
            }
            chrome.Url = "https://byu.instructure.com/courses/1026";
        }
        private void Ralt_Click(object sender, EventArgs e)
        {   //Runs the ralt function reworked into c#. Will run either the buzz or canvas ones, if not on eithe rof those pages then fails.
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;

            if (chrome?.Url?.Contains("instruct") == true)
            {
                worker.DoWork += CanvasRalt;
            }
            else if (chrome?.Url?.Contains("agilix") == true)
            {
                worker.DoWork += BuzzRalt;
            }
            else
            {
                MessageBox.Show("Not on a valid page");
                return;
            }
            worker.RunWorkerAsync();
        }
        public class PoshHelper
        {   //Helper object for if I run into the problem again of a popup freezing everything
            public PoshHelper(ChromeDriver d, int s)
            {
                driver = d;
                i = s;
            }
            public ChromeDriver driver;
            public int i;
        }
        public class StoreWebElement
        {
            public string text { get; set; }
            public List<string> class_list { get; set; }
            public string id { get; set; }
            public override string ToString()
            {
                string s;
                s = $"Id: {id}\n";
                s += $"Text: {text}\n";
                s += "Class list: ";
                if (class_list == null)
                {
                    return s;
                }
                foreach (string i in class_list)
                {
                    s += i + " ";
                }
                return s;
            }
        }
        
        private void CanvasRalt(object sender, DoWorkEventArgs e)
        {
            var s = new System.Diagnostics.Stopwatch();
            s.Start();
            var number_of_modules = chrome.FindElementsByClassName("context_module_item").Count(c => c.Text != "");
            var home_page_url = chrome.Url;
            wait.Timeout = new TimeSpan(0, 0, 10);
            Dispatcher.Invoke(() =>
            {
                Run run = new Run($"RALT estimated time (10 seconds per page, {number_of_modules} pages) : {TimeSpan.FromSeconds(number_of_modules * 10).ToString(@"hh\:mm\:ss")}\n")
                {
                    Foreground = System.Windows.Media.Brushes.White
                };
                TerminalOutput.Inlines.Add(run);
            });
            Dispatcher.Invoke(() =>
            {
                Run run = new Run("0 / {number_of_modules} . . .")
                {
                    Foreground = System.Windows.Media.Brushes.DarkGoldenrod
                };
                TerminalOutput.Inlines.Add(run);
            });
            StoreWebElement store = new StoreWebElement();
            for (int i = 0; i < number_of_modules; i++)
            {
                if(i != 0 && (i % 10) == 0)
                {
                    chrome.Quit();
                    var manager = new FirefoxProfileManager();
                    var ffProfile = manager.GetProfile("default");
                    var fds = FirefoxDriverService.CreateDefaultService(MainWindow.panelOptions.FirefoxDriverPath);
                    fds.HideCommandPromptWindow = true;
                    var options = new FirefoxOptions();
                    options.Profile = ffProfile;
                    chrome = new FirefoxDriver(fds, options, new TimeSpan(0, 2, 0));
                    wait = new WebDriverWait(chrome, new TimeSpan(0, 0, 10));
                    chrome.Url = home_page_url;
                    LoginToByu();
                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                    sw.Start();
                    while(true)
                    {
                        if (sw.ElapsedMilliseconds > 6000)
                            break;
                    }
                }
                Dispatcher.Invoke(() =>
                {
                    TerminalOutput.Inlines.Remove(TerminalOutput.Inlines.LastInline);
                    Run run = new Run($"{i} / {number_of_modules} . . .\n")
                    {
                        Foreground = System.Windows.Media.Brushes.DarkGoldenrod
                    };
                    TerminalOutput.Inlines.Add(run);
                });
                try
                {
                    store = new StoreWebElement();
                    var cur_item = wait.Until(c => c.FindElements(By.CssSelector("li[class*=\"context_module_item\"]")))[i];
                    store.id = cur_item.GetAttribute("id");
                    store.text = cur_item.Text;
                    var class_list = cur_item.GetAttribute("class").CleanSplit(" ").Select(c => Regex.Replace(c, @"\s+", ""));
                    store.class_list = class_list.ToList();
                    if (class_list.Contains("context_module_sub_header"))
                    {
                        continue;
                    }
                    else if (class_list.Contains("external_url"))
                    {
                        continue;
                    }

                    chrome.Url = cur_item.FindElement(By.CssSelector("a.item_link")).GetAttribute("href");
                    var edit_button = wait.Until(c => c.FindElement(By.CssSelector("a[class*=\"edit\"]")));
                    chrome.Url = edit_button.GetAttribute("href");

                    if (chrome.Url.Contains("quiz"))
                    {
                        wait.Until(c => c.SwitchTo().Frame(c.FindElement(By.Id("quiz_description_ifr"))));
                        chrome.ExecuteScript("var el = document.querySelector(\"img\"); if(el){el.setAttribute('alt','');}");
                        chrome.SwitchTo().ParentFrame();
                        chrome.ExecuteScript("window.scrollTo(0,document.body.scrollHeight);");
                        wait.Until(c => c.FindElement(By.CssSelector("button.save_quiz_button"))).Click();
                    }
                    else if (chrome.Url.Contains("assignments"))
                    {
                        wait.Until(c => c.SwitchTo().Frame(c.FindElement(By.Id("assignment_description_ifr"))));
                        chrome.ExecuteScript("var el = document.querySelector(\"img\"); if(el){el.setAttribute('alt','');}");
                        chrome.SwitchTo().ParentFrame();
                        chrome.ExecuteScript("window.scrollTo(0,document.body.scrollHeight);");
                        wait.Until(c => c.FindElement(By.CssSelector("button.btn.btn-primary"))).Click();
                    }
                    else if (chrome.Url.Contains("discussion"))
                    {
                        wait.Until(c => c.SwitchTo().Frame(c.FindElement(By.Id("discussion-topic-message9_ifr"))));
                        chrome.ExecuteScript("var el = document.querySelector(\"img\"); if(el){el.setAttribute('alt','');}");
                        chrome.SwitchTo().ParentFrame();
                        chrome.ExecuteScript("window.scrollTo(0,document.body.scrollHeight);");
                        wait.Until(c => c.FindElement(By.CssSelector("[data-text-while-loading]"))).Click();
                    }
                    else if (chrome.Url.Contains("pages"))
                    {
                        wait.Until(c => c.SwitchTo().Frame(c.FindElement(By.Id("wiki_page_body_ifr"))));
                        chrome.ExecuteScript("var el = document.querySelector(\"img\"); if(el){el.setAttribute('alt','');}");
                        chrome.SwitchTo().ParentFrame();
                        chrome.ExecuteScript("window.scrollTo(0,document.body.scrollHeight);");
                        wait.Until(c => c.FindElement(By.CssSelector("button.submit"))).Click();
                    }

                    if (chrome.isAlertPresent())
                    {
                        chrome.SwitchTo().Alert().Dismiss();
                        chrome.SwitchTo().Window(chrome.CurrentWindowHandle);
                    }
                    chrome.SwitchTo().Window(chrome.CurrentWindowHandle);
                    wait.UntilElementIsVisible(By.CssSelector("a[class*='edit']"));
                    chrome.FindElementByCssSelector("a[class='home']").Click();
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        Run run = new Run($"\nError on {i}")
                        {
                            Foreground = System.Windows.Media.Brushes.Red
                        };
                        TerminalOutput.Inlines.Add(run);
                    });
                    Dispatcher.Invoke(() =>
                    {
                        Run run = new Run($"\nUrl: {chrome?.Url}")
                        {
                            Foreground = System.Windows.Media.Brushes.Red
                        };
                        TerminalOutput.Inlines.Add(run);
                    });
                    Dispatcher.Invoke(() =>
                    {
                        Run run = new Run($"\nID: {store?.id}")
                        {
                            Foreground = System.Windows.Media.Brushes.Red
                        };
                        TerminalOutput.Inlines.Add(run);
                    });
                    Dispatcher.Invoke(() =>
                    {
                        Run run = new Run($"\nText: {store?.text}")
                        {
                            Foreground = System.Windows.Media.Brushes.Red
                        };
                        TerminalOutput.Inlines.Add(run);
                    });
                    Dispatcher.Invoke(() =>
                    {
                        Run run = new Run($"\nClassList: {String.Join(", ", store?.class_list)}")
                        {
                            Foreground = System.Windows.Media.Brushes.Red
                        };
                        TerminalOutput.Inlines.Add(run);
                    });
                    Dispatcher.Invoke(() =>
                    {
                        Run run = new Run("\n" + ex?.Message + "\n")
                        {
                            Foreground = System.Windows.Media.Brushes.Red
                        };
                        TerminalOutput.Inlines.Add(run);
                    });
                    Dispatcher.Invoke(() =>
                    {
                        Run run = new Run("\n")
                        {
                            Foreground = System.Windows.Media.Brushes.Red
                        };
                        TerminalOutput.Inlines.Add(run);
                    });
                    try
                    {
                        if (chrome.isAlertPresent())
                        {
                            chrome.SwitchTo().Alert().Dismiss();
                            chrome.SwitchTo().Window(chrome.CurrentWindowHandle);
                        }
                    }
                    catch
                    {
                        //do nothing
                    }
                    try
                    {
                        chrome.Url = home_page_url;
                    }
                    catch
                    {
                        Dispatcher.Invoke(() =>
                        {
                            Run run = new Run("Program broke and will not run\n")
                            {
                                Foreground = System.Windows.Media.Brushes.Red
                            };
                            TerminalOutput.Inlines.Add(run);
                        });
                    }
                }
            }
            Dispatcher.Invoke(() =>
            {
                Run run = new Run($"RALT finished.\nTime taken: {s.Elapsed.ToString(@"hh\:mm\:ss")}\n")
                {
                    Foreground = System.Windows.Media.Brushes.White
                };
                TerminalOutput.Inlines.Add(run);
            });
        }
        private void BuzzRalt(object sender, DoWorkEventArgs e)
        {
            //Get number of modules
            var number_of_modules = chrome.FindElementsByCssSelector("button[class*=\"glyphicon-option\"]").Count();
            for (int i = 0; i < number_of_modules; i++)
            {
                try
                {
                    //Open moudle options
                    wait.Until(c => c.FindElement(By.CssSelector("button[class*=\"glyphicon-option\"]")).Displayed);
                    chrome.FindElementsByCssSelector("button[class*=\"glyphicon-option\"]")[i].Click();
                    //Wait for edit button to show up and click displayed one
                    wait.Until(c => {
                        var els = c.FindElements(By.LinkText("Edit"));
                        if (els.Select(el => el.Displayed).Count() > 0)
                        {
                            return els.Where(el => el.Displayed).First();
                        }
                        else
                        {
                            return null;
                        }
                    });
                    //Get image
                    var image = wait.Until(c => c.FindElement(By.CssSelector("img[class*='fr-draggable']")));
                    //Clear title if it exists
                    if ("" != image.GetAttribute("title") && null != image.GetAttribute("title"))
                    {
                        chrome.ExecuteScript("document.querySelector(\"img[class*='fr-draggable']\").setAttribute('title',''));");
                    }
                    //Enter image options
                    image.Click();
                    //Wait for button to edit alt text shows up
                    wait.Until(c =>
                    {
                        var el = c.FindElement(By.CssSelector("button[id*='imageAlt']"));
                        if (el.Displayed)
                        {
                            return el;
                        }
                        else
                        {
                            return null;
                        }
                    }).Click();
                    //Clear the text field
                    wait.Until(c =>
                    {
                        var el = c.FindElement(By.CssSelector("input[placeholder*=\"Alternative\"])"));
                        if (el.Displayed)
                        {
                            return el;
                        }
                        else
                        {
                            return null;
                        }
                    }).Clear();

                    //Update alt text
                    chrome.FindElementsByTagName("button").Where(el => el.Text == "Update").First().Click();
                    //Save page
                    chrome.FindElementsByTagName("button").Where(el => el.Text == "Save").First().Click();

                    try
                    {
                        //Check for pop up
                        wait.Timeout = new TimeSpan(0, 0, 1);
                        wait.Until(c =>
                        {
                            return c.FindElement(By.CssSelector("mat-dialog-container"));
                        }).FindElements(By.CssSelector("button.mat-button.mat-primary")).First().Click();
                        wait.Timeout = new TimeSpan(0, 0, 3);
                        //Try to then leave page
                        chrome.FindElementsByTagName("button")
                            .Where(el => el.Text.Contains("Clear"))
                            .First()
                            .Click();
                        //May ask "are you sure"
                        chrome.FindElementsByCssSelector("span.mat-button-wrapper")
                            .Where(el => el.Text.Contains("LEAVE"))
                            .First()
                            .Click();
                    }
                    catch
                    {
                        //Silently error since this should mean there is no popup
                    }
                }
                catch
                {
                    Console.WriteLine("Nothing Found");
                    chrome.FindElementsByTagName("button").Where(el => el.Text.Contains("Save")).First().Click();
                    try
                    {
                        wait.Timeout = new TimeSpan(0, 0, 1);
                        wait.Until(c =>
                        {
                            return c.FindElement(By.CssSelector("mat-dialog-container"));
                        }).FindElements(By.CssSelector("button.mat-button.mat-primary")).First().Click();
                        wait.Timeout = new TimeSpan(0, 0, 3);
                        //Try to then leave page
                        chrome.FindElementsByTagName("button")
                            .Where(el => el.Text.Contains("Clear"))
                            .First()
                            .Click();
                        //May ask "are you sure"
                        chrome.FindElementsByCssSelector("span.mat-button-wrapper")
                            .Where(el => el.Text.Contains("LEAVE"))
                            .First()
                            .Click();
                    }
                    catch
                    {
                        //Silently error since this should mean there is no popup
                    }
                }
            }
        }
        private void Login_Click(object sender, EventArgs e)
        {
            BackgroundWorker worker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            worker.DoWork += LoginToByu;
            worker.RunWorkerAsync();
        }
        private void LoginToByu(object sender = null, DoWorkEventArgs e = null)
        {
            string username = MainWindow.panelOptions.ByuCred["Username"];            
            var password = MainWindow.panelOptions.ByuCred["Password"];
            if (chrome.Url.Contains("instructure") || chrome.Url.Contains("cas"))
            {
                wait.Until(c => c.FindElement(By.Id("username"))).ReturnClear().SendKeys(username);
                wait.Until(c => c.FindElement(By.Id("password"))).ReturnClear().SendKeys(password);
                wait.Until(c => c.FindElement(By.CssSelector("input[value*=\"Sign\"]"))).Submit();
            }
            else
            {
                chrome.SwitchTo().Window("Brigham Young University Sign-in Service");
                wait.Until(c => c.FindElement(By.Id("netid"))).ReturnClear().SendKeys(username);
                wait.Until(c => c.FindElement(By.CssSelector("input#password"))).ReturnClear().SendKeys(password);
                wait.Until(c => c.FindElement(By.CssSelector("input[value*=\"Sign\"]"))).Submit();
                chrome.SwitchTo().Window(chrome.CurrentWindowHandle);
            }
        }
        private void A11yHelp_Click(object sender, EventArgs e)
        {
            BackgroundWorker worker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            worker.DoWork += ShowA11yHelpers;
            worker.RunWorkerAsync();
        }
        private void ShowA11yHelpers(object sender, DoWorkEventArgs e)
        {
            void RecursiveA11y(int num = 0)
            {   //recursively run the javascript within every iframe, so it should work on any webpage.
                var num_frames = chrome.FindElementsByTagName("iframe").Count();
                if (num > 20)
                {
                    //Fail safe to not get stuck
                    return;
                }
                for (int i = 0; i < num_frames; i++)
                {
                    chrome.SwitchTo().Frame(i);
                    RecursiveA11y((num + 1));
                }
                if (chrome.FindElementsByCssSelector(".AccessibilityHelper").Count() > 0)
                {   //If that class exists then the function was already ran
                    chrome.SwitchTo().ParentFrame();
                    return;
                }
                //Huge string of javascript to highlight all of the given accessibility things I want. Can just add a new line at the bottom of the string to extend it.
                chrome.ExecuteScript(@"(function() { var e, t, o = document.querySelectorAll(""img""); function r(e) { ""use strict""; e.style.backgroundColor = ""#FFE"", e.style.borderColor = ""#393"", e.style.boxShadow = ""1px 2px 5px #CCC"", e.style.zIndex = ""1"" } function s(e) { ""use strict""; e.style.backgroundColor = ""#FFF"", e.style.borderColor = ""#CCC"", e.style.boxShadow = ""none"", e.style.zIndex = ""0"" } function l(e, t, o) { ""use strict""; return function() { e(t), e(o) } } for (t = 0; t < o.length; t++)(e = document.createElement(""div"")).style.backgroundColor = ""#FFF"", e.className = ""AccessibilityHelper"", e.color = ""black"", e.style.border = ""2px solid #CCC"", e.style.borderRadius = ""7px"", /*e.style.clear = ""right"",*/ e.style.cssFloat = o[t].style.cssFloat, e.style.margin = ""-7px -0px -7px -7px"", e.style.padding = ""5px"", e.style.position = ""relative"", e.style.textAlign = ""left"", e.style.whiteSpace = ""pre-wrap"", e.style.width = ""75px"", e.style.fontSize = ""12px"", e.style.zIndex = ""0"", e.textContent = ""Alt Text:\n"" + o[t].alt, o[t].style.backgroundColor = ""#FFF"", o[t].className += "" AccessibilityHelper"", o[t].style.border = ""2px solid #CCC"", o[t].style.borderRadius = ""7px"", o[t].style.margin = ""-7px"", o[t].style.padding = ""5px"", o[t].parentNode.insertBefore(e, o[t]), e.addEventListener(""mouseover"", l(r, e, o[t])), o[t].addEventListener(""mouseover"", l(r, e, o[t])), e.addEventListener(""mouseout"", l(s, e, o[t])), o[t].addEventListener(""mouseout"", l(s, e, o[t])); }());javascript:(function() { var e, t, o = document.querySelectorAll(""iframe""); function r(e) { ""use strict""; e.style.backgroundColor = ""#FFE"", e.style.borderColor = ""#393"", e.style.boxShadow = ""1px 2px 5px #CCC"", e.style.zIndex = ""1"" } function s(e) { ""use strict""; e.style.backgroundColor = ""#FFF"", e.style.borderColor = ""#CCC"", e.style.boxShadow = ""none"", e.style.zIndex = ""0"" } function l(e, t, o) { ""use strict""; return function() { e(t), e(o) } } for (t = 0; t < o.length; t++)(e = document.createElement(""div"")).style.backgroundColor = ""#FFF"", e.className = ""AccessibilityHelper"", e.style.border = ""2px solid #CCC"", e.style.borderRadius = ""7px"", e.style.clear = ""right"", /*e.style.cssFloat = ""left"",*/ e.style.margin = ""-7px -0px -7px -7px"", e.style.padding = ""5px"", e.style.position = ""relative"", e.style.textAlign = ""left"", e.style.whiteSpace = ""pre-wrap"", e.style.width = ""300px"", e.style.zIndex = ""0"", e.textContent = ""Iframe title:\n"" + o[t].title, o[t].style.backgroundColor = ""#FFF"", o[t].className += "" AccessibilityHelper"", o[t].style.border = ""2px solid #CCC"", o[t].style.borderRadius = ""7px"", o[t].style.margin = ""-7px"", o[t].style.padding = ""5px"", o[t].parentNode.insertBefore(e, o[t]), e.addEventListener(""mouseover"", l(r, e, o[t])), o[t].addEventListener(""mouseover"", l(r, e, o[t])), e.addEventListener(""mouseout"", l(s, e, o[t])), o[t].addEventListener(""mouseout"", l(s, e, o[t])); }());javascript:(function() { var e, t, o = document.querySelectorAll(""h1""); function r(e) { ""use strict""; e.style.backgroundColor = ""#FFE"", e.style.borderColor = ""#393"", e.style.boxShadow = ""1px 2px 5px #CCC"", e.style.zIndex = ""1"" } function s(e) { ""use strict""; e.style.backgroundColor = ""#FFF"", e.style.borderColor = ""#CCC"", e.style.boxShadow = ""none"", e.style.zIndex = ""0"" } function l(e, t, o) { ""use strict""; return function() { e(t), e(o) } } for (t = 0; t < o.length; t++)(e = document.createElement(""div"")).style.backgroundColor = ""#FFF"", e.className = ""AccessibilityHelper"", e.style.border = ""2px solid #CCC"", e.style.borderRadius = ""7px"", e.style.clear = ""right"",/* e.style.cssFloat = ""left"",*/ e.style.margin = ""-7px 0px 0px 0px"", e.style.padding = ""5px"", e.style.position = ""relative"", e.style.textAlign = ""left"", e.style.whiteSpace = ""pre-wrap"", e.style.width = ""35px"", e.style.zIndex = ""0"", e.textContent = o[t].tagName, o[t].style.backgroundColor = ""#FFF"", o[t].className += "" AccessibilityHelper"", o[t].style.border = ""2px solid #CCC"", o[t].style.borderRadius = ""7px"", o[t].style.margin = ""-7px"", o[t].style.padding = ""5px"", o[t].parentNode.insertBefore(e, o[t]), e.addEventListener(""mouseover"", l(r, e, o[t])), o[t].addEventListener(""mouseover"", l(r, e, o[t])), e.addEventListener(""mouseout"", l(s, e, o[t])), o[t].addEventListener(""mouseout"", l(s, e, o[t])); }());(function() { var e, t, o = document.querySelectorAll(""h2""); function r(e) { ""use strict""; e.style.backgroundColor = ""#FFE"", e.style.borderColor = ""#393"", e.style.boxShadow = ""1px 2px 5px #CCC"", e.style.zIndex = ""1"" } function s(e) { ""use strict""; e.style.backgroundColor = ""#FFF"", e.style.borderColor = ""#CCC"", e.style.boxShadow = ""none"", e.style.zIndex = ""0"" } function l(e, t, o) { ""use strict""; return function() { e(t), e(o) } } for (t = 0; t < o.length; t++)(e = document.createElement(""div"")).style.backgroundColor = ""#FFF"", e.className = ""AccessibilityHelper"", e.style.border = ""2px solid #CCC"", e.style.borderRadius = ""7px"", e.style.clear = ""right"", /*e.style.cssFloat = ""left"",*/ e.style.margin = ""-7px 0px 0px -7px"", e.style.padding = ""5px"", e.style.position = ""relative"", e.style.textAlign = ""left"", e.style.whiteSpace = ""pre-wrap"", e.style.width = ""35px"", e.style.zIndex = ""0"", e.textContent = o[t].tagName, o[t].style.backgroundColor = ""#FFF"", o[t].className += "" AccessibilityHelper"", o[t].style.border = ""2px solid #CCC"", o[t].style.borderRadius = ""7px"", o[t].style.margin = ""-7px"", o[t].style.padding = ""5px"", o[t].parentNode.insertBefore(e, o[t]), e.addEventListener(""mouseover"", l(r, e, o[t])), o[t].addEventListener(""mouseover"", l(r, e, o[t])), e.addEventListener(""mouseout"", l(s, e, o[t])), o[t].addEventListener(""mouseout"", l(s, e, o[t])); }());(function() { var e, t, o = document.querySelectorAll(""h3""); function r(e) { ""use strict""; e.style.backgroundColor = ""#FFE"", e.style.borderColor = ""#393"", e.style.boxShadow = ""1px 2px 5px #CCC"", e.style.zIndex = ""1"" } function s(e) { ""use strict""; e.style.backgroundColor = ""#FFF"", e.style.borderColor = ""#CCC"", e.style.boxShadow = ""none"", e.style.zIndex = ""0"" } function l(e, t, o) { ""use strict""; return function() { e(t), e(o) } } for (t = 0; t < o.length; t++)(e = document.createElement(""div"")).style.backgroundColor = ""#FFF"", e.className = ""AccessibilityHelper"", e.style.border = ""2px solid #CCC"", e.style.borderRadius = ""7px"", e.style.clear = ""right"", /*e.style.cssFloat = ""left"",*/ e.style.margin = ""-7px 0px 0px -7px"", e.style.padding = ""5px"", e.style.position = ""relative"", e.style.textAlign = ""left"", e.style.whiteSpace = ""pre-wrap"", e.style.width = ""35px"", e.style.zIndex = ""0"", e.textContent = o[t].tagName, o[t].style.backgroundColor = ""#FFF"", o[t].className += "" AccessibilityHelper"", o[t].style.border = ""2px solid #CCC"", o[t].style.borderRadius = ""7px"", o[t].style.margin = ""-7px"", o[t].style.padding = ""5px"", o[t].parentNode.insertBefore(e, o[t]), e.addEventListener(""mouseover"", l(r, e, o[t])), o[t].addEventListener(""mouseover"", l(r, e, o[t])), e.addEventListener(""mouseout"", l(s, e, o[t])), o[t].addEventListener(""mouseout"", l(s, e, o[t])); }());(function() { var e, t, o = document.querySelectorAll(""h4""); function r(e) { ""use strict""; e.style.backgroundColor = ""#FFE"", e.style.borderColor = ""#393"", e.style.boxShadow = ""1px 2px 5px #CCC"", e.style.zIndex = ""1"" } function s(e) { ""use strict""; e.style.backgroundColor = ""#FFF"", e.style.borderColor = ""#CCC"", e.style.boxShadow = ""none"", e.style.zIndex = ""0"" } function l(e, t, o) { ""use strict""; return function() { e(t), e(o) } } for (t = 0; t < o.length; t++)(e = document.createElement(""div"")).style.backgroundColor = ""#FFF"", e.className = ""AccessibilityHelper"", e.style.border = ""2px solid #CCC"", e.style.borderRadius = ""7px"", e.style.clear = ""right"", /*e.style.cssFloat = ""left"",*/ e.style.margin = ""-7px 0px 0px -7px"", e.style.padding = ""5px"", e.style.position = ""relative"", e.style.textAlign = ""left"", e.style.whiteSpace = ""pre-wrap"", e.style.width = ""35px"", e.style.zIndex = ""0"", e.textContent = o[t].tagName, o[t].style.backgroundColor = ""#FFF"", o[t].className += "" AccessibilityHelper"", o[t].style.border = ""2px solid #CCC"", o[t].style.borderRadius = ""7px"", o[t].style.margin = ""-7px"", o[t].style.padding = ""5px"", o[t].parentNode.insertBefore(e, o[t]), e.addEventListener(""mouseover"", l(r, e, o[t])), o[t].addEventListener(""mouseover"", l(r, e, o[t])), e.addEventListener(""mouseout"", l(s, e, o[t])), o[t].addEventListener(""mouseout"", l(s, e, o[t])); }());(function() { var e, t, o = document.querySelectorAll(""h5""); function r(e) { ""use strict""; e.style.backgroundColor = ""#FFE"", e.style.borderColor = ""#393"", e.style.boxShadow = ""1px 2px 5px #CCC"", e.style.zIndex = ""1"" } function s(e) { ""use strict""; e.style.backgroundColor = ""#FFF"", e.style.borderColor = ""#CCC"", e.style.boxShadow = ""none"", e.style.zIndex = ""0"" } function l(e, t, o) { ""use strict""; return function() { e(t), e(o) } } for (t = 0; t < o.length; t++)(e = document.createElement(""div"")).style.backgroundColor = ""#FFF"", e.className = ""AccessibilityHelper"", e.style.border = ""2px solid #CCC"", e.style.borderRadius = ""7px"", e.style.clear = ""right"", /*e.style.cssFloat = ""left"",*/ e.style.margin = ""-7px 0px 0px -7px"", e.style.padding = ""5px"", e.style.position = ""relative"", e.style.textAlign = ""left"", e.style.whiteSpace = ""pre-wrap"", e.style.width = ""35px"", e.style.zIndex = ""0"", e.textContent = o[t].tagName, o[t].style.backgroundColor = ""#FFF"", o[t].className += "" AccessibilityHelper"", o[t].style.border = ""2px solid #CCC"", o[t].style.borderRadius = ""7px"", o[t].style.margin = ""-7px"", o[t].style.padding = ""5px"", o[t].parentNode.insertBefore(e, o[t]), e.addEventListener(""mouseover"", l(r, e, o[t])), o[t].addEventListener(""mouseover"", l(r, e, o[t])), e.addEventListener(""mouseout"", l(s, e, o[t])), o[t].addEventListener(""mouseout"", l(s, e, o[t])); }());(function() { var e, t, o = document.querySelectorAll(""h6""); function r(e) { ""use strict""; e.style.backgroundColor = ""#FFE"", e.style.borderColor = ""#393"", e.style.boxShadow = ""1px 2px 5px #CCC"", e.style.zIndex = ""1"" } function s(e) { ""use strict""; e.style.backgroundColor = ""#FFF"", e.style.borderColor = ""#CCC"", e.style.boxShadow = ""none"", e.style.zIndex = ""0"" } function l(e, t, o) { ""use strict""; return function() { e(t), e(o) } } for (t = 0; t < o.length; t++)(e = document.createElement(""div"")).style.backgroundColor = ""#FFF"", e.className = ""AccessibilityHelper"", e.style.border = ""2px solid #CCC"", e.style.borderRadius = ""7px"", e.style.clear = ""right"", /*e.style.cssFloat = ""left"",*/ e.style.margin = ""-7px 0px 0px -7px"", e.style.padding = ""5px"", e.style.position = ""relative"", e.style.textAlign = ""left"", e.style.whiteSpace = ""pre-wrap"", e.style.width = ""35px"", e.style.zIndex = ""0"", e.textContent = o[t].tagName, o[t].style.backgroundColor = ""#FFF"", o[t].className += "" AccessibilityHelper"", o[t].style.border = ""2px solid #CCC"", o[t].style.borderRadius = ""7px"", o[t].style.margin = ""-7px"", o[t].style.padding = ""5px"", o[t].parentNode.insertBefore(e, o[t]), e.addEventListener(""mouseover"", l(r, e, o[t])), o[t].addEventListener(""mouseover"", l(r, e, o[t])), e.addEventListener(""mouseout"", l(s, e, o[t])), o[t].addEventListener(""mouseout"", l(s, e, o[t])); }());
    (function() {
    document.querySelectorAll(""th"").forEach(c => {if(c.scope == ""row""){c.style.backgroundColor = ""green"";}else if(c.scope == ""col""){c.style.backgroundColor = ""blue"";}else{c.style.backgroundColor = ""red""} c.className += "" AccessibilityHelper"";});
    document.querySelectorAll(""i"").forEach(c => {c.style.backgroundColor = ""DeepPink""; c.className += "" AccessibilityHelper"";});
    document.querySelectorAll(""b"").forEach(c => {c.style.backgroundColor = ""DarkViolet""; c.className += "" AccessibilityHelper"";});
    }());");
                chrome.SwitchTo().ParentFrame();
            }
            RecursiveA11y();
        }

    }

}
