using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class OsztalyfonokiKinevezoView : IEvolView
{
    private const string EntityType = "SchoolClassTeacher";
    private readonly IDirectorContext? _context;

    private ListBox? _classListBox;
    private List<GenericRecord>? _classTeacherRecords;

    private List<string>? _classes;
    private StackPanel? _detailsPanel;
    private Button? _saveButton;
    private string? _selectedClass;
    private TextBlock? _selectedClassNameTextBlock;
    private TextBlock? _statusTextBlock;
    private ComboBox? _teacherComboBox;
    private List<User>? _teachers;
    private TextBlock? _warningTextBlock;

    public OsztalyfonokiKinevezoView()
    {
    }

    public OsztalyfonokiKinevezoView(IDirectorContext context)
    {
        _context = context;
    }

    public new string Name => "Osztályfőnöki Kinevezés";

    public string Description =>
        "Tanárok hozzárendelése osztályokhoz osztályfőnökként, figyelmeztetéssel, ha egy tanár már rendelkezik osztállyal.";

    public Control CreateView()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("200,*"),
            Margin = new Thickness(10)
        };

        _classListBox = new ListBox();
        _classListBox.SelectionChanged += ClassListBox_SelectionChanged;
        Grid.SetColumn(_classListBox, 0);

        _detailsPanel = new StackPanel
        {
            IsVisible = false,
            Spacing = 10,
            Margin = new Thickness(15, 0, 0, 0)
        };
        Grid.SetColumn(_detailsPanel, 1);

        _selectedClassNameTextBlock = new TextBlock { FontWeight = FontWeight.Bold, FontSize = 16 };
        _teacherComboBox = new ComboBox
            { PlaceholderText = "Válassz tanárt...", HorizontalAlignment = HorizontalAlignment.Stretch };
        _teacherComboBox.SelectionChanged += TeacherComboBox_SelectionChanged;

        _warningTextBlock = new TextBlock
            { Foreground = Brushes.OrangeRed, IsVisible = false, TextWrapping = TextWrapping.Wrap };

        _saveButton = new Button { Content = "Kinevezés mentése" };
        _saveButton.Click += SaveButton_Click;

        _statusTextBlock = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };

        _detailsPanel.Children.Add(_selectedClassNameTextBlock);
        _detailsPanel.Children.Add(new TextBlock { Text = "Osztályfőnök:" });
        _detailsPanel.Children.Add(_teacherComboBox);
        _detailsPanel.Children.Add(_warningTextBlock);
        _detailsPanel.Children.Add(_saveButton);
        _detailsPanel.Children.Add(_statusTextBlock);

        mainGrid.Children.Add(_classListBox);
        mainGrid.Children.Add(_detailsPanel);

        LoadData();
        return mainGrid;
    }

    private void LoadData()
    {
        if (_context == null || _classListBox == null) return;

        try
        {
            _classes = _context.GetAllClasses();
            _teachers = _context.GetAllUsers().Where(u => u.Role == Role.Teacher).OrderBy(u => u.Name).ToList();
            _classTeacherRecords = _context.QueryEntities(EntityType);

            _classListBox.ItemsSource = _classes;
            if (_teacherComboBox != null) _teacherComboBox.ItemsSource = _teachers.Select(t => t.Name).ToList();
        }
        catch (Exception ex)
        {
            SetStatus("Hiba az adatok betöltése közben: " + ex.Message, true);
        }
    }

    private void ClassListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_classListBox?.SelectedItem is string selectedClass && _detailsPanel != null && _teacherComboBox != null &&
            _teachers != null)
        {
            _selectedClass = selectedClass;
            _detailsPanel.IsVisible = true;
            _selectedClassNameTextBlock!.Text = $"{_selectedClass} osztályfőnökének kinevezése";
            SetStatus("", false);
            _warningTextBlock!.IsVisible = false;

            var currentAssignment = _classTeacherRecords?.FirstOrDefault(r =>
                r.Data.ContainsKey("ClassName") && r.Data["ClassName"] == _selectedClass);
            if (currentAssignment != null && currentAssignment.Data.TryGetValue("TeacherId", out var teacherIdStr))
            {
                var assignedTeacher = _teachers.FirstOrDefault(t => t.Id.ToString() == teacherIdStr);
                if (assignedTeacher != null)
                    _teacherComboBox.SelectedIndex = _teachers.IndexOf(assignedTeacher);
                else
                    _teacherComboBox.SelectedIndex = -1;
            }
            else
            {
                _teacherComboBox.SelectedIndex = -1;
            }
        }
    }

    private void TeacherComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_warningTextBlock == null || _teacherComboBox == null || _teachers == null ||
            _classTeacherRecords == null) return;

        _warningTextBlock.IsVisible = false;
        _warningTextBlock.Text = "";

        var selectedIndex = _teacherComboBox.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= _teachers.Count) return;

        var selectedTeacher = _teachers[selectedIndex];
        var existingAssignment = _classTeacherRecords.FirstOrDefault(r =>
            r.Data.TryGetValue("TeacherId", out var teacherId) &&
            teacherId == selectedTeacher.Id.ToString() &&
            (!r.Data.TryGetValue("ClassName", out var className) || className != _selectedClass));

        if (existingAssignment != null)
        {
            var assignedClass = existingAssignment.Data.GetValueOrDefault("ClassName", "egy másik");
            _warningTextBlock.Text =
                $"Figyelem: {selectedTeacher.Name} már a(z) {assignedClass} osztály osztályfőnöke.";
            _warningTextBlock.IsVisible = true;
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _selectedClass == null || _teacherComboBox?.SelectedItem == null || _teachers == null)
        {
            SetStatus("Nincs kiválasztva osztály vagy tanár.", true);
            return;
        }

        try
        {
            var selectedTeacher = _teachers[_teacherComboBox.SelectedIndex];

            var existingRecord = _classTeacherRecords?.FirstOrDefault(r =>
                r.Data.ContainsKey("ClassName") && r.Data["ClassName"] == _selectedClass);
            var recordId = existingRecord?.Id;

            var data = new Dictionary<string, string>
            {
                { "ClassName", _selectedClass },
                { "TeacherId", selectedTeacher.Id.ToString() }
            };

            _context.SaveEntity(EntityType, data, recordId);
            SetStatus("Sikeres mentés!", false);
            LoadData();
        }
        catch (Exception ex)
        {
            SetStatus("Hiba a mentés során: " + ex.Message, true);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        if (_statusTextBlock == null) return;
        _statusTextBlock.Text = message;
        _statusTextBlock.Foreground = isError ? Brushes.Red : Brushes.Green;
    }
}