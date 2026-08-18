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

public class JelenletRogzitoView : IEvolView
{
    private readonly ITeacherContext? _context;

    private List<string>? _allClasses;
    private List<Subject>? _allSubjects;
    private TextBlock? _attendanceHeader;

    private StackPanel? _attendancePanel;
    private ComboBox? _classComboBox;
    private Button? _createLessonButton;
    private int? _currentLessonId;
    private List<User>? _currentStudents;
    private DatePicker? _datePicker;

    private StackPanel? _lessonCreationPanel;
    private Button? _saveAttendanceButton;

    private TextBlock? _statusTextBlock;
    private StackPanel? _studentListPanel;
    private ComboBox? _subjectComboBox;
    private TextBox? _topicTextBox;

    public JelenletRogzitoView()
    {
    }

    public JelenletRogzitoView(ITeacherContext context)
    {
        _context = context;
    }

    public new string Name => "Jelenléti Ív";
    public string Description => "Tanórák létrehozása és a diákok jelenlétének rögzítése, késésekkel együtt.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel { Spacing = 15, Margin = new Thickness(20) };

        _statusTextBlock = new TextBlock
            { HorizontalAlignment = HorizontalAlignment.Center, FontWeight = FontWeight.Bold };

        // Panel 1: Lesson Creation
        _lessonCreationPanel = new StackPanel { Spacing = 10 };
        _classComboBox = new ComboBox { PlaceholderText = "Osztály" };
        _subjectComboBox = new ComboBox { PlaceholderText = "Tantárgy" };
        _datePicker = new DatePicker { SelectedDate = DateTime.Today };
        _topicTextBox = new TextBox { PlaceholderText = "Tanóra témája" };
        _createLessonButton = new Button { Content = "Óra létrehozása és jelenlét rögzítése" };
        _createLessonButton.Click += CreateLesson;

        _lessonCreationPanel.Children.Add(new TextBlock
            { Text = "1. Tanóra adatai", FontSize = 16, FontWeight = FontWeight.Bold });
        _lessonCreationPanel.Children.Add(_classComboBox);
        _lessonCreationPanel.Children.Add(_subjectComboBox);
        _lessonCreationPanel.Children.Add(_datePicker);
        _lessonCreationPanel.Children.Add(_topicTextBox);
        _lessonCreationPanel.Children.Add(_createLessonButton);

        // Panel 2: Attendance Recording
        _attendancePanel = new StackPanel { Spacing = 10, IsVisible = false };
        _attendanceHeader = new TextBlock { FontSize = 16, FontWeight = FontWeight.Bold };
        _studentListPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5 };
        _saveAttendanceButton = new Button { Content = "Jelenléti ív mentése" };
        _saveAttendanceButton.Click += SaveAttendance;

        var scrollViewer = new ScrollViewer { Content = _studentListPanel, Height = 400 };

        _attendancePanel.Children.Add(_attendanceHeader);
        _attendancePanel.Children.Add(scrollViewer);
        _attendancePanel.Children.Add(_saveAttendanceButton);

        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(_lessonCreationPanel);
        mainPanel.Children.Add(_attendancePanel);

        LoadData();
        return mainPanel;
    }

    private void LoadData()
    {
        if (_context == null) return;
        _allClasses = _context.GetAllClasses();
        _allSubjects = _context.GetAllSubjects();

        if (_classComboBox != null) _classComboBox.ItemsSource = _allClasses;
        if (_subjectComboBox != null) _subjectComboBox.ItemsSource = _allSubjects?.Select(s => s.Name).ToList();
    }

    private void CreateLesson(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _classComboBox?.SelectedItem == null || _subjectComboBox?.SelectedItem == null ||
            _datePicker?.SelectedDate == null || string.IsNullOrWhiteSpace(_topicTextBox?.Text))
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Minden mezőt ki kell tölteni az óra létrehozásához!";
            return;
        }

        var selectedClass = _classComboBox.SelectedItem.ToString();
        var selectedSubject = _subjectComboBox.SelectedItem.ToString();
        var lessonData = new Dictionary<string, string>
        {
            { "ClassName", selectedClass! },
            { "SubjectName", selectedSubject! },
            { "Topic", _topicTextBox.Text },
            { "Date", _datePicker.SelectedDate.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) }
        };

        _currentLessonId = _context.SaveEntity("Lesson", lessonData);

        _currentStudents = _context.GetMyClassStudents().Where(s => s.ClassName == selectedClass).ToList();

        if (_attendanceHeader != null)
            _attendanceHeader.Text = $"2. Jelenlét rögzítése: {selectedClass} - {selectedSubject}";
        PopulateStudentList(_currentStudents);

        if (_lessonCreationPanel != null) _lessonCreationPanel.IsVisible = false;
        if (_attendancePanel != null) _attendancePanel.IsVisible = true;
        if (_statusTextBlock != null) _statusTextBlock.Text = "Óra létrehozva. Rögzítse a jelenlétet!";
    }

    private void PopulateStudentList(List<User> students)
    {
        if (_studentListPanel == null) return;
        _studentListPanel.Children.Clear();

        foreach (var student in students)
        {
            var studentPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Thickness(0, 5),
                Tag = student
            };

            var nameText = new TextBlock
            {
                Text = student.Name,
                Width = 200,
                VerticalAlignment = VerticalAlignment.Center
            };

            var attendanceComboBox = new ComboBox
            {
                Name = "AttendanceComboBox",
                Width = 120,
                ItemsSource = new List<string> { "Jelen", "Hiányzik", "Igazolt", "Késik" },
                SelectedIndex = 0
            };

            var latePanel = new StackPanel
            {
                Name = "LatePanel",
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = false
            };

            var lateLabel = new TextBlock
            {
                Text = "Késés (perc):",
                VerticalAlignment = VerticalAlignment.Center
            };

            var lateMinutesTextBox = new TextBox
            {
                Name = "LateMinutesTextBox",
                Width = 50
            };

            latePanel.Children.Add(lateLabel);
            latePanel.Children.Add(lateMinutesTextBox);

            attendanceComboBox.SelectionChanged += (sender, e) =>
            {
                if (sender is ComboBox cb && cb.SelectedItem is string selected)
                    latePanel.IsVisible = selected == "Késik";
            };

            studentPanel.Children.Add(nameText);
            studentPanel.Children.Add(attendanceComboBox);
            studentPanel.Children.Add(latePanel);

            _studentListPanel.Children.Add(studentPanel);
        }
    }

    private void SaveAttendance(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _currentLessonId == null || _studentListPanel == null || _statusTextBlock == null ||
            _datePicker?.SelectedDate == null) return;

        var savedCount = 0;
        foreach (var child in _studentListPanel.Children)
            if (child is StackPanel studentPanel && studentPanel.Tag is User student)
            {
                var comboBox = studentPanel.FindControl<ComboBox>("AttendanceComboBox");
                if (comboBox?.SelectedItem is string state && state != "Jelen")
                {
                    var absenceData = new Dictionary<string, string>
                    {
                        { "StudentID", student.Id.ToString() },
                        { "LessonID", _currentLessonId.ToString() },
                        { "State", state },
                        { "Date", _datePicker.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }
                    };

                    if (state == "Késik")
                    {
                        var lateTextBox = studentPanel.FindControl<TextBox>("LateMinutesTextBox");
                        if (int.TryParse(lateTextBox?.Text, out var minutes) && minutes > 0)
                            absenceData.Add("LatenessInMinutes", minutes.ToString());
                        else
                            absenceData.Add("LatenessInMinutes", "0");
                    }

                    _context.SaveEntity("Absence", absenceData);
                    savedCount++;
                }
            }

        _statusTextBlock.Text = $"{savedCount} hiányzás/késés rögzítve.";
        if (_attendancePanel != null) _attendancePanel.IsVisible = false;
        if (_lessonCreationPanel != null) _lessonCreationPanel.IsVisible = true;
        _studentListPanel.Children.Clear();
    }
}