using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace My
{
    public class PanelOptions
    {
        public string ChromeDriverPath { get; set; }
        public string FirefoxDriverPath { get; set; }
        public string QDriveContentUrl { get; set; }
        public string IDriveContentUrl { get; set; }
        public Dictionary<string, string> BrightCoveCred { get; set; }
        public string JsonDataDir { get; set; }
        public Dictionary<string, string> BYUOnlineCreds { get; set; }
        public Dictionary<string, string> BYUISTestCreds { get; set; }
        public Dictionary<string, string> BYUMasterCoursesCreds { get; set; }
        public Dictionary<string, string> ByuCred { get; set; }
        public string ReportPath { get; set; }
        public string PowershellScriptDir { get; set; }
        public string ExcelTemplatePath { get; set; }
        public string GoogleApi { get; set; }
        public List<string> FilesToIgnore { get; set; }
        public string HighScorePath { get; set; }
        public string CourseBackupDir { get; set; }
        public string BaseExcelArchive { get; set; }
        public string BaseMoveReportsDir { get; set; }
        public string A11yEmail { get; set; }
    }
}
