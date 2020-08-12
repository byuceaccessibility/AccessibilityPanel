using System.Collections.ObjectModel;

namespace My
{
    class MyTab
    {
        public string Header { get; set; }

        public ObservableCollection<A11yData> Data { get; } = new ObservableCollection<A11yData>();
    }
}
