using System;
using System.Collections.Generic;
using System.Linq;
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
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using My.CanvasApi;
using OfficeOpenXml;

namespace WPFCommandPanel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Need to referance the main window so that we can navigate to new pages from other elements
        public static MainWindow AppWindow;
        //Not really needed to have this reference but if we want to add more tabs / pages then we can store the old pages so we don't lose any data from them.
        public static CommandPanel CommandPanelObj;
        public static A11yViewer a11YViewer;
        public static A11yRepair a11YRepair;
        public static OptionsPage optionsPage;
        public static My.PanelOptions panelOptions;
        public MainWindow()
        {
            InitializeComponent();
            AppWindow = this;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            string json = "";
            string path = Assembly.GetEntryAssembly().Location.Contains("source") ? @"C:\Users\jwilli48\Desktop\AccessibilityTools\A11yPanel\options.json" :
                                System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + @"\options.json";
            using (StreamReader r = new StreamReader(path))
            {
                json = r.ReadToEnd();
            }
            ; panelOptions = JsonConvert.DeserializeObject<My.PanelOptions>(json);
            CommandPanelObj = new CommandPanel();
            a11YViewer = new A11yViewer();
            a11YRepair = new A11yRepair();
            optionsPage = new OptionsPage();            
            ShowPage.Navigate(CommandPanelObj);            
        }

        private void SwitchA11yReview(object sender, RoutedEventArgs e)
        {
            ShowPage.Navigate(CommandPanelObj);
        }

        private void SwitchA11yViewer(object sender, RoutedEventArgs e)
        {
            ShowPage.Navigate(a11YViewer);
        }

        private void SwitchA11yRepair(object sender, RoutedEventArgs e)
        {
            ShowPage.Navigate(a11YRepair);
        }
        private void SwitchOptions(object sender, RoutedEventArgs e)
        {
            ShowPage.Navigate(optionsPage);
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var radiobutton = sender as RadioButton;

            CanvasApi.ChangeDomain(radiobutton.Content.ToString());
            this.Dispatcher.Invoke(() =>
            {
                Run run = new Run($"Domain changed to {radiobutton.Content.ToString()}\n")
                {
                    Foreground = System.Windows.Media.Brushes.Green
                };
                CommandPanelObj.TerminalOutput.Inlines.Add(run);
            });
        }
    }
}
