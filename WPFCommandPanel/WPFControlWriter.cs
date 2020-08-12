using System.Text;
using System.Windows.Controls;
using System.IO;

namespace My.WPFControlWriter
{
    //Object to control console output / put it into the terminal on the command panel
    public class ControlWriter : TextWriter
    {
        private TextBlock terminal;
        public ControlWriter(TextBlock send_here)
        {
            this.terminal = send_here;
        }

        public override void Write(char value)
        {
            if (value == '\n')
            {
                return;
            }
            base.Write(value);
            terminal.Dispatcher.Invoke(() =>
            {
                terminal.Inlines.Add(value.ToString());
            });
        }

        public override void Write(string value)
        {
            base.Write(value);
            terminal.Dispatcher.Invoke(() =>
            {
                terminal.Inlines.Add(value);
            });
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

    }
}
