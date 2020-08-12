using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;

namespace My.SeleniumExtentions
{
    public static class SeleniumExtentions
    {   //Class to contain any extenstions to selenium
        public static IWebElement UntilElementIsVisible(this WebDriverWait wait, By locator)
        {   //Replace constantly used code that I was writing
            //Waits until the specified element is displayed or times out
            return wait.Until(c =>
            {
                try
                {
                    var el = c.FindElement(locator);
                    if (el.Displayed)
                    {
                        return el;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch
                {
                    return null;
                }
            });
        }

        public static IWebElement UntilElementExist(this WebDriverWait wait, By locator)
        {
            return wait.Until(c =>
            {
                try
                {
                    var el = c.FindElement(locator);
                    return el;
                }
                catch
                {
                    return null;
                }
            });
        }

        public static IWebElement ReturnClick(this IWebElement element)
        {
            element.Click();
            return element;
        }

        public static IWebElement ForChildElement(this WebDriverWait wait, IWebElement element, By locator)
        {
            return wait.Until(c =>
            {
                try
                {
                    var el = element.FindElement(locator);
                    return el;
                }
                catch
                {
                    return null;
                }
            });
        }

        public static bool UntilPageLoads(this WebDriverWait wait)
        {
            return wait.Until(c =>
            {
                return ((IJavaScriptExecutor)c).ExecuteScript("return document.readyState").Equals("complete");
            });
        }
        public static IWebElement ReturnClear(this IWebElement element)
        {
            element.SendKeys("");
            element.SendKeys(Keys.Backspace);
            element.Clear();
            return element;
        }

        public static bool isAlertPresent(this FirefoxDriver ff)
        {
            try
            {
                ff.SwitchTo().Alert();
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static bool isAlertPresent(this ChromeDriver chrome)
        {
            try
            {
                chrome.SwitchTo().Alert();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
