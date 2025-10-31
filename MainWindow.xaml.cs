using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using YouTubeDownloader.Models;
using YouTubeDownloader.Services;

namespace YouTubeDownloader
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<YouTubeVideo> videoList = new();
        private readonly VideoService videoService;
        private readonly SearchService searchService;

        private ICollectionView? view;

        public MainWindow()
        {
            InitializeComponent();
            videoService = new VideoService();
            searchService = new SearchService(videoList);

            SearchBox.Focus();
            Directory.CreateDirectory(videoService.DownloadFolder);

            ResultsList.ItemsSource = videoList;
            view = CollectionViewSource.GetDefaultView(videoList);
            view.SortDescriptions.Add(new SortDescription(nameof(YouTubeVideo.view_count), ListSortDirection.Descending));

            OpenFolderButton.IsEnabled = Directory.Exists(videoService.DownloadFolder);
            OpenFolderButton.Opacity = OpenFolderButton.IsEnabled ? 1.0 : 0.5;

            SetInteractiveUI(false);
            ResultsList.MouseDoubleClick += ResultsList_MouseDoubleClick;
            AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(GridViewColumnHeader_Click));
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (searchService.IsSearching)
            {
                searchService.StopSearch(StatusText, ResetSearchButton, SetInteractiveUI);
                return;
            }

            string query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                StatusText.Text = "Please enter a query.";
                return;
            }

            SetSearchButtonToStop();

            searchService.StartSearch(query, StatusText, SetInteractiveUI, ResetSearchButton);
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is YouTubeVideo video)
                videoService.DownloadAudio(video, StatusText);
        }

        private void DownloadVideoButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is YouTubeVideo video)
                videoService.DownloadVideo(video, StatusText);
        }

        private void PlayButtonVideo_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is YouTubeVideo video)
                videoService.PlayVideo(video, StatusText);
        }

        private void PlayButtonAudio_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is YouTubeVideo video)
                videoService.PlayAudio(video, StatusText);
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            videoService.OpenDownloadFolder();
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsList.SelectedItem is YouTubeVideo video)
                videoService.OpenInBrowser(video, StatusText);
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
            {
                string sortBy = header.Column.Header switch
                {
                    string h when h.Contains("Title") => nameof(YouTubeVideo.title),
                    string h when h.Contains("Channel") => nameof(YouTubeVideo.uploader),
                    string h when h.Contains("Views") => nameof(YouTubeVideo.view_count),
                    string h when h.Contains("Date") => nameof(YouTubeVideo.upload_date),
                    _ => null
                };

                if (sortBy == null) return;

                if (view!.SortDescriptions.Count > 0 && view.SortDescriptions[0].PropertyName == sortBy)
                {
                    var current = view.SortDescriptions[0];
                    view.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new SortDescription(sortBy, current.Direction == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending));
                }
                else
                {
                    view!.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new SortDescription(sortBy, ListSortDirection.Descending));
                }

                view.Refresh();
            }
        }

        private void SetInteractiveUI(bool enabled)
        {
            bool hasSelection = enabled && ResultsList.SelectedItem is YouTubeVideo;

            SetButtonState(PlayVideoButton, hasSelection);
            SetButtonState(PlayAudioButton, hasSelection);
            SetButtonState(DownloadButton, hasSelection);
            SetButtonState(DownloadVideoButton, hasSelection);

            ResultsContainer.IsEnabled = enabled;
            ResultsContainer.Opacity = enabled ? 1.0 : 0.5;
        }

        private void SetButtonState(Button button, bool enabled)
        {
            button.IsEnabled = enabled;
            button.Opacity = enabled ? 1.0 : 0.5;
        }

        private void ResetSearchButton()
        {
            SearchButton.Content = "🔎 Search";
            SearchButton.ClearValue(Button.BackgroundProperty);
            SearchButton.ClearValue(Button.ForegroundProperty);

            // Re-enable the interface only when the list has items
            bool hasResults = videoList.Count > 0;
            SetInteractiveUI(hasResults);
        }

        private void SetSearchButtonToStop()
        {
            SearchButton.Content = "⛔ Stop";
            SearchButton.Background = Brushes.IndianRed;
            SearchButton.Foreground = Brushes.White;
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, e);
            }
        }

        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetInteractiveUI(true);
        }
    }
}