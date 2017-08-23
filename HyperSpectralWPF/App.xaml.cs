using Microsoft.Kinect.Wpf.Controls;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace HyperSpectralWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        ///<summary>
        /// Gets the app level KinectRegion element, 
        ///which is created in App.xaml.cs
        ///</summary>
        internal KinectRegion KinectRegion { get; set; }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Prevent default unhandled exception processing
            e.Handled = true;
        }
    }
}
