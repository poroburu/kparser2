﻿using System;
using System.Windows;

namespace kparser2
{
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
