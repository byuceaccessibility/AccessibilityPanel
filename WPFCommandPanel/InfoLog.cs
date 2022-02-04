using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFCommandPanel
{   // Class for logging errors and other info
    internal class InfoLog
    {
        public InfoLog()
        {

        }

        public string _File_Path = MainWindow.panelOptions.LogPathLocal;

        public void Report(string message)
        {
            using (StreamWriter w = File.AppendText(_File_Path))
            {
                Log(message, w);
            }
        }

        public static void Log(string logMessage, TextWriter w)
        {
            w.Write("\r\nLog Entry : ");
            w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
            w.WriteLine("  :");
            w.WriteLine($"  : {logMessage}");
            w.WriteLine("-------------------------------");
        }
    }
}
