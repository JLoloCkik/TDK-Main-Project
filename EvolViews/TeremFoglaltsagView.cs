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

public class TeremFoglaltsagView : IEvolView
{
    private const string LessonEntityType = "Lesson";
    private readonly IDirectorContext? _context;

    private List<GenericRecord>? _allLessons;
    private DatePicker? _datePicker;
    private ListBox? _occupancyListBox;
    private TextBlock? _statusTextBlock;

    public TeremFoglaltsagView()
    {
    }

    public TeremFoglaltsagView(IDirectorContext context)
    {
        _context = context;
    }

    public new string Name => "Teremfoglaltsági Lista";
    public string Description => "Teremfoglaltság megtekintése osztályok és időpontok szerint.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(15)
        };

        _statusTextBlock = new TextBlock { Text = "Válasszon egy napot a foglaltság megtekintéséhez." };

        _datePicker = new DatePicker
        {
            SelectedDate = DateTime.Today
        };
        _datePicker.SelectedDateChanged += OnDateChanged;

        _occupancyListBox = new ListBox();

        var datePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        datePanel.Children.Add(new TextBlock { Text = "Dátum:", VerticalAlignment = VerticalAlignment.Center });
        datePanel.Children.Add(_datePicker);

        mainPanel.Children.Add(datePanel);
        mainPanel.Children.Add(_occupancyListBox);
        mainPanel.Children.Add(_statusTextBlock);

        LoadAllLessons();
        UpdateOccupancyList();

        return mainPanel;
    }

    private void LoadAllLessons()
    {
        if (_context == null)
        {
            SetStatus("Hiba: A kontextus nincs beállítva.", true);
            return;
        }

        try
        {
            _allLessons = _context.QueryEntities(LessonEntityType);
            if (_allLessons == null)
            {
                _allLessons = new List<GenericRecord>();
                SetStatus("Nem sikerült betölteni az órarendi adatokat.", true);
            }
            else
            {
                SetStatus($"Összesen {_allLessons.Count} óra betöltve.", false);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Hiba az órák betöltése közben: {ex.Message}", true);
            _allLessons = new List<GenericRecord>();
        }
    }

    private void OnDateChanged(object? sender, DatePickerSelectedValueChangedEventArgs e)
    {
        UpdateOccupancyList();
    }

    private void UpdateOccupancyList()
    {
        if (_occupancyListBox == null || _datePicker?.SelectedDate == null || _allLessons == null) return;

        var selectedDate = _datePicker.SelectedDate.Value.Date;

        var lessonsForDay = _allLessons
            .Where(lesson =>
            {
                if (lesson.Data.TryGetValue("Date", out var dateStr) && DateTime.TryParse(dateStr,
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var lessonDate))
                    return lessonDate.Date == selectedDate;
                return false;
            })
            .OrderBy(lesson => DateTime.Parse(lesson.Data["Date"], CultureInfo.InvariantCulture))
            .ThenBy(lesson => lesson.Data.GetValueOrDefault("Room", "N/A"))
            .ToList();

        if (lessonsForDay.Any())
            _occupancyListBox.ItemsSource = lessonsForDay.Select(lesson =>
            {
                var time = DateTime.Parse(lesson.Data["Date"], CultureInfo.InvariantCulture).ToString("HH:mm");
                var room = lesson.Data.GetValueOrDefault("Room", "Ismeretlen terem");
                var className = lesson.Data.GetValueOrDefault("ClassName", "Ismeretlen osztály");
                var subject = lesson.Data.GetValueOrDefault("SubjectName", "Ismeretlen tantárgy");
                return $"{time} - {room}: {className} ({subject})";
            }).ToList();
        else
            _occupancyListBox.ItemsSource = new List<string> { "Nincs rögzített óra a kiválasztott napon." };
    }

    private void SetStatus(string message, bool isError)
    {
        if (_statusTextBlock != null)
        {
            _statusTextBlock.Text = message;
            _statusTextBlock.Foreground = isError ? Brushes.Red : Brushes.Gray;
        }
    }
}