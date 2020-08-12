using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// Interaction logic for DraggablePopup.xaml
    /// </summary>
    public partial class DraggablePopup : Popup
    {
        public DraggablePopup()
        {
            InitializeComponent();
            var thumb = new Thumb
            {
                Width = 0,
                Height = 0,
            };
            ContentPanel.Children.Add(thumb);

            MouseDown += (sender, e) =>
            {
                if(e.RightButton == MouseButtonState.Pressed)
                {
                    IsOpen = false;
                    e.Handled = true;
                    return;
                }
                thumb.RaiseEvent(e);
            };

            thumb.DragDelta += (sender, e) =>
            {
                HorizontalOffset += e.HorizontalChange;
                VerticalOffset += e.VerticalChange;
            };
        }        
    }
}
