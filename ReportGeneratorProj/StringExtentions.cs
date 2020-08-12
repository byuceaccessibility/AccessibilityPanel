using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace My.StringExtentions
{
    public static class StringExtentions
    {   //Helpful string extension methods
        public static string[] CleanSplit(this string ToSplit, string seperator)
        {   //Splits a string and gets rid of any empty, null or whitespace only items.
            if (ToSplit == null)
            {
                return null;
            }
            return ToSplit.Split(new[] { seperator }, StringSplitOptions.RemoveEmptyEntries)
                          .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                          .ToArray();
        }
        public static string[] CleanSplit(this string ToSplit, char seperator)
        {   //Splits a string and gets rid of any empty, null or whitespace only items.
            if (ToSplit == null)
            {
                return null;
            }
            return ToSplit.Split(seperator)
                          .Where(s => !string.IsNullOrEmpty(s) && !string.IsNullOrWhiteSpace(s))
                          .ToArray();
        }
        public static string FirstCharToUpper(this string input)
        {   //Capitalizes the first character of an input string
            switch (input)
            {
                case null: throw new ArgumentNullException(nameof(input));
                case "": throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
                default: return input.First().ToString().ToUpper() + input.Substring(1);
            }
        }
        public static string RollOverTime(this string ToRollOver)
        {
            //Must be formated as 00:00:00, hh:mm:ss, else it just returns the exact same string
            if (!(new Regex(@"^\d\d:\d\d:\d\d$").IsMatch(ToRollOver)))
            {
                return ToRollOver;
            }
            var parts = ToRollOver.CleanSplit(':');
            while (int.Parse(parts[1]) > 59)
            {
                parts[1] = (int.Parse(parts[1]) - 60).ToString("D2");
                parts[0] = (int.Parse(parts[0]) + 1).ToString("D2");
            }
            return parts[0] + ":" + parts[1] + ":" + parts[2];
        }
    }
}
