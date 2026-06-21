using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ClipboardManager.Services;

public class StorageManager
{
    private readonly string _baseDir;
    private readonly string _dataDir;
    private readonly string _dbPath;

    public StorageManager()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _baseDir = Path.Combine(localAppData, "ClipboardManager");
        _dataDir = Path.Combine(_baseDir, "Data");
        _dbPath = Path.Combine(_baseDir, "clipboard.db");

        Initialize();
    }

    private void Initialize()
    {
        if (!Directory.Exists(_dataDir))
        {
            Directory.CreateDirectory(_dataDir);
        }

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS ClipboardItems (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                DataType INTEGER NOT NULL,
                PreviewSnippet TEXT,
                DiskFilePath TEXT,
                IsPinned INTEGER NOT NULL DEFAULT 0
            );
        ";
        command.ExecuteNonQuery();
    }

    public void AddItem(ClipboardItem item)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ClipboardItems (Id, Timestamp, DataType, PreviewSnippet, DiskFilePath, IsPinned)
            VALUES ($id, $timestamp, $dataType, $previewSnippet, $diskFilePath, $isPinned);
        ";
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$timestamp", item.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("$dataType", (int)item.DataType);
        command.Parameters.AddWithValue("$previewSnippet", item.PreviewSnippet);
        command.Parameters.AddWithValue("$diskFilePath", item.DiskFilePath ?? string.Empty);
        command.Parameters.AddWithValue("$isPinned", item.IsPinned ? 1 : 0);

        command.ExecuteNonQuery();
    }

    public List<ClipboardItem> GetItems(int limit = 100)
    {
        var items = new List<ClipboardItem>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Timestamp, DataType, PreviewSnippet, DiskFilePath, IsPinned FROM ClipboardItems ORDER BY Timestamp DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new ClipboardItem
            {
                Id = reader.GetString(0),
                Timestamp = DateTime.Parse(reader.GetString(1)),
                DataType = (ClipboardDataType)reader.GetInt32(2),
                PreviewSnippet = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                DiskFilePath = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                IsPinned = reader.GetInt32(5) == 1
            });
        }
        return items;
    }

    public void DeleteItem(string id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        
        var getCommand = connection.CreateCommand();
        getCommand.CommandText = "SELECT DiskFilePath FROM ClipboardItems WHERE Id = $id;";
        getCommand.Parameters.AddWithValue("$id", id);
        using var reader = getCommand.ExecuteReader();
        if (reader.Read())
        {
            var filePath = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try { File.Delete(filePath); } catch { /* Ignore file lock issues for now */ }
            }
        }

        var deleteCommand = connection.CreateCommand();
        deleteCommand.CommandText = "DELETE FROM ClipboardItems WHERE Id = $id;";
        deleteCommand.Parameters.AddWithValue("$id", id);
        deleteCommand.ExecuteNonQuery();
    }

    public string SaveLargeDataToDisk(byte[] data, string extension)
    {
        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(_dataDir, fileName);
        File.WriteAllBytes(filePath, data);
        return filePath;
    }
}
