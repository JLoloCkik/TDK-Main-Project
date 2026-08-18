using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Kreta.Contexts;
using Kreta.Core;

public class OsztalyTeremfoglaltsagView : IEvolView
{
    private const string LessonEntityType = "Lesson";
    private readonly IDirectorContext? _context;
    private List<string>? _allClasses;
    private List<GenericRecord>? _allLessons;
    private ComboBox? _classComboBox;
    private ListBox? _scheduleListBox;
    private TextBlock? _statusTextBlock;

    public OsztalyTeremfoglaltsagView()
    {
    }

    public OsztalyTeremfoglaltsagView(IDirectorContext context)
    {
        _context = context;
    }

    public new string Name => "Osztályok Teremfoglaltsága";
    public string Description => "Egy adott osztály órarendjének és teremhasználatának megtekintése.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10, Margin = new Thickness(10) };
        _statusTextBlock = new TextBlock
        {
            Text = "Válasszon egy osztályt az órarend megtekintéséhez.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _classComboBox = new ComboBox
            { PlaceholderText = "Válassz osztályt...", HorizontalAlignment = HorizontalAlignment.Stretch };
        _classComboBox.SelectionChanged += OnClassSelectionChanged;
        _scheduleListBox = new ListBox { HorizontalAlignment = HorizontalAlignment.Stretch, Height = 400 };
        var topPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5 };
        topPanel.Children.Add(new TextBlock { Text = "Osztály:" });
        topPanel.Children.Add(_classComboBox);
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(topPanel);
        mainPanel.Children.Add(_scheduleListBox);
        LoadInitialData();
        return mainPanel;
    }

    private void LoadInitialData()
    {
        if (_context == null)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Hiba: A kontextus nincs beállítva.";
            return;
        }

        try
        {
            _allClasses = _context.GetAllClasses();
            _allLessons = _context.QueryEntities(LessonEntityType);
            if (_classComboBox != null) _classComboBox.ItemsSource = _allClasses?.OrderBy(c => c).ToList();
            if (_allClasses == null || !_allClasses.Any())
                if (_statusTextBlock != null)
                    _statusTextBlock.Text = "Nincsenek osztályok a rendszerben.";
        }
        catch (Exception ex)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = $"Hiba az adatok betöltése közben: {ex.Message}";
        }
    }

    private void OnClassSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_classComboBox?.SelectedItem is not string selectedClass || _allLessons == null)
        {
            if (_scheduleListBox != null) _scheduleListBox.ItemsSource = null;
            return;
        }

        if (_statusTextBlock != null) _statusTextBlock.Text = $"{selectedClass} osztály órarendje:";
        try
        {
            var classLessons = _allLessons
                .Where(l => l.Data.GetValueOrDefault("ClassName", string.Empty) == selectedClass).Select(l =>
                    new
                    {
                        Lesson = l,
                        Success = DateTime.TryParse(l.Data.GetValueOrDefault("Date"), CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var date),
                        Date = date
                    }).Where(x => x.Success).OrderBy(x => x.Date)
                .ThenBy(x => x.Lesson.Data.GetValueOrDefault("StartTime", "00:00")).ToList();
            if (!classLessons.Any())
            {
                _scheduleListBox.ItemsSource = new List<string> { "Nincs óra rögzítve ehhez az osztályhoz." };
                return;
            }

            var cultureInfo = new CultureInfo("hu-HU");
            var formattedSchedule = classLessons.GroupBy(l => l.Date.ToString("dddd (yyyy.MM.dd)", cultureInfo))
                .Select(dayGroup =>
                {
                    var dayHeader = $"{char.ToUpper(dayGroup.Key[0])}{dayGroup.Key.Substring(1)}";
                    var lessonDetails = dayGroup.Select(l =>
                    {
                        var startTime = l.Lesson.Data.GetValueOrDefault("StartTime", "??:??");
                        var endTime = l.Lesson.Data.GetValueOrDefault("EndTime", "??:??");
                        var subject = l.Lesson.Data.GetValueOrDefault("SubjectName", "Ismeretlen tantárgy");
                        var room = l.Lesson.Data.GetValueOrDefault("Room", "N/A");
                        return $"    {startTime}-{endTime} - {subject} ({room})";
                    });
                    return $"{dayHeader}\n{string.Join("\n", lessonDetails)}";
                }).ToList();
            if (_scheduleListBox != null) _scheduleListBox.ItemsSource = formattedSchedule;
        }
        catch (Exception ex)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = $"Hiba a feldolgozás során: {ex.Message}";
        }
    }
}