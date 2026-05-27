using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace InrxToSiusRank.Desktop;

internal sealed class LogWindow : Window
{
    private readonly TextBox _logBox;

    public LogWindow(
        Func<Task> copyLogAsync,
        Func<Task> saveLogAsync,
        Action clearLog)
    {
        Title = "Logg";
        Width = 900;
        Height = 520;
        MinWidth = 620;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _logBox = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
            FontFamily = new Avalonia.Media.FontFamily("Consolas, Menlo, Cascadia Mono, monospace"),
            FontSize = 12
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_logBox, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(_logBox, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);

        var copyButton = new Button { Content = "Kopier logg" };
        copyButton.Click += async (_, _) => await copyLogAsync();

        var saveButton = new Button { Content = "Lagre logg..." };
        saveButton.Click += async (_, _) => await saveLogAsync();

        var clearButton = new Button { Content = "Tøm" };
        clearButton.Click += (_, _) => clearLog();

        var closeButton = new Button { Content = "Lukk" };
        closeButton.Click += (_, _) => Close();

        Content = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 8,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        copyButton,
                        saveButton,
                        clearButton,
                        closeButton
                    }
                },
                _logBox
            }
        };
        Grid.SetRow(_logBox, 1);
    }

    public void SetLogText(string text)
    {
        _logBox.Text = text;
        ScrollToEnd();
    }

    public void ScrollToEnd()
    {
        _logBox.CaretIndex = _logBox.Text?.Length ?? 0;
        _logBox.ScrollToLine(Math.Max(0, _logBox.GetLineCount() - 1));
    }
}
