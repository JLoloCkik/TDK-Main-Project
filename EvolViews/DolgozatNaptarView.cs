using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class DolgozatNaptarView : IEvolView
{
    private const string TestEntityType = "Test";
    private readonly IStudentContext? _context;
    private Grid? _calendarGrid;
    private User? _currentUser;
    private ListBox? _detailsListBox;

    private DateTime _displayedMonth;
    private TextBlock? _monthYearTextBlock;
    private List<GenericRecord>? _myTests;
    private TextBlock? _statusTextBlock;

    public DolgozatNaptarView()
    {
    }

    public DolgozatNaptarView(IStudentContext context)
    {
        _context = context;
        _displayedMonth = DateTime.Today;
    }

    public new string Name => "Dolgozat Naptár";
    public string Description => "A közelgő dolgozatok megtekintése naptár formátumban.";

    public Control CreateView()
    {
        var mainPanel = new DockPanel();

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10)
        };
        var prevButton = new Button { Content = "<" };
        prevButton.Click += PrevMonth_Click;
        _monthYearTextBlock = new TextBlock
            { VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Bold, FontSize = 16 };
        var nextButton = new Button { Content = ">" };
        nextButton.Click += NextMonth_Click;
        headerPanel.Children.Add(prevButton);
        headerPanel.Children.Add(_monthYearTextBlock);
        headerPanel.Children.Add(nextButton);

        DockPanel.SetDock(headerPanel, Dock.Top);

        var detailsPanel = new StackPanel
        {
            Spacing = 5,
            Margin = new Thickness(10),
            MinWidth = 250
        };
        detailsPanel.Children.Add(new TextBlock { Text = "Kijelölt nap dolgozatai:", FontWeight = FontWeight.Bold });
        _detailsListBox = new ListBox { MinHeight = 200, MaxWidth = 300 };
        detailsPanel.Children.Add(_detailsListBox);

        DockPanel.SetDock(detailsPanel, Dock.Right);

        _statusTextBlock = new TextBlock
        {
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        DockPanel.SetDock(_statusTextBlock, Dock.Bottom);

        _calendarGrid = new Grid
        {
            Margin = new Thickness(10)
        };

        mainPanel.Children.Add(headerPanel);
        mainPanel.Children.Add(detailsPanel);
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(_calendarGrid);

        LoadAndDisplayData();

        return mainPanel;
    }

    private void LoadAndDisplayData()
    {
        if (_context == null || _statusTextBlock == null) return;

        _currentUser = _context.GetMyProfile();
        if (_currentUser == null)
        {
            _statusTextBlock.Text = "Hiba: Nem sikerült betölteni a diák adatait.";
            return;
        }

        var allTests = _context.QueryEntities(TestEntityType);
        _myTests = allTests.Where(t =>
            t.Data.ContainsKey("ClassName") &&
            t.Data["ClassName"] == _currentUser.ClassName).ToList();

        _statusTextBlock.Text = $"Betöltve {_myTests.Count} dolgozat az osztály számára.";

        PopulateCalendar();
    }

    private void PopulateCalendar()
    {
        if (_calendarGrid == null || _monthYearTextBlock == null) return;

        _monthYearTextBlock.Text = _displayedMonth.ToString("yyyy. MMMM", new CultureInfo("hu-HU"));
        _calendarGrid.Children.Clear();
        _calendarGrid.RowDefinitions.Clear();
        _calendarGrid.ColumnDefinitions.Clear();

        if (_detailsListBox != null) _detailsListBox.ItemsSource = new List<string> { "Válasszon egy napot!" };

        for (var i = 0; i < 7; i++)
            _calendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        _calendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < 6; i++) _calendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        string[] dayHeaders = { "H", "K", "Sze", "Cs", "P", "Szo", "V" };
        for (var i = 0; i < 7; i++)
        {
            var header = new TextBlock
            {
                Text = dayHeaders[i],
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeight.Bold
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, i);
            _calendarGrid.Children.Add(header);
        }

        var firstDayOfMonth = new DateTime(_displayedMonth.Year, _displayedMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(_displayedMonth.Year, _displayedMonth.Month);
        var dayOfWeek = ((int)firstDayOfMonth.DayOfWeek + 6) % 7; // 0=Monday

        for (var i = 0; i < daysInMonth; i++)
        {
            var row = (i + dayOfWeek) / 7 + 1;
            var col = (i + dayOfWeek) % 7;
            var dayDate = firstDayOfMonth.AddDays(i);

            var border = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Tag = dayDate
            };

            border.PointerPressed += Day_PointerPressed;

            var dayPanel = new StackPanel { Spacing = 2, Margin = new Thickness(4) };
            dayPanel.Children.Add(new TextBlock { Text = (i + 1).ToString() });

            var testsForDay = _myTests?.Where(t =>
                t.Data.ContainsKey("Date") &&
                DateTime.TryParse(t.Data["Date"], out var testDate) &&
                testDate.Date == dayDate.Date
            ).ToList();

            if (testsForDay != null && testsForDay.Any())
            {
                border.Background = Brushes.LightSkyBlue;
                var indicator = new Border
                {
                    Background = Brushes.Red, CornerRadius = new CornerRadius(3), Width = 6, Height = 6,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(2)
                };
                dayPanel.Children.Add(indicator);
            }

            border.Child = dayPanel;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            _calendarGrid.Children.Add(border);
        }
    }

    private void Day_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_detailsListBox == null || !(sender is Border border) || !(border.Tag is DateTime selectedDate)) return;

        var testsForDay = _myTests?.Where(t =>
            t.Data.ContainsKey("Date") &&
            DateTime.TryParse(t.Data["Date"], out var testDate) &&
            testDate.Date == selectedDate.Date
        ).ToList();

        if (testsForDay != null && testsForDay.Any())
            _detailsListBox.ItemsSource = testsForDay.Select(t =>
                $"{t.Data.GetValueOrDefault("SubjectName", "?")}: {t.Data.GetValueOrDefault("Topic", "Nincs téma")}"
            ).ToList();
        else
            _detailsListBox.ItemsSource = new List<string> { "Nincs dolgozat ezen a napon." };
    }

    private void PrevMonth_Click(object? sender, RoutedEventArgs e)
    {
        _displayedMonth = _displayedMonth.AddMonths(-1);
        PopulateCalendar();
    }

    private void NextMonth_Click(object? sender, RoutedEventArgs e)
    {
        _displayedMonth = _displayedMonth.AddMonths(1);
        PopulateCalendar();
    }
}