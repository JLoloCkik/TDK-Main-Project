using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class TeremKezeloView : IEvolView
{
    private const string LessonEntityType = "Lesson";
    private readonly IDirectorContext? _context;

    private List<GenericRecord>? _allLessons;
    private DatePicker? _datePicker;
    private StackPanel? _editPanel;
    private ListBox? _lessonsListBox;
    private TextBox? _roomTextBox;
    private Button? _saveButton;
    private GenericRecord? _selectedLesson;
    private TextBlock? _statusTextBlock;

    public TeremKezeloView()
    {
    }

    public TeremKezeloView(IDirectorContext context)
    {
        _context = context;
    }

    public new string Name => "Terem Kezelés";
    public string Description => "Teremfoglaltság megtekintése és tanórák termeinek módosítása.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(10)
        };

        _datePicker = new DatePicker
        {
            SelectedDate = DateTime.Today
        };
        _datePicker.SelectedDateChanged += DatePicker_SelectedDateChanged;

        _lessonsListBox = new ListBox
        {
            Height = 300,
            Margin = new Thickness(0, 5)
        };
        _lessonsListBox.SelectionChanged += LessonsListBox_SelectionChanged;

        _editPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 5,
            IsVisible = false,
            Margin = new Thickness(0, 10)
        };

        _roomTextBox = new TextBox();

        _saveButton = new Button
        {
            Content = "Mentés"
        };
        _saveButton.Click += SaveButton_Click;

        _editPanel.Children.Add(new TextBlock { Text = "Óra új terme:" });
        _editPanel.Children.Add(_roomTextBox);
        _editPanel.Children.Add(_saveButton);

        _statusTextBlock = new TextBlock
        {
            Margin = new Thickness(0, 5)
        };

        mainPanel.Children.Add(new TextBlock { Text = "Válasszon dátumot:", FontWeight = FontWeight.Bold });
        mainPanel.Children.Add(_datePicker);
        mainPanel.Children.Add(_lessonsListBox);
        mainPanel.Children.Add(_editPanel);
        mainPanel.Children.Add(_statusTextBlock);

        LoadAllLessons();

        return mainPanel;
    }

    private void LoadAllLessons()
    {
        if (_context == null) return;
        _allLessons = _context.QueryEntities(LessonEntityType);
        UpdateLessonListForSelectedDate();
    }

    private void DatePicker_SelectedDateChanged(object? sender, DatePickerSelectedValueChangedEventArgs e)
    {
        UpdateLessonListForSelectedDate();
    }

    private void UpdateLessonListForSelectedDate()
    {
        if (_lessonsListBox == null || _allLessons == null || _datePicker?.SelectedDate == null) return;

        var selectedDate = _datePicker.SelectedDate.Value.Date;

        var dailyLessons = _allLessons
            .Where(lesson => lesson.Data.ContainsKey("Date") &&
                             DateTime.TryParse(lesson.Data["Date"], CultureInfo.InvariantCulture,
                                 DateTimeStyles.AssumeLocal, out var lessonDate) &&
                             lessonDate.Date == selectedDate)
            .OrderBy(lesson =>
                DateTime.Parse(lesson.Data["Date"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal))
            .ToList();

        _lessonsListBox.ItemsSource = dailyLessons.Select(lesson =>
        {
            var lessonDate = DateTime.Parse(lesson.Data["Date"], CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal);
            var subject = lesson.Data.GetValueOrDefault("SubjectName", "N/A");
            var className = lesson.Data.GetValueOrDefault("ClassName", "N/A");
            var room = lesson.Data.GetValueOrDefault("Room", "N/A");
            return $"{lessonDate:HH:mm} - {room} - {className} - {subject}";
        }).ToList();

        if (_editPanel != null) _editPanel.IsVisible = false;
        _selectedLesson = null;
    }

    private void LessonsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_lessonsListBox == null || _lessonsListBox.SelectedItem == null || _editPanel == null ||
            _roomTextBox == null || _datePicker?.SelectedDate == null)
        {
            if (_editPanel != null) _editPanel.IsVisible = false;
            return;
        }

        var selectedDate = _datePicker.SelectedDate.Value.Date;
        var dailyLessons = _allLessons?.Where(lesson => lesson.Data.ContainsKey("Date") &&
                                                        DateTime.TryParse(lesson.Data["Date"],
                                                            CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal,
                                                            out var lessonDate) &&
                                                        lessonDate.Date == selectedDate)
            .OrderBy(lesson =>
                DateTime.Parse(lesson.Data["Date"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal))
            .ToList();

        var selectedIndex = _lessonsListBox.SelectedIndex;
        if (dailyLessons != null && selectedIndex >= 0 && selectedIndex < dailyLessons.Count)
        {
            _selectedLesson = dailyLessons[selectedIndex];
            _roomTextBox.Text = _selectedLesson.Data.GetValueOrDefault("Room", "");
            _editPanel.IsVisible = true;
        }
        else
        {
            _selectedLesson = null;
            _editPanel.IsVisible = false;
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _selectedLesson == null || _roomTextBox == null ||
            string.IsNullOrWhiteSpace(_roomTextBox.Text))
        {
            if (_statusTextBlock != null)
                _statusTextBlock.Text = "Hiba: Nincs kiválasztott óra vagy az új terem neve érvénytelen.";
            return;
        }

        var newData = new Dictionary<string, string>(_selectedLesson.Data);
        newData["Room"] = _roomTextBox.Text.Trim();

        try
        {
            var savedId = _context.SaveEntity(LessonEntityType, newData, _selectedLesson.Id);
            if (savedId > 0)
            {
                if (_statusTextBlock != null) _statusTextBlock.Text = "Terem sikeresen módosítva!";
                LoadAllLessons();
            }
            else
            {
                if (_statusTextBlock != null) _statusTextBlock.Text = "A mentés sikertelen volt.";
            }
        }
        catch (Exception ex)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = $"Hiba a mentés során: {ex.Message}";
        }
    }
}