using System;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClipboardManager;

public enum ClipboardDataType
{
    Text,
    Image,
    FileDrop,
    Audio,
    Unknown
}

public class ClipboardItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private string _fullText;
    public string FullText
    {
        get => _fullText;
        set
        {
            _fullText = value;
            OnPropertyChanged();
        }
    }

    private Microsoft.UI.Xaml.Media.ImageSource _imagePreview;
    public Microsoft.UI.Xaml.Media.ImageSource ImagePreview
    {
        get => _imagePreview;
        set
        {
            _imagePreview = value;
            OnPropertyChanged();
        }
    }
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string TimestampText => Timestamp.ToString("HH:mm:ss");
    public ClipboardDataType DataType { get; set; }
    public string DataTypeText => DataType.ToString();
    public string PreviewSnippet { get; set; } = string.Empty;
    public string DiskFilePath { get; set; } = string.Empty;
    public bool IsPinned { get; set; } = false;

    public string IconGlyph => DataType switch
    {
        ClipboardDataType.Text => "\uE8D2",     // Document icon
        ClipboardDataType.Image => "\uEB9F",    // Image icon
        ClipboardDataType.FileDrop => "\uE8B7", // Folder icon
        _ => "\uE8A5"                           // Generic icon
    };
}
