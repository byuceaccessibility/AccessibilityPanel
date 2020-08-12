using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Documents;
using System.IO;
using System.Collections.ObjectModel;
using System.Diagnostics;
using My;
using System.Windows.Controls;

namespace WPFCommandPanel
{
    /// <summary>
    /// Interaction logic for A11yViewer.xaml
    /// </summary>
    public partial class A11yViewer : Page
    {
        public A11yViewer()
        {
            InitializeComponent();
            var tabs = new ObservableCollection<MyTab>();
            TabData.DataContext = tabs;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string dataDir = MainWindow.panelOptions.JsonDataDir;
            string[] fileList = Directory.GetFiles(dataDir, "*" + SearchBox.Text + "*.json");
            var tabs = new ObservableCollection<MyTab>();
            foreach (var file in fileList)
            {
                string json = "";
                using (StreamReader r = new StreamReader(file))
                {
                    json = r.ReadToEnd();
                }
                List<A11yData> fileData = JsonConvert.DeserializeObject<List<A11yData>>(json);
                var tab = new MyTab() { Header = System.IO.Path.GetFileName(file).Split('.')[0] };
                foreach (var item in fileData)
                {
                    tab.Data.Add(item);
                }
                tabs.Add(tab);
            }
            TabData.DataContext = tabs;
        }

        private void DataGridHyperlinkColumn_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)e.OriginalSource;
            try
            {
                Process.Start(link.NavigateUri.AbsoluteUri);
            }
            catch
            {

            }

        }

        private void Revert_Button_Click_1(object sender, RoutedEventArgs e)
        {
            string dataDir = MainWindow.panelOptions.JsonDataDir;
            var selectedTab = TabData.SelectedItem as MyTab;
            string json = "";
            using (StreamReader r = new StreamReader(System.IO.Path.Combine(dataDir, selectedTab.Header + ".json")))
            {
                json = r.ReadToEnd();
            }
            List<A11yData> fileData = JsonConvert.DeserializeObject<List<A11yData>>(json);
            selectedTab.Data.Clear();
            foreach (var item in fileData)
            {
                selectedTab.Data.Add(item);
            }
            TabData.SelectedItem = selectedTab;
        }

        private void RevertAll_Button_Click_1(object sender, RoutedEventArgs e)
        {
            var selected = TabData.SelectedItem as MyTab;
            string dataDir = MainWindow.panelOptions.JsonDataDir;
            string[] fileList = Directory.GetFiles(dataDir, "*" + SearchBox.Text + "*.json");
            var tabs = new ObservableCollection<MyTab>();
            foreach (var file in fileList)
            {
                string json = "";
                using (StreamReader r = new StreamReader(file))
                {
                    json = r.ReadToEnd();
                }
                List<A11yData> fileData = JsonConvert.DeserializeObject<List<A11yData>>(json);
                var tab = new MyTab() { Header = System.IO.Path.GetFileName(file).Split('.')[0] };
                foreach (var item in fileData)
                {
                    tab.Data.Add(item);
                }
                tabs.Add(tab);
            }
            TabData.DataContext = tabs;
            TabData.SelectedItem = selected;
        }
        private void Save_Button_Click_2(object sender, RoutedEventArgs e)
        {
            string dataDir = MainWindow.panelOptions.JsonDataDir;
            var selectedTab = TabData.SelectedItem as MyTab;
            List<A11yData> toSave = new List<A11yData>(selectedTab.Data);
            using (StreamWriter file = new StreamWriter(System.IO.Path.Combine(dataDir, selectedTab.Header + ".json"), false))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(file, toSave);
            }
            MessageBox.Show($"{selectedTab.Header} saved.");
        }

        private void SaveAll_Button_Click_2(object sender, RoutedEventArgs e)
        {
            string dataDir = MainWindow.panelOptions.JsonDataDir;
            foreach (var item in TabData.Items)
            {
                var tab = item as MyTab;
                List<A11yData> toSave = new List<A11yData>(tab.Data);
                using (StreamWriter file = new StreamWriter(System.IO.Path.Combine(dataDir, tab.Header + ".json"), false))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Formatting = Formatting.Indented;
                    serializer.Serialize(file, toSave);
                }
            }
            MessageBox.Show($"All data saved.");
        }
    }
}
