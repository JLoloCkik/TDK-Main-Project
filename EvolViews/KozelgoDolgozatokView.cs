using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class KozelgoDolgozatokView : IEvolView
{
    private const string TestEntityType = "Test";
    private readonly IStudentContext? _context;
    private TextBlock? _statusTextBlock;
    private ListBox? _testsListBox;

    public KozelgoDolgozatokView()
    {
    }

    public KozelgoDolgozatokView(IStudentContext context)
    {
        _context = context;
    }

    public new string Name => "Közelgő Dolgozatok";
    public string Description => "A diák számára kiírt, jövőbeli dolgozatok listája.";

    public Control CreateView()
    {
        _testsListBox = new ListBox();
        _statusTextBlock = new TextBlock
        {
            Text = "Adatok betöltése...",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(10)
        };

        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(15),
            Children =
            {
                new TextBlock
                {
                    Text = "Közelgő dolgozataim",
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                _statusTextBlock,
                _testsListBox
            }
        };

        LoadUpcomingTests();

        return mainPanel;
    }

    private void LoadUpcomingTests()
    {
        if (_context == null || _testsListBox == null || _statusTextBlock == null)
        {
            if (_statusTextBlock != null)
                _statusTextBlock.Text = "Hiba: A kontextus nincs beállítva.";
            return;
        }

        try
        {
            var myClass = _context.GetMyProfile()?.ClassName;
            if (string.IsNullOrEmpty(myClass))
            {
                _statusTextBlock.Text = "Hiba: Nincs osztályhoz rendelve.";
                return;
            }

            var allTests = _context.QueryEntities(TestEntityType);
            var upcomingTests = new List<Tuple<DateTime, string>>();

            foreach (var test in allTests)
                if (test.Data.TryGetValue("ClassName", out var className) && className == myClass &&
                    test.Data.TryGetValue("Date", out var dateString) &&
                    DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None,
                        out var testDate) &&
                    testDate.Date >= DateTime.Today)
                {
                    var subject = test.Data.TryGetValue("SubjectName", out var s) ? s : "Ismeretlen tantárgy";
                    var topic = test.Data.TryGetValue("Topic", out var t) ? t : "Nincs megadva";
                    var formattedTest = $"{testDate:yyyy. MM. dd.} - {subject}: {topic}";
                    upcomingTests.Add(new Tuple<DateTime, string>(testDate, formattedTest));
                }

            if (upcomingTests.Any())
            {
                _testsListBox.ItemsSource = upcomingTests.OrderBy(t => t.Item1).Select(t => t.Item2).ToList();
                _statusTextBlock.IsVisible = false;
            }
            else
            {
                _statusTextBlock.Text = "Nincsenek közelgő dolgozatok.";
                _statusTextBlock.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba történt a dolgozatok betöltése közben: {ex.Message}";
        }
    }
}