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

public class KorrepetalasEgyeztetoView : IEvolView
{
    private const string EntityType = "TutoringAppointment";
    private readonly ITeacherContext? _context;

    private List<GenericRecord>? _allAppointments;
    private List<Subject>? _allSubjects;
    private ListBox? _appointmentsListBox;
    private DatePicker? _datePicker;
    private StackPanel? _editPanel;
    private TextBox? _locationTextBox;
    private NumericUpDown? _maxStudentsNumericUpDown;
    private Button? _newButton;
    private Button? _saveButton;
    private GenericRecord? _selectedAppointment;
    private TextBlock? _statusTextBlock;
    private ComboBox? _subjectComboBox;
    private TextBox? _timeTextBox;

    public KorrepetalasEgyeztetoView()
    {
    }

    public KorrepetalasEgyeztetoView(ITeacherContext context)
    {
        _context = context;
    }

    public new string Name => "Korrepetálási Időpontok";
    public string Description => "Korrepetálási időpontok meghirdetése és kezelése, létszámkorláttal.";

    public Control CreateView()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*")
        };

        // Left Panel for list
        var leftPanel = new StackPanel { Margin = new Thickness(10) };
        _appointmentsListBox = new ListBox { Height = 400 };
        _appointmentsListBox.SelectionChanged += AppointmentsListBox_SelectionChanged;
        _newButton = new Button { Content = "Új időpont" };
        _newButton.Click += NewButton_Click;
        leftPanel.Children.Add(new TextBlock
            { Text = "Meghirdetett időpontok", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 10) });
        leftPanel.Children.Add(_appointmentsListBox);
        leftPanel.Children.Add(_newButton);

        Grid.SetColumn(leftPanel, 0);
        mainGrid.Children.Add(leftPanel);

        // Right Panel for editing
        _editPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10, IsEnabled = false };
        _subjectComboBox = new ComboBox { PlaceholderText = "Tantárgy" };
        _datePicker = new DatePicker();
        _timeTextBox = new TextBox { PlaceholderText = "Időpont (pl. 14:30)" };
        _locationTextBox = new TextBox { PlaceholderText = "Helyszín" };
        _maxStudentsNumericUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 50,
            Increment = 1,
            Value = 5
        };

        var maxStudentPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        maxStudentPanel.Children.Add(new TextBlock
            { Text = "Maximum létszám:", VerticalAlignment = VerticalAlignment.Center });
        maxStudentPanel.Children.Add(_maxStudentsNumericUpDown);

        _saveButton = new Button { Content = "Mentés" };
        _saveButton.Click += SaveAppointment;
        _statusTextBlock = new TextBlock
            { Text = "Válasszon egy időpontot a szerkesztéshez, vagy hozzon létre újat.", Foreground = Brushes.Gray };

        _editPanel.Children.Add(new TextBlock { Text = "Időpont adatai", FontWeight = FontWeight.Bold });
        _editPanel.Children.Add(_subjectComboBox);
        _editPanel.Children.Add(_datePicker);
        _editPanel.Children.Add(_timeTextBox);
        _editPanel.Children.Add(_locationTextBox);
        _editPanel.Children.Add(maxStudentPanel);
        _editPanel.Children.Add(_saveButton);
        _editPanel.Children.Add(_statusTextBlock);

        Grid.SetColumn(_editPanel, 1);
        mainGrid.Children.Add(_editPanel);

        LoadSubjects();
        LoadAppointments();

        return mainGrid;
    }

    private void LoadAppointments()
    {
        if (_context == null || _appointmentsListBox == null) return;
        _allAppointments = _context.QueryEntities(EntityType).OrderByDescending(a => a.Data["Date"]).ToList();
        _appointmentsListBox.ItemsSource = _allAppointments.Select(a =>
        {
            a.Data.TryGetValue("SubjectName", out var subject);
            a.Data.TryGetValue("Date", out var date);
            return $"{subject} - {date}";
        }).ToList();
    }

    private void LoadSubjects()
    {
        if (_context == null || _subjectComboBox == null) return;
        _allSubjects = _context.GetAllSubjects();
        _subjectComboBox.ItemsSource = _allSubjects.Select(s => s.Name).ToList();
    }

    private void AppointmentsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_appointmentsListBox?.SelectedItem == null || _editPanel == null || _allAppointments == null) return;

        _selectedAppointment = _allAppointments[_appointmentsListBox.SelectedIndex];
        _editPanel.IsEnabled = true;

        if (_subjectComboBox != null && _selectedAppointment.Data.TryGetValue("SubjectName", out var subjectName))
            _subjectComboBox.SelectedItem = subjectName;
        if (_datePicker != null && _selectedAppointment.Data.TryGetValue("Date", out var dateStr) &&
            DateTime.TryParse(dateStr, out var date))
            _datePicker.SelectedDate = date;
        if (_timeTextBox != null && _selectedAppointment.Data.TryGetValue("Time", out var time))
            _timeTextBox.Text = time;
        if (_locationTextBox != null && _selectedAppointment.Data.TryGetValue("Location", out var location))
            _locationTextBox.Text = location;
        if (_maxStudentsNumericUpDown != null)
        {
            if (_selectedAppointment.Data.TryGetValue("MaxStudents", out var maxStr) &&
                int.TryParse(maxStr, out var maxVal))
                _maxStudentsNumericUpDown.Value = maxVal;
            else
                _maxStudentsNumericUpDown.Value = 5; // Default
        }

        if (_statusTextBlock != null) _statusTextBlock.Text = "Adatok betöltve.";
    }

    private void NewButton_Click(object? sender, RoutedEventArgs e)
    {
        _selectedAppointment = null;
        if (_appointmentsListBox != null) _appointmentsListBox.SelectedItem = null;
        ClearAndEnableEditPanel();
        if (_statusTextBlock != null) _statusTextBlock.Text = "Adja meg az új időpont adatait.";
    }

    private void SaveAppointment(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _subjectComboBox?.SelectedItem == null || _datePicker?.SelectedDate == null ||
            string.IsNullOrWhiteSpace(_timeTextBox?.Text) || string.IsNullOrWhiteSpace(_locationTextBox?.Text) ||
            _maxStudentsNumericUpDown == null)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Hiba: Minden mező kitöltése kötelező!";
            return;
        }

        var selectedSubject = _allSubjects?.FirstOrDefault(s => s.Name == _subjectComboBox.SelectedItem.ToString());
        if (selectedSubject == null) return;

        var data = new Dictionary<string, string>
        {
            ["SubjectId"] = selectedSubject.Id.ToString(),
            ["SubjectName"] = selectedSubject.Name,
            ["Date"] = _datePicker.SelectedDate.Value.ToString("yyyy-MM-dd"),
            ["Time"] = _timeTextBox.Text,
            ["Location"] = _locationTextBox.Text,
            ["MaxStudents"] = ((int)_maxStudentsNumericUpDown.Value).ToString(CultureInfo.InvariantCulture)
        };

        var idToSave = _selectedAppointment?.Id;
        _context.SaveEntity(EntityType, data, idToSave);

        if (_statusTextBlock != null) _statusTextBlock.Text = "Időpont sikeresen mentve!";
        LoadAppointments();
        ClearAndEnableEditPanel();
    }

    private void ClearAndEnableEditPanel()
    {
        if (_editPanel == null) return;
        _editPanel.IsEnabled = true;
        if (_subjectComboBox != null) _subjectComboBox.SelectedIndex = -1;
        if (_datePicker != null) _datePicker.SelectedDate = DateTime.Today;
        if (_timeTextBox != null) _timeTextBox.Text = string.Empty;
        if (_locationTextBox != null) _locationTextBox.Text = string.Empty;
        if (_maxStudentsNumericUpDown != null) _maxStudentsNumericUpDown.Value = 5;
    }
}