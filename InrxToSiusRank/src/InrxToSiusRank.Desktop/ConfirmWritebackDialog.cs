using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace InrxToSiusRank.Desktop;

internal sealed class ConfirmWritebackDialog : Window
{
    public ConfirmWritebackDialog(string title)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var cancelButton = new Button
        {
            Content = "Avbryt",
            MinWidth = 90
        };
        cancelButton.Click += (_, _) => Close(false);

        var confirmButton = new Button
        {
            Content = "Skriv til inrX",
            MinWidth = 120
        };
        confirmButton.Classes.Add("danger");
        confirmButton.Click += (_, _) => Close(true);

        Content = new Border
        {
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Dette skriver SIUS Rank-resultater til storage.db3/inrX.",
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap,
                        Width = 440
                    },
                    new TextBlock
                    {
                        Text = "Kontroller at du har kjørt og gjennomgått tørrkjøring først. Fortsett bare hvis du har en trygg kopi av databasen og er klar til å skrive resultatene tilbake.",
                        TextWrapping = TextWrapping.Wrap,
                        Width = 440
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            cancelButton,
                            confirmButton
                        }
                    }
                }
            }
        };
    }
}
