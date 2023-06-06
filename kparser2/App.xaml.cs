namespace kparser2
{
    using System;
    using System.Windows;
    using kparser2.Core;
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            this.Activated += StartElmish;
        }

        private void StartElmish(object sender, EventArgs e)
        {
            this.Activated-= StartElmish;
            Program.main(MainWindow);
        }
    }
}
