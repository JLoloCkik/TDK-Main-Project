using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class BeiratkozasiStatisztikaView : IEvolView
{
    private readonly IDirectorContext? _context;
    private Grid? _statsGrid;
    private TextBlock? _statusTextBlock;
    private TextBlock? _summaryClassCountText;
    private TextBlock? _summaryTotalStudentsText;

    public BeiratkozasiStatisztikaView()
    {
    }

    public BeiratkozasiStatisztikaView(IDirectorContext context)
    {
        _context = context;
    }

    public new string Name => "Beiratkozási Statisztika";

    public string Description =>
        "Az iskolai osztályokba beiratkozott diákok számának és százalékos arányának megjelenítése, táblázatos formában, kibővített összegzéssel.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10, Margin = new Thickness(15) };
        _statusTextBlock = new TextBlock { Text = "Adatok betöltése...", Margin = new Thickness(0, 0, 0, 10) };
        _statsGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(5) };
        var headerBorder = new Border
            { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(5) };
        var headerPanel = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        var h1 = new TextBlock { Text = "Osztály", FontWeight = FontWeight.Bold };
        Grid.SetColumn(h1, 0);
        var h2 = new TextBlock
        {
            Text = "Létszám", FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10, 0)
        };
        Grid.SetColumn(h2, 1);
        var h3 = new TextBlock
        {
            Text = "Arány (%)", FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10, 0)
        };
        Grid.SetColumn(h3, 2);
        headerPanel.Children.Add(h1);
        headerPanel.Children.Add(h2);
        headerPanel.Children.Add(h3);
        headerBorder.Child = headerPanel;
        _summaryTotalStudentsText = new TextBlock { FontWeight = FontWeight.Bold };
        _summaryClassCountText = new TextBlock { FontWeight = FontWeight.Bold };
        var summaryBorder = new Border
        {
            BorderBrush = Brushes.DarkGray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10), Margin = new Thickness(0, 15, 0, 0)
        };
        var summaryPanel = new StackPanel { Spacing = 5 };
        summaryPanel.Children.Add(new TextBlock
            { Text = "Összegzés", FontSize = 16, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 5) });
        summaryPanel.Children.Add(_summaryTotalStudentsText);
        summaryPanel.Children.Add(_summaryClassCountText);
        summaryBorder.Child = summaryPanel;
        mainPanel.Children.Add(new TextBlock
            { Text = Name, FontSize = 22, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 10) });
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(headerBorder);
        mainPanel.Children.Add(new ScrollViewer { Content = _statsGrid });
        mainPanel.Children.Add(summaryBorder);
        LoadStatistics();
        return mainPanel;
    }

    private void LoadStatistics()
    {
        if (_context == null || _statsGrid == null || _statusTextBlock == null || _summaryTotalStudentsText == null ||
            _summaryClassCountText == null)
        {
            if (_statusTextBlock != null)
                _statusTextBlock.Text = "Hiba: A nézet komponensei nincsenek megfelelően inicializálva.";
            return;
        }

        _statsGrid.Children.Clear();
        _statsGrid.RowDefinitions.Clear();
        try
        {
            var allUsers = _context.GetAllUsers();
            if (allUsers == null)
            {
                _statusTextBlock.Text = "Hiba: Nem sikerült lekérni a felhasználókat.";
                return;
            }

            var allStudents = allUsers.Where(u => u.Role == Role.Student).ToList();
            var totalStudents = allStudents.Count;
            if (totalStudents == 0)
            {
                _statusTextBlock.Text = "Nincsenek diákok a rendszerben.";
                _summaryTotalStudentsText.Text = "Összes diák: 0";
                _summaryClassCountText.Text = "Osztályok száma: 0";
                return;
            }

            var classStats = allStudents.Where(s => !string.IsNullOrEmpty(s.ClassName)).GroupBy(s => s.ClassName)
                .Select(g => new
                {
                    ClassName = g.Key ?? "Nincs besorolva", Count = g.Count(),
                    Percentage = (double)g.Count() / totalStudents * 100
                }).OrderBy(s => s.ClassName).ToList();
            var rowIndex = 0;
            foreach (var stat in classStats)
            {
                _statsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var classNameText = new TextBlock
                    { Text = stat.ClassName, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5) };
                Grid.SetRow(classNameText, rowIndex);
                Grid.SetColumn(classNameText, 0);
                var countText = new TextBlock
                {
                    Text = $"{stat.Count} fő", VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10, 5)
                };
                Grid.SetRow(countText, rowIndex);
                Grid.SetColumn(countText, 1);
                var percentageText = new TextBlock
                {
                    Text = stat.Percentage.ToString("F2", CultureInfo.InvariantCulture),
                    VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(10, 5)
                };
                Grid.SetRow(percentageText, rowIndex);
                Grid.SetColumn(percentageText, 2);
                _statsGrid.Children.Add(classNameText);
                _statsGrid.Children.Add(countText);
                _statsGrid.Children.Add(percentageText);
                rowIndex++;
            }

            _summaryTotalStudentsText.Text = $"Összes beiratkozott diák: {totalStudents}";
            _summaryClassCountText.Text = $"Osztályok száma: {classStats.Count}";
            _statusTextBlock.Text = $"Statisztika frissítve: {DateTime.Now:yyyy.MM.dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba a statisztika betöltése közben: {ex.Message}";
        }
    }
}