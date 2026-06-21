using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ClipboardManager.Services;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using System.IO;
using System;

namespace ClipboardManager;

public sealed partial class MainPage : Page
{
    private StorageManager _storageManager;
    private ClipboardMonitor _clipboardMonitor;
    
    private List<ClipboardItem> _allItems = new();
    public ObservableCollection<ClipboardItem> FilteredItems { get; } = new();

    public MainPage()
    {
        InitializeComponent();

        _storageManager = new StorageManager();
        _clipboardMonitor = new ClipboardMonitor(_storageManager);
        
        LoadHistory();

        _clipboardMonitor.OnItemAdded += (s, item) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _allItems.Insert(0, item);
                RefreshFilteredList();
            });
        };
        _clipboardMonitor.Start();
    }

    private void LoadHistory()
    {
        _allItems = _storageManager.GetItems(50);
        RefreshFilteredList();
    }

    private void RefreshFilteredList()
    {
        if (SearchBox == null || FilterComboBox == null || SortToggleButton == null) return;

        var query = _allItems.AsEnumerable();

        // 1. Search Filter
        var searchText = SearchBox.Text?.ToLower() ?? "";
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(i => i.PreviewSnippet.ToLower().Contains(searchText));
        }

        // 2. Type Filter
        if (FilterComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag != "All")
        {
            if (Enum.TryParse<ClipboardDataType>(tag, out var dataType))
            {
                query = query.Where(i => i.DataType == dataType);
            }
        }

        // 3. Sorting
        bool oldestFirst = SortToggleButton.IsChecked == true;
        if (oldestFirst)
        {
            query = query.OrderBy(i => i.Timestamp);
        }
        else
        {
            query = query.OrderByDescending(i => i.Timestamp);
        }

        FilteredItems.Clear();
        foreach (var qItem in query)
        {
            FilteredItems.Add(qItem);
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            RefreshFilteredList();
        }
    }

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshFilteredList();
    }

    private void SortToggleButton_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (SortToggleButton != null)
        {
            SortToggleButton.Content = SortToggleButton.IsChecked == true ? "Newest First" : "Oldest First";
            RefreshFilteredList();
        }
    }

    private void ClipboardListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Selection no longer automatically copies. Copying is done via CopyButton_Click.
    }

    private async void Expander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (sender.DataContext is ClipboardItem item)
        {
            if (string.IsNullOrEmpty(item.FullText))
            {
                if (item.DataType == ClipboardDataType.Text)
                {
                    if (!string.IsNullOrEmpty(item.DiskFilePath) && File.Exists(item.DiskFilePath))
                    {
                        item.FullText = await File.ReadAllTextAsync(item.DiskFilePath);
                    }
                    else
                    {
                        item.FullText = item.PreviewSnippet;
                    }
                }
                else if (item.DataType == ClipboardDataType.Image)
                {
                    if (item.ImagePreview == null && !string.IsNullOrEmpty(item.DiskFilePath) && File.Exists(item.DiskFilePath))
                    {
                        item.ImagePreview = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(item.DiskFilePath));
                    }
                    item.FullText = string.Empty;
                }
                else if (item.DataType == ClipboardDataType.FileDrop)
                {
                    item.FullText = $"File Path: {item.DiskFilePath}";
                }
            }
        }
    }

    private async void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ClipboardItem item)
        {
            try
            {
                var dataPackage = new DataPackage();
                bool hasContent = false;
                
                if (item.DataType == ClipboardDataType.Text)
                {
                    string textToCopy = item.PreviewSnippet;
                    if (!string.IsNullOrEmpty(item.DiskFilePath) && File.Exists(item.DiskFilePath))
                    {
                        textToCopy = await File.ReadAllTextAsync(item.DiskFilePath);
                    }
                    dataPackage.SetText(textToCopy);
                    hasContent = true;
                }
                else if (item.DataType == ClipboardDataType.Image)
                {
                    if (!string.IsNullOrEmpty(item.DiskFilePath) && File.Exists(item.DiskFilePath))
                    {
                        var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(item.DiskFilePath);
                        var streamRef = Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(storageFile);
                        dataPackage.SetBitmap(streamRef);
                        hasContent = true;
                    }
                }
                else if (item.DataType == ClipboardDataType.FileDrop)
                {
                    if (!string.IsNullOrEmpty(item.DiskFilePath))
                    {
                        if (File.Exists(item.DiskFilePath))
                        {
                            var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(item.DiskFilePath);
                            dataPackage.SetStorageItems(new List<Windows.Storage.IStorageItem> { storageFile });
                            hasContent = true;
                        }
                        else if (Directory.Exists(item.DiskFilePath))
                        {
                            var storageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(item.DiskFilePath);
                            dataPackage.SetStorageItems(new List<Windows.Storage.IStorageItem> { storageFolder });
                            hasContent = true;
                        }
                    }
                }
                
                if (hasContent)
                {
                    _clipboardMonitor.IgnoreNextChange = true;
                    Clipboard.SetContent(dataPackage);
                    Clipboard.Flush();
                }
            }
            catch 
            {
                _clipboardMonitor.IgnoreNextChange = false;
            }
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string id)
        {
            _storageManager.DeleteItem(id);
            var item = _allItems.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                _allItems.Remove(item);
                FilteredItems.Remove(item);
            }
        }
    }

    private void SelectItemsButton_Click(object sender, RoutedEventArgs e)
    {
        ClipboardListView.SelectionMode = ListViewSelectionMode.Multiple;
        SelectItemsButton.Visibility = Visibility.Collapsed;
        DeleteSelectedButton.Visibility = Visibility.Visible;
        CancelSelectionButton.Visibility = Visibility.Visible;
    }

    private void CancelSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        ClipboardListView.SelectionMode = ListViewSelectionMode.Single;
        SelectItemsButton.Visibility = Visibility.Visible;
        DeleteSelectedButton.Visibility = Visibility.Collapsed;
        CancelSelectionButton.Visibility = Visibility.Collapsed;
    }

    private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = ClipboardListView.SelectedItems.Cast<ClipboardItem>().ToList();
        foreach (var item in selectedItems)
        {
            _storageManager.DeleteItem(item.Id);
            _allItems.Remove(item);
            FilteredItems.Remove(item);
        }
        
        CancelSelectionButton_Click(sender, e);
    }
}
