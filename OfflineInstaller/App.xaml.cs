using System;
using System.Windows;

namespace OfflineInstaller
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Update the title of the MainWindow, this is designed to show the User the Station ID as well as the Current IP address.
        /// </summary>
        /// <param name="title"></param>
        public static void SetWindowTitle(string title)
        {
            Current.Dispatcher.BeginInvoke(new Action(() => {
                Current.MainWindow.Title = title;
            }));
        }
    }
}
