using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace ClipboardManager.Services;

public class ClipboardMonitor
{
    private readonly StorageManager _storageManager;
    private bool _isListening = false;
    public bool IgnoreNextChange { get; set; } = false;

    public event EventHandler<ClipboardItem>? OnItemAdded;

    public ClipboardMonitor(StorageManager storageManager)
    {
        _storageManager = storageManager;
    }

    public void Start()
    {
        if (_isListening) return;
        Clipboard.ContentChanged += Clipboard_ContentChanged;
        _isListening = true;
    }

    public void Stop()
    {
        if (!_isListening) return;
        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        _isListening = false;
    }

    private async void Clipboard_ContentChanged(object? sender, object e)
    {
        if (IgnoreNextChange)
        {
            IgnoreNextChange = false;
            return;
        }

        try
        {
            var dataPackageView = Clipboard.GetContent();
            var newItem = new ClipboardItem();

            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                var text = await dataPackageView.GetTextAsync();
                newItem.DataType = ClipboardDataType.Text;
                
                if (text.Length > 0)
                {
                    var firstLine = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                    newItem.PreviewSnippet = firstLine.Length > 50 ? firstLine.Substring(0, 50) + "..." : firstLine;
                    
                    if (text.Length > 1000 || text != newItem.PreviewSnippet)
                    {
                        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
                        newItem.DiskFilePath = _storageManager.SaveLargeDataToDisk(bytes, ".txt");
                    }
                }
            }
            else if (dataPackageView.Contains(StandardDataFormats.Bitmap))
            {
                newItem.DataType = ClipboardDataType.Image;
                newItem.PreviewSnippet = "[Image]";
                
                var streamRef = await dataPackageView.GetBitmapAsync();
                using var stream = await streamRef.OpenReadAsync();
                using var reader = new DataReader(stream.GetInputStreamAt(0));
                var bytes = new byte[stream.Size];
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(bytes);

                newItem.DiskFilePath = _storageManager.SaveLargeDataToDisk(bytes, ".png");
            }
            else if (dataPackageView.Contains(StandardDataFormats.StorageItems))
            {
                newItem.DataType = ClipboardDataType.FileDrop;
                var storageItems = await dataPackageView.GetStorageItemsAsync();
                var count = storageItems.Count;
                if (count > 0)
                {
                    var firstItem = storageItems[0];
                    newItem.PreviewSnippet = $"{count} File(s): {firstItem.Name}...";
                    newItem.DiskFilePath = firstItem.Path;
                }
            }
            else
            {
                return; // Ignore unknown formats for now
            }

            _storageManager.AddItem(newItem);
            OnItemAdded?.Invoke(this, newItem);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading clipboard: {ex.Message}");
        }
    }
}
