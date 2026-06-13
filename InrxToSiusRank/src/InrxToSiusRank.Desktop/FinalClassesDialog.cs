using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace InrxToSiusRank.Desktop;

internal sealed class FinalClassesDialog : Window
{
    private readonly IReadOnlyList<CheckBox> _classInputs;

    public FinalClassesDialog(
        string exerciseName,
        IReadOnlyList<string> classes,
        IReadOnlySet<string> selectedClasses)
    {
        Title = "Finaleklasser";
        Width = 420;
        Height = 360;
        MinWidth = 360;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _classInputs = classes
            .Select(className => new CheckBox
            {
                Content = className,
                Tag = className,
                IsChecked = IsSelected(className, selectedClasses),
                MinHeight = 28
            })
            .ToList();

        var classList = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = exerciseName,
                    FontWeight = FontWeight.SemiBold
                }
            }
        };
        foreach (var input in _classInputs)
        {
            classList.Children.Add(input);
        }

        var cancelButton = new Button { Content = "Avbryt", MinWidth = 90 };
        cancelButton.Click += (_, _) => Close(null);

        var applyButton = new Button { Content = "Bruk", MinWidth = 90, Classes = { "primary" } };
        applyButton.Click += (_, _) => Close(SelectedClasses());

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, applyButton }
        };
        Grid.SetRow(buttons, 1);

        Content = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 16,
            Children =
            {
                new ScrollViewer
                {
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    Content = classList
                },
                buttons
            }
        };
    }

    private IReadOnlyList<string> SelectedClasses() =>
        _classInputs
            .Where(input => input.IsChecked == true)
            .Select(input => input.Tag?.ToString() ?? string.Empty)
            .Where(className => !string.IsNullOrWhiteSpace(className))
            .ToList();

    private static bool IsSelected(string className, IReadOnlySet<string> selectedClasses)
    {
        var normalized = GroupNormalizer.Normalize(className);
        return selectedClasses.Contains(className) ||
               selectedClasses.Contains(normalized);
    }
}
