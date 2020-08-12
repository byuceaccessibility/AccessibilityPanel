using System;
using System.Linq;

using System.Windows.Controls;

namespace WPFCommandPanel
{
    public partial class CommandPanel : Page
    {
        private void FileWatcher_Created(object sender, System.IO.FileSystemEventArgs e)
        {
            //If a file is created that is an excel doc we want to display it
            if (!e.FullPath.Contains(".xlsx"))
            {
                return;
            }
            this.file_paths.Add(new FileDisplay(e.FullPath));
        }
        public void FileWatcher_Deleted(object sender, System.IO.FileSystemEventArgs e)
        {
            //Remove any excel docs from the list if they were deleted
            if (!e.FullPath.Contains(".xlsx"))
            {
                return;
            }
            this.file_paths.Remove(file_paths.First(f => f.FullName == e.FullPath));
        }
        public void FileWatcher_Renamed(object sender, System.IO.RenamedEventArgs e)
        {
            /*
            try
            {
                //If it was renamed we need to delete old one and create new one with new path
                if (!e.FullPath.Contains(".xlsx"))
                {
                    return;
                }
                if (e.Name == e.OldName)
                {
                    return;
                }
                if (!e.OldName.Contains(".xlsx"))
                {   //Excel for some reason creates a .tmp file everytime you save the excel document and then renames that to the correct name
                    //This causes this event to be run even though it is not needed, so just return in that case
                    return;
                }
                file_paths.Remove(file_paths.FirstOrDefault(f => f.FullName == e.OldFullPath));
                file_paths.Add(new FileDisplay(e.FullPath));
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    System.Windows.Documents.Run run = new System.Windows.Documents.Run("Failed to rename file...")
                    {
                        Foreground = System.Windows.Media.Brushes.Red
                    };
                    TerminalOutput.Inlines.Add(run);
                });
            }
            */
        }
    }
}

