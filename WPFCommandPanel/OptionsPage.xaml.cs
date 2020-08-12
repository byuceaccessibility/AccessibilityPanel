using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WPFCommandPanel
{
    /// <summary>
    /// Interaction logic for Options.xaml
    /// </summary>
    public partial class OptionsPage : Page
    {
        public OptionsPage()
        {
            InitializeComponent();

            string json = "";
            string path = Assembly.GetEntryAssembly().Location.Contains("source") ? @"C:\Users\jwilli48\Desktop\AccessibilityTools\A11yPanel\options.json" :
                                System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + @"\options.json";
            using (StreamReader r = new StreamReader(path))
            {
                json = r.ReadToEnd();
            }
            editor.Text = json;          
        }

        private void editor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Key == Key.S)
                {
                    string path = Assembly.GetEntryAssembly().Location.Contains("source") ? @"C:\Users\jwilli48\Desktop\AccessibilityTools\A11yPanel\options.json" :
                    System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + @"\options.json";

                    File.WriteAllText(path, editor.Text);
                    e.Handled = true;
                }
            }
        }
    }
}
