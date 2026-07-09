using DeezFiles.Models;
using DeezFiles.Services;
using DeezFiles.Utilities;
using DNStore.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
namespace DeezFiles
{
    public partial class MainPage : Page
    {
        private ObservableCollection<FileItem> Files { get; } = new ObservableCollection<FileItem>();

        // Constructor
        public MainPage()
        {
            InitializeComponent();
            this.Loaded += MainPage_Loaded; // Register the Loaded event
        }

        /// <summary>
        /// Reads filestate.json (if it exists & is non‐empty) and rebuilds the rows in the UI.
        /// </summary>
        private async Task LoadFileStateAndPopulateAsync()
        {
            try
            {
                await LocalFileHelper.SyncFileManifestsFromServerAsync();

                // 1) Clear any existing rows
                RowsPanel.Children.Clear();

                // 2) Compute path to your JSON
                string jsonFilePath = Path.Combine(LocalFileHelper.statePath, "filestate.json"); // Stringed out so that it will work on my system where statepath is not declared or idk
                //string jsonFilePath = "C:\\Users\\user\\OneDrive\\Documents\\DNStore\\DN_Test\\state\\filestate.json";

                // 3) If the file isn't there, bail out
                if (!File.Exists(jsonFilePath))
                {
                    TotalFilesCount.Text = "Total Files: 0";
                    UploadSizeTotal.Text = FormatFileSize(0);
                    return;
                }


                // 4) Read the entire file
                string jsonData = await File.ReadAllTextAsync(jsonFilePath);

                // 5) If the file is empty or whitespace, do nothing
                if (string.IsNullOrWhiteSpace(jsonData))
                {
                    TotalFilesCount.Text = "Total Files: 0";
                    UploadSizeTotal.Text = FormatFileSize(0);
                    return;
                }

                // 6) Deserialize into a dictionary
                var fileStateDict = JsonSerializer
                    .Deserialize<Dictionary<string, JsonElement>>(jsonData);

                ulong totalSize = 0;

                // 7) For each entry, extract fields & add a row
                foreach (var kvp in fileStateDict)
                {
                    string fileName = kvp.Key;
                    var fileData = kvp.Value;

                    DateTime uploadTime = fileData.GetProperty("UploadTime").GetDateTime();
                    ulong fileSize = fileData.GetProperty("Size").GetUInt64();
                    totalSize += fileSize;

                    string formattedSize = FormatFileSize(fileSize);

                    // Build the UI row and add it
                    var row = CreateFileRow(fileName, uploadTime, formattedSize);
                    RowsPanel.Children.Add(row);
                }

                // 8) Update your totals labels
                TotalFilesCount.Text = $"Total Files: {fileStateDict.Count}";
                UploadSizeTotal.Text = FormatFileSize(totalSize);
            }
            catch (Exception ex)
            {
                // Log any problems but don’t crash the UI
                System.Diagnostics.Debug.WriteLine($"Error loading filestate.json: {ex}");
            }
        }

        public Grid CreateFileRow(string fileName, DateTime uploadTime, string size)
        {
            var row = new Grid
            {
                Margin = new Thickness(0, 2, 0, 2),
                MinHeight = 26,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star), MinWidth = 130 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 75 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star), MinWidth = 105 });

            // File Name - Changed to White
            var tbName = new TextBlock
            {
                Margin = new Thickness(30, 0, 8, 0),
                Text = fileName,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = new SolidColorBrush(Colors.White) // Added white color
            };
            Grid.SetColumn(tbName, 0);
            row.Children.Add(tbName);

            // Date - Changed to White
            var tbDate = new TextBlock
            {
                Margin = new Thickness(4, 0, 4, 0),
                Text = uploadTime.ToString("MM/dd/yyyy HH:mm"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White) // Added white color
            };
            Grid.SetColumn(tbDate, 1);
            row.Children.Add(tbDate);

            // Size - Changed to White
            var tbSize = new TextBlock
            {
                Margin = new Thickness(4, 0, 4, 0),
                Text = size,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White) // Added white color
            };
            Grid.SetColumn(tbSize, 2);
            row.Children.Add(tbSize);

            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var btnDownload = new Button
            {
                Width = 28,
                Height = 24,
                Padding = new Thickness(4),
                Margin = new Thickness(0, 0, 8, 0),
                Tag = fileName, // store file name to identify which file to download
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "Download",
                Content = new Image
                {
                    Source = new BitmapImage(new Uri("/Assets/Download.png", UriKind.Relative)),
                    Width = 16,
                    Height = 16,
                    Stretch = Stretch.Fill
                }
            };
            btnDownload.Click += Download_Click;
            actionPanel.Children.Add(btnDownload);

            var btnDelete = new Button
            {
                Width = 28,
                Height = 24,
                Padding = new Thickness(4),
                Margin = new Thickness(0),
                Tag = fileName,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "Delete",
                Content = new TextBlock
                {
                    Text = "\uE74D",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 144, 144)),
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            btnDelete.Click += Delete_Click;
            actionPanel.Children.Add(btnDelete);

            Grid.SetColumn(actionPanel, 3);
            row.Children.Add(actionPanel);

            return row;
        }


        // Loaded event handler
        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            string addinfo = LocalFileHelper.GetDNETaddress(AuthorizationService.currentUsername);
            string[] add = addinfo.Split(":");
            UserID.Text = add[0];
            UserAdd.Text = "@" + add[1].ToLower();
            Blockchain.InitializeBlockchainAsync();
            UpdateFileList();
            LocalFileHelper.FileListUpdated += OnFileListUpdated;
            LocalFileHelper.DownloadCompleted += OnDownloadCompleted;
            LocalFileHelper.DownloadFailed += OnDownloadFailed;
        }

        private void OnFileListUpdated(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                await UpdateFileListAsync();
            }), DispatcherPriority.Normal);
        }

        private async Task UpdateFileListAsync()
        {
            try
            {
                await LocalFileHelper.SyncFileManifestsFromServerAsync();

                // Clear previous rows
                RowsPanel.Children.Clear();

                string jsonFilePath = Path.Combine(LocalFileHelper.statePath, "filestate.json");

                if (File.Exists(jsonFilePath))
                {
                    string jsonData = await File.ReadAllTextAsync(jsonFilePath);

                    if (!string.IsNullOrEmpty(jsonData))
                    {
                        var fileStateDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonData);

                        ulong totalsize = 0;

                        foreach (var kvp in fileStateDict)
                        {
                            string fileName = kvp.Key;
                            var fileData = kvp.Value;

                            DateTime uploadTime = fileData.GetProperty("UploadTime").GetDateTime();
                            ulong fileSize = fileData.GetProperty("Size").GetUInt64();
                            totalsize += fileSize;

                            string formattedSize = FormatFileSize(fileSize);

                            // Create row UI for this file
                            var row = CreateFileRow(fileName, uploadTime, formattedSize);

                            RowsPanel.Children.Add(row);
                        }

                        // Update totals
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            TotalFilesCount.Text = "Total Files: " + fileStateDict.Count.ToString();
                            UploadSizeTotal.Text = FormatFileSize(totalsize);
                        }), DispatcherPriority.Normal);
                    }
                }

                if (RowsPanel.Children.Count == 0)
                {
                    TotalFilesCount.Text = "Total Files: 0";
                    UploadSizeTotal.Text = FormatFileSize(0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating file list: {ex.Message}");
            }
        }


        private void UpdateFileList()
        {
            _ = UpdateFileListAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await UpdateFileListAsync();
        }

        private string FormatFileSize(ulong bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }

            return string.Format("{0:n1}{1}", number, suffixes[counter]);
        }

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            LocalFileHelper.FileListUpdated -= OnFileListUpdated;
            LocalFileHelper.DownloadCompleted -= OnDownloadCompleted;
            LocalFileHelper.DownloadFailed -= OnDownloadFailed;
            AuthorizationService.Logout();
            this.NavigationService.Navigate(new Uri("Views/LoginPage.xaml", UriKind.RelativeOrAbsolute));

        }


        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    await FileHelper.UploadFile(openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("UPLOAD ERROR: " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Updates the total files count and total size display
        /// </summary>
        private void UpdateTotals()
        {
            try
            {
                // Count current rows in the UI
                int totalFiles = RowsPanel.Children.Count;

                // Calculate total size from all rows
                ulong totalSize = 0;

                foreach (var child in RowsPanel.Children)
                {
                    if (child is Grid row)
                    {
                        // Find the size TextBlock (it's in column 2)
                        foreach (var gridChild in row.Children)
                        {
                            if (gridChild is TextBlock tb && Grid.GetColumn(tb) == 2)
                            {
                                // Parse the size text back to bytes for accurate totaling
                                totalSize += ParseSizeToBytes(tb.Text);
                                break;
                            }
                        }
                    }
                }

                // Update the UI labels
                TotalFilesCount.Text = $"Total Files: {totalFiles}";
                UploadSizeTotal.Text = FormatFileSize(totalSize);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating totals: {ex}");
            }
        }

        /// <summary>
        /// Converts formatted size string back to bytes (helper for UpdateTotals)
        /// </summary>
        private ulong ParseSizeToBytes(string sizeText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sizeText))
                    return 0;

                // Remove any spaces and get the numeric part and suffix
                sizeText = sizeText.Trim();

                string numericPart = "";
                string suffix = "";

                for (int i = 0; i < sizeText.Length; i++)
                {
                    if (char.IsDigit(sizeText[i]) || sizeText[i] == '.')
                    {
                        numericPart += sizeText[i];
                    }
                    else
                    {
                        suffix = sizeText.Substring(i);
                        break;
                    }
                }

                if (!decimal.TryParse(numericPart, out decimal number))
                    return 0;

                // Convert back to bytes based on suffix
                switch (suffix.ToUpper())
                {
                    case "B":
                        return (ulong)number;
                    case "KB":
                        return (ulong)(number * 1024);
                    case "MB":
                        return (ulong)(number * 1024 * 1024);
                    case "GB":
                        return (ulong)(number * 1024 * 1024 * 1024);
                    case "TB":
                        return (ulong)(number * 1024 * 1024 * 1024 * 1024);
                    default:
                        return (ulong)number; // Assume bytes if no suffix
                }
            }
            catch
            {
                return 0;
            }
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton && clickedButton.Tag is string fileName)
            {
                await FileHelper.DownloadFile(fileName);
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clickedButton || clickedButton.Tag is not string fileName)
                return;

            var confirmation = MessageBox.Show(
                $"Delete '{fileName}' from your uploaded files?\nThis will remove its local metadata and local shard copies.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            await FileHelper.DeleteFile(fileName);
        }

        private void OnDownloadCompleted(string savedPath)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(
                    $"Download completed.\nSaved to:\n{savedPath}",
                    "Download Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }));
        }

        private void OnDownloadFailed(string errorMessage)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(
                    $"Download could not be completed.\n{errorMessage}",
                    "Download Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }));
        }


        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void FileList_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
