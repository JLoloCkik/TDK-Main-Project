using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class KozelgoDolgozatokView : IEvolView
{
    private IStudentContext? _context;
    private ListBox? _testsListBox;
    private TextBlock? _statusTextBlock;
    private const string TestEntityType = "Test";

    public new string Name => "Közelgő Dolgozatok";
    public string Description => "A diák számára kiírt, jövőbeli dolgozatok listája.";

    public KozelgoDolgozatokView() { }

    public KozelgoDolgozatokView(IStudentContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        _testsListBox = new ListBox();
        _statusTextBlock = new TextBlock
        {
            Text = "Adatok betöltése...",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(10)
        };

        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Avalonia.Thickness(15),
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
            {
                if (test.Data.TryGetValue("ClassName", out var className) && className == myClass &&
                    test.Data.TryGetValue("Date", out var dateString) &&
                    DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var testDate) &&
                    testDate.Date >= DateTime.Today)
                {
                    string subject = test.Data.TryGetValue("SubjectName", out var s) ? s : "Ismeretlen tantárgy";
                    string topic = test.Data.TryGetValue("Topic", out var t) ? t : "Nincs megadva";
                    string formattedTest = $"{testDate:yyyy. MM. dd.} - {subject}: {topic}";
                    upcomingTests.Add(new Tuple<DateTime, string>(testDate, formattedTest));
                }
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
