using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

namespace Kreta.Dynamic;

public class HibaView : UserControl, IEvolView
{
    public string Name => "Hozzáférés Megtagadva";
    public string Description => "Hozzáférés-korlátozás hiba.";

    public Control CreateView()
    {
        var panel = new StackPanel
        {
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = "❌ Hozzáférés megtagadva!",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#C0392B")),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Nincs jogosultsága a kért funkció végrehajtásához (RBAC hiba).",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#7B241C")),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FDEDEC")),
            BorderBrush = new SolidColorBrush(Color.Parse("#F5B7B1")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(24),
            Margin = new Avalonia.Thickness(16),
            Child = panel
        };
    }
}