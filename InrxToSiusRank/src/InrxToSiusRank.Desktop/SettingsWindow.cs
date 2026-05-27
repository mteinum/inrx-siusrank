using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace InrxToSiusRank.Desktop;

internal sealed record GlobalSettingsValues(
    string EncodingName,
    string SiusRankFolder,
    string DefaultDatabasePath);

internal sealed class SettingsWindow : Window
{
    private readonly ComboBox _encodingBox;
    private readonly TextBox _siusRankFolderBox;
    private readonly TextBox _databasePathBox;

    public SettingsWindow(GlobalSettingsValues values)
    {
        Title = "Innstillinger";
        Width = 720;
        Height = 260;
        MinWidth = 620;
        MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _encodingBox = new ComboBox
        {
            Width = 180,
            ItemsSource = new[] { CsvEncoding.Utf8Bom, CsvEncoding.Windows1252 },
            SelectedItem = string.IsNullOrWhiteSpace(values.EncodingName) ? CsvEncoding.Utf8Bom : values.EncodingName
        };
        _siusRankFolderBox = new TextBox { Text = values.SiusRankFolder, PlaceholderText = @"C:\SIUS\SiusRank" };
        _databasePathBox = new TextBox { Text = values.DefaultDatabasePath, PlaceholderText = "Default storage.db3" };

        var siusBrowseButton = new Button { Content = "Velg mappe" };
        siusBrowseButton.Click += async (_, _) => await BrowseFolderAsync(_siusRankFolderBox, "Velg SIUS Rank-mappe");

        var databaseBrowseButton = new Button { Content = "Velg fil" };
        databaseBrowseButton.Click += async (_, _) => await BrowseDatabaseAsync();

        var cancelButton = new Button { Content = "Avbryt", MinWidth = 90 };
        cancelButton.Click += (_, _) => Close(null);

        var saveButton = new Button { Content = "Lagre", MinWidth = 90, Classes = { "primary" } };
        saveButton.Click += (_, _) => Close(new GlobalSettingsValues(
            _encodingBox.SelectedItem?.ToString() ?? CsvEncoding.Utf8Bom,
            _siusRankFolderBox.Text?.Trim() ?? string.Empty,
            _databasePathBox.Text?.Trim() ?? string.Empty));

        var fields = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = "Globale standardverdier brukes før event.json er lastet. Event-spesifikke stier lagres relativt til stevnemappen.",
                    Classes = { "helper" },
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                CreateEncodingRow(),
                CreatePathRow("SIUS Rank-mappe", _siusRankFolderBox, siusBrowseButton),
                CreatePathRow("Default inrX storage.db3", _databasePathBox, databaseBrowseButton)
            }
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, saveButton }
        };
        Grid.SetRow(buttons, 1);

        Content = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 16,
            Children =
            {
                fields,
                buttons
            }
        };
    }

    private Grid CreateEncodingRow()
    {
        var row = CreateRow("Encoding", _encodingBox);
        return row;
    }

    private static Grid CreatePathRow(string label, TextBox input, Button browseButton)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("190,*,Auto"),
            ColumnSpacing = 8
        };
        row.Children.Add(new TextBlock { Text = label, Classes = { "form-label" }, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(input, 1);
        row.Children.Add(input);
        Grid.SetColumn(browseButton, 2);
        row.Children.Add(browseButton);
        return row;
    }

    private static Grid CreateRow(string label, Control control)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("190,*"),
            ColumnSpacing = 8
        };
        row.Children.Add(new TextBlock { Text = label, Classes = { "form-label" }, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private async Task BrowseDatabaseAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Velg default storage.db3",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("SQLite database") { Patterns = ["*.db3", "*.sqlite", "*.sqlite3"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            _databasePathBox.Text = path;
        }
    }

    private async Task BrowseFolderAsync(TextBox target, string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            target.Text = path;
        }
    }
}
