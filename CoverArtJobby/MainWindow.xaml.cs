using System;
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
using System.IO;
using System.Web;
using System.Net;
using Id3Lib;
using Mp3Lib;

namespace CoverArtJobby
{
    //test commit2
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool imageUpdated = false;
        private bool tagsUpdated = false;
        private bool browserHasFocus = false;
        public bool runInBackground = false;
        Mp3File file = null;

        //class var to prevent memory leakage
        private Bitmap imageBitmap;

        //various directories 
        public DirectoryInfo backupDirectory = null;
        public DirectoryInfo scanDirectory = null;
        public DirectoryInfo destinationDirectory = null;

        public MainWindow()
        {
            this.Hide();
            InitializeComponent();
            

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
                if (selectNextItem(true, true))
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
        /// <param name="startAtOne">Check the current file as well</param>
        public bool selectNextItem(bool missingArt, bool startAtZero)
        {
            int currentIndex = FileList.SelectedIndex;

            bool firstItem = true;
            bool exit = false;
            bool invalidTag = false;
            while(exit == false && currentIndex > -1 && currentIndex < FileList.Items.Count - 1)
            {
                invalidTag = false;
                if (firstItem && startAtZero)
                {
                    currentIndex--;
                }
                firstItem = false;
                currentIndex++;
                //open the file (peek at it, then close, to make sure we don't OOM
                FileInfo fi = FileList.Items[currentIndex] as FileInfo;
                Mp3File peekFile = new Mp3File(fi);
               

                //check for unsupported tags
                try
                {
                    if (peekFile.TagHandler.Picture == null)
                    {
                        //null test to see if the picture errors. Probably not needed
                    }
                }
                catch (NotImplementedException e)
                {
                    MessageBox.Show("Invalid tag on " + peekFile.FileName + " - " + e.Message);
                    invalidTag = true;
                }

                //if we are not looking for artwork, exit
                //or, if we have a valid tag, and it has a pic, exit 
                
                if (missingArt) //looking for next file with missing artwork
                {
                    //skip invalid tags
                    if (!invalidTag && peekFile.TagHandler.Picture == null)
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

                    if (file != null) { file = null; }

                    file = new Mp3File(fi);
                    Tag_File.Text = fi.Name;

                    
                    if (file.TagHandler.Picture != null)
                    {
                        SetImageFromBitmap(file.TagHandler.Picture, false);
                    }
                    else
                    {
                        Tag_Image.Source = null;
                    }
                    Tag_Album.Text = file.TagHandler.Album;
                    Tag_Artist.Text = file.TagHandler.Artist;
                    
                    Tag_Song.Text = file.TagHandler.Song;
                    imageUpdated = false;
                    tagsUpdated = false;

                    webFrame.Visibility = System.Windows.Visibility.Hidden;

                }
                catch (Id3Lib.Exceptions.TagNotFoundException)
                {
                    //drop out if no tag. Should still be able to write one.

                    file = null;
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }

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
                    byte[] data = webClient.DownloadData(uri);
                    MemoryStream ms = new MemoryStream(data);
                    System.Drawing.Image image = System.Drawing.Image.FromStream(ms);
                    SetImageFromBitmap(image, true);
                    return true;
                }
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.ToString());
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
                // Using a regex to parse HTML, but JUST FOR THIS EXAMPLE :-)
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

                    //< IMG width = "432" height = "432" id = "irc_mi" style = "margin-top: 0px;" onload = "typeof google==='object'&amp;&amp;google.aft&amp;&amp;google.aft(this)" alt = "Image result for Aperio  Mindfield   Seasons Changing" src = "https://i1.sndcdn.com/artworks-000298160676-shcuz7-t500x500.jpg" ></ A >< !--EndFragment-- ></ DIV ></ BODY ></ HTML >


                }
            }
        }


        private void btnReloadTag_Click(object sender, RoutedEventArgs e)
        {
            refreshCurrentTag();
        }

        public void saveTags()
        {
            if (imageUpdated)
            {

                System.Drawing.Image image = Tag_Image.Tag as System.Drawing.Image;
                //Bitmap b = Tag_Image.Tag as Bitmap;
                file.TagHandler.Picture = image;
            }

            if (tagsUpdated)
            {
                file.TagHandler.Album = Tag_Album.Text;
                file.TagHandler.Artist = Tag_Artist.Text;
                file.TagHandler.Song = Tag_Song.Text;
            }
            try
            {

                file.Update();

                //move / delete any bak files
                string bakName = System.IO.Path.ChangeExtension(file.FileName, "bak");
                FileInfo bakfile = new FileInfo(bakName);
                if (bakfile.Exists)
                {
                    if (chk_Backup.IsChecked == true)
                    {
                        string location = backupDirectory.FullName + @"\" + System.IO.Path.GetFileName(file.FileName);
                        bakfile.MoveTo(location);
                    }
                    else
                    {
                        bakfile.Delete();
                    }
                }

                
                //move file to output folder (if selected)
                if (destinationDirectory != null)
                {
                    FileInfo fi = new FileInfo(file.FileName);
                    file = null;
                    fi.MoveTo(destinationDirectory.FullName + "\\" + fi.Name);
                }



            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
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

            string URL = "https://www.google.co.uk/search?safe=off&source=lnms&tbm=isch&tbs=imgo:1&q=";
            URL += Uri.EscapeUriString(searchString);

            //string cleaned = 
            //            string URL = "https://www.google.co.uk/search?q=" + Tag_File.Text.Replace(" ","+") +"&safe=off&source=lnms&tbm=isch";

            if (chkEmbedSearch.IsChecked == true)
            {
                webFrame.Visibility = System.Windows.Visibility.Visible;
                webFrame.Source = new Uri(URL);
            }
            else
            {
                System.Diagnostics.Process.Start(URL);
            }

        }

        #region righbuttons

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
                selectNextItem(true, false);
                autoSearch();
            }
        }

        private void btnNextEmpty_Click(object sender, RoutedEventArgs e)
        {
            //remove the accidental presses due to the goddawfull handling of focus in the webbrowser
            if (validPress())
            {
                selectNextItem(true, false);
                autoSearch();
            }
        }

        private void btn_SaveAndNext_Click(object sender, RoutedEventArgs e)
        {
            //remove the accidental presses due to the goddawfull handling of focus in the webbrowser
            if (validPress())
            {
                saveTags();
                selectNextItem(false, false);
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












        #endregion
    /*    //FML
        private void webFrame_GotFocus(object sender, RoutedEventArgs e)
        {
            browserHasFocus = true;
            setButtons(true);
        }



        private void webFrame_LostFocus(object sender, RoutedEventArgs e)
        {
            browserHasFocus = false;
            setButtons(false);
        }

        private void setButtons(bool enable)
        {
            btn_SaveAndNext.IsEnabled = enable;
            btn_SaveNextEmpty.IsEnabled = enable;
            btnNextEmpty.IsEnabled = enable;
            btnSaveTag.IsEnabled = enable;
        }*/
    }
}
