﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using WinForms = System.Windows.Forms;
using WinControl = System.Windows.Forms.Control;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Web;
using System.Net;
using ATL;
using System.Reflection;
using CefSharp.Wpf;

namespace CoverArtJobby
{
    //test commit2
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string userAgent = "Mozilla / 5.0 (Windows NT 10.0; Win64; x64; rv:93.0) Gecko/20100101 Firefox/93.0";
        //public static string userAgent = "Mozilla/5.0 (Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko";
        // "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.1; WOW64; Trident/6.0;)"; 
        //"Mozilla/5.0 (compatible; MSIE 7.0; Windows NT 5.2; .NET CLR 1.0.3705;)"; 
        //"Mozilla/4.0 (compatible; MSIE 6.0;)"; 

        private bool imageUpdated = false;
        private bool tagsUpdated = false;
        public bool runInBackground = false;
        Track audioFile = null;

        //class var to prevent memory leakage
        private Bitmap imageBitmap;

        //various directories 
        public DirectoryInfo backupDirectory = null;
        public DirectoryInfo scanDirectory = null;
        public DirectoryInfo destinationDirectory = null;

        public MainWindow()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            CefSettings settings = new CefSettings();
            settings.CachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name +  @"\CEF";
            CefSharp.Cef.Initialize(settings);

            this.Hide();
            InitializeComponent();

            webFrame.BeginInit();


        }

        public void postSetup()
        {
            
            populate_Treeview_Drives();

            //go to the scan folder
            if (scanDirectory != null)
            {
                expandFolder(scanDirectory, true);
            }


            /*
            tryExpand(FolderBrowser.Items, @"C:\");
            tryExpand(FolderBrowser.Items, @"Users");
            tryExpand(FolderBrowser.Items, @"Phil");
            tryExpand(FolderBrowser.Items, @"Dropbox");
            tryExpand(FolderBrowser.Items, @"Music");
            tryExpand(FolderBrowser.Items, @"musictemp",true);
            backupDirectory = new DirectoryInfo(@"C:\musictemp");
            */

            if (backupDirectory != null)
            {
                txtBackupFolder.Text = backupDirectory.FullName;
            }
            //chk_recurse.IsChecked = true; - now done by command line
            chk_autosearch_file.IsChecked = true;

            if (runInBackground)
            {
                backgroundScan();   
            }
            else
            {
                this.Show(); 
            }

        }

        #region FolderBrowser

        /// <summary>
        /// Try navigate to a folder recursively, from a lowest level folder updwards. Select it if neccessary
        /// </summary>
        /// <param name="di"></param>
        public void expandFolder(DirectoryInfo di, bool selectFolder)
        {
            //Expand the folder above, if there is one
            if (di.Parent != null)
            {
                expandFolder(di.Parent, false);
            }
            //now expand the actual folder, which should be exposed. Lat call should be =true(if passed in as original param) and the folder should be selected. 
            tryExpand(FolderBrowser.Items, di.Name, selectFolder );
        }

        public void tryExpand(ItemCollection rootCollection, string name, bool select = false)
        {
            
            //loop through the items and try and expand / populate the item if one exists
            foreach (TreeViewItem i in rootCollection)
            {
                if (i.Header.ToString().ToUpper() == name.ToUpper()) {
                    if (select)
                    {
                        
                        i.IsSelected = true;
                        SetSelected(FolderBrowser, i);
                    
                    }
                    else{i.IsExpanded = true;}
                
                }
                //if it is just our "loading.." message, drop out
                if (!((i.Items.Count == 1) && (i.Items[0] is string)))
                {
                    tryExpand(i.Items, name, select);
                }
            }

            FolderBrowser.UpdateLayout();
        }


        static private bool SetSelected(ItemsControl parent, object child)
        {

            if (parent == null || child == null)
            {
                return false;
            }

            TreeViewItem childNode = parent.ItemContainerGenerator
                .ContainerFromItem(child) as TreeViewItem;

            if (childNode != null)
            {
                childNode.Focus();
                return childNode.IsSelected = true;
            }

            if (parent.Items.Count > 0)
            {
                foreach (object childItem in parent.Items)
                {
                    ItemsControl childControl = parent
                        .ItemContainerGenerator
                        .ContainerFromItem(childItem)
                        as ItemsControl;

                    if (SetSelected(childControl, child))
                    {
                        return true;
                    }
                }
            }

            return false;
        }




        public void populate_Treeview_Drives()
        {


            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo driveInfo in drives)
                FolderBrowser.Items.Add(CreateFolderTreeItem(driveInfo));

        }

        private TreeViewItem CreateFolderTreeItem(object o)
        {
            TreeViewItem item = new TreeViewItem();
            item.Expanded += new RoutedEventHandler(TreeViewItem_Expanded);
            item.Selected += new RoutedEventHandler(TreeViewItem_Selected);
            item.Header = o.ToString();
            item.Tag = o;
            item.Items.Add("Loading...");
            return item;
        }

        public void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = e.Source as TreeViewItem;
            if ((item.Items.Count == 1) && (item.Items[0] is string))
            {
                item.Items.Clear();

                DirectoryInfo expandedDir = null;
                if (item.Tag is DriveInfo)
                    expandedDir = (item.Tag as DriveInfo).RootDirectory;
                if (item.Tag is DirectoryInfo)
                    expandedDir = (item.Tag as DirectoryInfo);
                try
                {
                    foreach (DirectoryInfo subDir in expandedDir.GetDirectories())
                        item.Items.Add(CreateFolderTreeItem(subDir));
                }
                catch { }
            }
        }


        public void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            populateFileList();
        }

        #endregion

        #region fileList
        public void populateFileList()
        {
            TreeViewItem selectedItem = FolderBrowser.SelectedItem as TreeViewItem;
            if (selectedItem != null && selectedItem.Tag is DirectoryInfo)
            {
                DirectoryInfo di = selectedItem.Tag as DirectoryInfo ;
                //foreach (FileInfo f in di.EnumerateFiles()) //todo - restrict to mp3s?
                //{
                    

                //}
                if (chk_recurse.IsChecked == true) 
                {
                    FileList.ItemsSource = di.EnumerateFiles("*", SearchOption.AllDirectories);
                }
                else
                {
                    FileList.ItemsSource = di.EnumerateFiles("*");
                }
            }
        }
        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            refreshCurrentTag();

        }

        /// <summary>
        /// runs the scan in the background, then pops open the window when it finds a file without an image. 
        /// </summary>
        public void backgroundScan()
        {
            if (FileList.Items.Count > 0)
            {
                //select our first file, and scan for a file without an image
                FileList.SelectedIndex = 0;
                if (selectNextItem(true, true, false))
                {
                    this.Show();
                }
                else
                {
                    //no file found
                    this.Close();
                }
            }
            //no files, close
            else
            {
                this.Close();
            }
        }

        /// <summary>
        /// Scans for the next file. Returns false if no files found
        /// </summary>
        /// <param name="missingArt">Only stops when we find a file with missing art</param>
        /// <param name="startAtZero">Check the current file as well</param>
        /// <param name="removeCurrent">Removes the current item from the list first. OVerrides SAZ</param>
        public bool selectNextItem(bool missingArt, bool startAtZero, bool removeCurrent)
        {
            int currentIndex = FileList.SelectedIndex;

            bool firstItem = true;
            bool exit = false;
            bool invalidTag = false;
            

            while (exit == false && currentIndex > -1 && currentIndex < FileList.Items.Count - 1)
            {
                invalidTag = false;
                //Remove the current item if flagged. 
                if (firstItem && removeCurrent)
                {
                    //you can't scan the current file AND remove it. Well you can, but you would be stupid. 
                    startAtZero = false;
                    //we dont really remove it, just refresh the list and have a "fake" index. 
                    populateFileList();
                    currentIndex--;
                }
                
                if (firstItem && startAtZero)
                {
                    currentIndex--;
                }
                firstItem = false;
                currentIndex++;

                //open the file (peek at it, then close, to make sure we don't OOM
                FileInfo fi = FileList.Items[currentIndex] as FileInfo;
                Track peekFile = new Track(fi.FullName) ;
               

                //check for unsupported tags
                try
                {
                    if (peekFile.EmbeddedPictures.Count == 0)
                    {
                        //null test to see if the picture errors. Probably not needed
                    }
                }
                catch (NotImplementedException e)
                {
                    MessageBox.Show("Invalid tag on " + peekFile.Path  + " - " + e.Message);
                    invalidTag = true;
                }
                catch (System.IO.IOException e)
                {
                    MessageBox.Show("IO Error on " + peekFile.Path + " - " + e.Message);
                    invalidTag = true;
                }

                //if we are not looking for artwork, exit
                //or, if we have a valid tag, and it has a pic, exit 

                if (missingArt) //looking for next file with missing artwork
                {
                    //skip invalid tags
                    if (!invalidTag && peekFile.EmbeddedPictures.Count() == 0)
                    {
                        FileList.SelectedIndex = currentIndex;
                        exit = true;
                    }
                }
                else //looking for next valid file
                {
                    if (!invalidTag)
                    {
                        FileList.SelectedIndex = currentIndex;
                        exit = true;
                    }
                }

                
                //peekFile = null;
                //fi = null;
                //GC as the unmanaged code is a memory whore
                GC.Collect(GC.MaxGeneration);
                GC.WaitForPendingFinalizers();

            }
            //return true if we found a file, false if we didnt. 
            return exit;
        }

        
        #endregion

        #region currentTag
        public void refreshCurrentTag()
        {
            //setButtons(true);
            //loop through the selected files and grab the ID3 tags and info

            if (FileList.SelectedItems.Count > 0)
            {
                try
                {
                    FileInfo fi = FileList.SelectedItems[0] as FileInfo;

                    if (audioFile != null) { audioFile = null; }

                    audioFile = new Track(fi.FullName);
                    Tag_File.Text = fi.Name;

                    
                    if ( audioFile.EmbeddedPictures.Count() > 0)
                    {

                        // Get (first) picture 
                        PictureInfo pic  = audioFile.EmbeddedPictures[0];
                        System.Drawing.Image image = System.Drawing.Image.FromStream(new System.IO.MemoryStream(pic.PictureData));
                        SetImageFromBitmap(image, false);
                    }
                    else
                    {
                        Tag_Image.Source = null;
                    }
                    Tag_Album.Text = audioFile.Album;
                    Tag_Artist.Text = audioFile.Artist;
                    Tag_Song.Text = audioFile.Title;
                    imageUpdated = false;
                    tagsUpdated = false;

                    webFrame.Visibility = System.Windows.Visibility.Hidden;

                }
                /*catch (Id3Lib.Exceptions.TagNotFoundException)
                {
                    //drop out if no tag. Should still be able to write one.

                    audioFile = null;
                }*/
                catch (System.IO.IOException)
                {
                    lbl_status.Text = "Error reading file.";
                }
                /*catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }*/

            }
            else
            {
                //todo - lock fields
            }
        }


        private void SetImageFromBitmap(System.Drawing.Image image, bool updateID3)
        {
            
            imageBitmap = new Bitmap(image);
            BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(imageBitmap.GetHbitmap(),
              IntPtr.Zero,
              System.Windows.Int32Rect.Empty,
              BitmapSizeOptions.FromWidthAndHeight(imageBitmap.Width, imageBitmap.Height));
            
            Tag_Image.Source = bs;
            Tag_ImageDims.Text = imageBitmap.Width.ToString() + "x" + imageBitmap.Height.ToString();

            if (updateID3)
            {
                Tag_Image.Tag = image;
                //System.Drawing.Image imageTag = System.Drawing.Image.FromHbitmap(b.GetHbitmap(), IntPtr.Zero);
                //Tag_Image.Tag = imageTag;
                imageUpdated = true;
            }
        }

        private bool SetImageFromUri(Uri uri)
        {
            string fileName = System.IO.Path.GetTempFileName();
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    
 //                   webClient.Headers.Add("user-agent", userAgent);

                    byte[] data = webClient.DownloadData(uri);
                    MemoryStream ms = new MemoryStream(data);
                    System.Drawing.Image image = System.Drawing.Image.FromStream(ms);
                    SetImageFromBitmap(image, true);
                    return true;
                }
            }
            catch (Exception e)
            {
                lbl_status.Text = "Error grabbing image: " + e.ToString();
                return false;
            }
        }




        private void Tag_Image_Drop(object sender, System.Windows.DragEventArgs e)
        {
            //should be a URL or a file URI
            //http://stackoverflow.com/questions/8442085/receiving-an-image-dragged-from-web-page-to-wpf-window

            System.Windows.IDataObject data = e.Data;
            string[] formats = data.GetFormats();

            object obj = null;
            bool found = false;

            if (formats.Contains("text/html") )
            {

                obj = data.GetData("text/html");
                found = true;
            }
            else if (formats.Contains("HTML Format") )
            {
                obj = data.GetData("HTML Format");
                found = true;
            }
            if (found)
            {
                
                string html = string.Empty;
                if (obj is string)
                {
                    html = (string)obj;
                    //remove any whitespace, linebreaks
                    html = html.Replace(" ", "").Replace("\r\n","");

                }
                else if (obj is MemoryStream)
                {
                    MemoryStream ms = (MemoryStream)obj;
                    byte[] buffer = new byte[ms.Length];
                    ms.Read(buffer, 0, (int)ms.Length);
                    if (buffer[1] == (byte)0)  // Detecting unicode
                    {
                        html = System.Text.Encoding.Unicode.GetString(buffer);
                    }
                    else
                    {
                        html = System.Text.Encoding.ASCII.GetString(buffer);
                    }
                }
                // Using a basic regex to parse HTML
                var match = new Regex(@"<img[^>]*src=""([^""]*)""", RegexOptions.IgnoreCase).Match(html);
                if (match.Success)
                {
                    Uri uri = new Uri(match.Groups[1].Value);
                    SetImageFromUri(uri);
                }
                else
                {
                    // Try look for a URL to an image, encoded (thanks google image search....)
                    match = new Regex(@"(url|src)=[""]?(.*?)(&|"")").Match(html);
                    //url=http%3A%2F%2Fi.imgur.com%2FK1lxb2L.jpg&amp
                    if (match.Success)
                    {
                        bool successImg = false;
                        int i = 0;
                        while (i < match.Groups.Count && successImg == false)
                        {
                            i++;
                            try
                            {
                                Uri uri = new Uri(Uri.UnescapeDataString(match.Groups[i].Value));
                                successImg = SetImageFromUri(uri);
                            }
                            catch (Exception)
                            {
                                //probably nothing to worry about...
                            }
                        }
                    }

                    //Samples: 
                    //< IMG width = "432" height = "432" id = "irc_mi" style = "margin-top: 0px;" onload = "typeof google==='object'&amp;&amp;google.aft&amp;&amp;google.aft(this)" alt = "Image result for Aperio  Mindfield   Seasons Changing" src = "https://i1.sndcdn.com/artworks-000298160676-shcuz7-t500x500.jpg" ></ A >< !--EndFragment-- ></ DIV ></ BODY ></ HTML >
                    //<IMGclass="n3VNCb"style="margin:0px;width:585px;height:585px;"alt="Identities2(2020,File)|Discogs"src="https://img.discogs.com/wzI0NTRfX35BLMsrm-c1gFSY-qU=/fit-in/600x600/filters:strip_icc():format(jpeg):mode_rgb():quality(90)/discogs-images/R-15423379-1591302400-6122.jpeg.jpg


                }
            }
        }


        private void btnReloadTag_Click(object sender, RoutedEventArgs e)
        {
            refreshCurrentTag();
        }

        public void saveTags()
        {
            if (audioFile == null)
            {
                lbl_status.Text = "No file is currently selected!";
                return;
            }

            if (imageUpdated)
            {
                System.Drawing.Image image = Tag_Image.Tag as System.Drawing.Image;
                //Bitmap b = Tag_Image.Tag as Bitmap;

                //Remove all images. 
                //TODO - removing all images might be a bit aggressive.
                audioFile.EmbeddedPictures.Clear();

                //add the new image (bitmap format?)
                System.IO.MemoryStream ms = new MemoryStream();
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                PictureInfo newPicture = PictureInfo.fromBinaryData(ms.ToArray(), PictureInfo.PIC_TYPE.CD);
                audioFile.EmbeddedPictures.Add(newPicture);
            }

            if (tagsUpdated)
            {
                audioFile.Album = Tag_Album.Text;
                audioFile.Artist = Tag_Artist.Text;
                audioFile.Title = Tag_Song.Text;
            }
            
            //check our paths exist and files don't before we start. 
            string bakName = System.IO.Path.ChangeExtension(audioFile.Path, "bak");
            FileInfo bakfile = new FileInfo(bakName);
            string backupLocation = backupDirectory.FullName + @"\" + System.IO.Path.GetFileName(audioFile.Path);

            if (backupDirectory.Exists == false)
            {
                lbl_status.Text = "Backup Directory does not exist!";
                return;
            }
            //if it's null, it was never set, therefore we dont move the file. If its !exists, it was set, but isnt a valid folder. 
            if (destinationDirectory != null && destinationDirectory.Exists == false)
            {
                lbl_status.Text = "Destination Directory does not exist!";
                return;
            }


            //save the file. Creates a .bak in the source dir
            audioFile.Save();

            //move / delete any bak files                
            if (bakfile.Exists)
            {
                if (chk_Backup.IsChecked == true)
                {
                    bakfile.MoveTo(backupLocation);
                }
                else
                {
                    bakfile.Delete();
                }
            }


            //move file to output folder (if selected)
            //if it's null, it was never set, therefore we dont move the file. If its !exists, it was set, but isnt a valid folder. 
            if (destinationDirectory != null)
            {
                FileInfo fi = new FileInfo(audioFile.Path);
                audioFile = null;
                fi.MoveTo(destinationDirectory.FullName + "\\" + fi.Name);
                    
                   
            }
        }

        #endregion


        #region backups

        private void btnPickFolder_Click(object sender, RoutedEventArgs e)
        {
            WinForms.FolderBrowserDialog fb = new WinForms.FolderBrowserDialog();
            if (backupDirectory != null) { fb.SelectedPath = backupDirectory.FullName; }
            fb.Description = "Select a backup location";
            WinForms.DialogResult r = fb.ShowDialog();
            if (fb.SelectedPath != null) 
            { 
                backupDirectory = new DirectoryInfo(fb.SelectedPath);
                txtBackupFolder.Text = backupDirectory.FullName;
            }

        }



        #endregion





        private void btn_Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnGuessTag_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btn_Search_File_Click(object sender, RoutedEventArgs e)
        {
            search_fileName();
        }

        private void search_fileName()
        {
            string filename = "";
            //drop the extension
            var match = new Regex(@"(.*)\..+").Match(Tag_File.Text);
            if (match.Success)
            {
                filename = match.Groups[1].Value;
            }

            doSearch(filename);

        }

        private void doSearch(string searchString)
        {
            
            
            //remove ampersands (break URLs)
            searchString = searchString.Replace("&", "");
            searchString = searchString.Replace("_", " ");
            //remove hyphens - google assumes it is to ignore next term
            searchString = searchString.Replace("-", " ");

            //string URL = "https://www.google.co.uk/search?safe=off&source=lnms&tbm=isch&tbs=imgo:1&q=";

            string URL = "https://www.google.com/search?safe=off&tbm=isch&q=";

            URL += Uri.EscapeUriString(searchString);

            //string cleaned = 
            //            string URL = "https://www.google.co.uk/search?q=" + Tag_File.Text.Replace(" ","+") +"&safe=off&source=lnms&tbm=isch";

            if (chkEmbedSearch.IsChecked == true)
            {
                webFrame.Visibility = System.Windows.Visibility.Visible;
                

                //HideScriptErrors(webFrame, true);

                //webFrame .CoreWebView2.Settings.UserAgent = userAgent;
                webFrame.Load(URL);//, null, null, "User-Agent: " + userAgent
                //webFrame.Source = new Uri(URL);
            }
            else
            {
                System.Diagnostics.Process.Start(URL);
            }

        }

        /*public void HideScriptErrors(WebView2 wb, bool Hide)
        {
            FieldInfo fiComWebBrowser = typeof(WebBrowser)
                .GetField("_axIWebBrowser2",
                          BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiComWebBrowser == null) return;
            object objComWebBrowser = fiComWebBrowser.GetValue(wb);
            if (objComWebBrowser == null) return;
            objComWebBrowser.GetType().InvokeMember(
                "Silent", BindingFlags.SetProperty, null, objComWebBrowser,
                new object[] { Hide });
        }*/

    /// <summary>
    /// Stuff some extra js on the end of a call to stop any javascript errors
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    void Browser_OnLoadCompleted(object sender, NavigationEventArgs e)
        {
            var browser = sender as WebBrowser;

            if (browser == null || browser.Document == null)
                return;

            dynamic document = browser.Document;

            if (document.readyState != "complete")
                return;

            dynamic script = document.createElement("script");
            script.type = @"text/javascript";
            script.text = @"window.onerror = function(msg,url,line){return true;}";
            document.head.appendChild(script);
        }

        #region rightbuttons

        private void btnSaveTag_Click(object sender, RoutedEventArgs e)
        {
            //remove the accidental presses due to the goddawfull handling of focus in the webbrowser
            if (validPress())
            {
                saveTags();
                autoSearch();
            }
        }

        private void btn_SaveNextEmpty_Click(object sender, RoutedEventArgs e)
        {
            //remove the accidental presses due to the goddawfull handling of focus in the webbrowser
            if (validPress())
            {
                saveTags();
                //we've moved the original, so remove from the list
                int currentIndex = FileList.SelectedIndex;
                FileList.SelectedIndex = currentIndex;
                selectNextItem(true, false, true);
                autoSearch();
            }
        }

        private void btnNextEmpty_Click(object sender, RoutedEventArgs e)
        {
            //remove the accidental presses due to the goddawfull handling of focus in the webbrowser
            if (validPress())
            {
                selectNextItem(true, false, false);
                autoSearch();
            }
        }

        private void btn_SaveAndNext_Click(object sender, RoutedEventArgs e)
        {
            //remove the accidental presses due to the goddawfull handling of focus in the webbrowser
            if (validPress())
            {
                saveTags();
                selectNextItem(false, false, true);
                autoSearch();
            }
        }

        private void autoSearch()
        {
            if (chk_autosearch_file.IsChecked == true) { search_fileName(); }
        }


        private bool validPress()
        {
            if (WinControl.ModifierKeys == WinForms.Keys.Alt || WinControl.MouseButtons == WinForms.MouseButtons.Left)
            {
                return true;
            }
            if (!webFrame.IsFocused)
            {
                return true;
            }

            return false;
        }

        private void btn_OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem selectedItem = FolderBrowser.SelectedItem as TreeViewItem;
            if (selectedItem != null && selectedItem.Tag is DirectoryInfo)
            {
                DirectoryInfo di = selectedItem.Tag as DirectoryInfo;
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    Arguments = di.FullName,
                    FileName = "explorer.exe"
                };

            Process.Start(startInfo);
        }

    }





        #endregion


    }
}
