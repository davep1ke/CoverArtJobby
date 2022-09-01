using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace CoverArtJobby
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        
        enum nextArg {blank, scan_folder, backup_folder, destination_folder, recurse, background};

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            MainWindow wnd = new MainWindow();

            //parse the command line args
            bool cancel = false;
            nextArg arg = nextArg.blank;
            foreach (String next in e.Args)
            {
                //remove any "'s 
                String thisStr = next.Replace("\"","");

                if (arg == nextArg.blank && thisStr == nextArg.backup_folder.ToString())
                {
                    arg = nextArg.backup_folder;
                }
                else if (arg == nextArg.blank && thisStr == nextArg.destination_folder.ToString())
                {
                    arg = nextArg.destination_folder;
                }
                else if (arg == nextArg.blank && thisStr == nextArg.scan_folder.ToString())
                {
                    arg = nextArg.scan_folder;
                }
                else if (arg == nextArg.blank && thisStr == nextArg.recurse.ToString())
                {
                    wnd.chk_recurse.IsChecked = true;
                    arg = nextArg.blank;
                }
                else if (arg == nextArg.blank && thisStr == nextArg.background.ToString())
                {
                    wnd.runInBackground = true;
                    arg = nextArg.blank;
                }
                //Other, unexpected arg
                else if (arg == nextArg.blank)
                {
                    MessageBox.Show("wrong argument " + thisStr + ".\n\r Options: scan_folder, backup_folder, destination_folder, recurse, background"); //TODO - proper help
                    cancel = true;
                    
                }
                //individal modes
                else if (arg == nextArg.backup_folder)
                {
                    wnd.backupDirectory = new System.IO.DirectoryInfo(thisStr);
                    arg = nextArg.blank;
                }
                else if (arg == nextArg.destination_folder)
                {
                    wnd.destinationDirectory = new System.IO.DirectoryInfo(thisStr);
                    arg = nextArg.blank;
                }
                else if (arg == nextArg.scan_folder)
                {
                    wnd.scanDirectory = new System.IO.DirectoryInfo(thisStr);
                    arg = nextArg.blank;
                }
                else if (arg == nextArg.recurse)
                {
                    //NA
                }
                else if (arg == nextArg.background)
                {
                    //NA
                }

            }
            if (cancel)
            {
                this.Shutdown();
            } 
            else
            {
                wnd.postSetup();
            }
           
        }



    }
}
