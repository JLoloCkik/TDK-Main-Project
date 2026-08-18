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

public class HaziFeladatKezeloView : IEvolView
{
    private const string HomeworkEntityType = "Homework";
    private readonly ITeacherContext? _context;

    private List<GenericRecord>? _allHomework;
    private TextBox? _attachmentUrlTextBox;
    private ComboBox? _classComboBox;
    private List<string>? _classes;
    private DatePicker? _deadlinePicker;
    private TextBox? _descriptionTextBox;
    private StackPanel? _editPanel;

    private ListBox? _homeworkListBox;
    private Button? _newButton;
    private Button? _saveButton;
    private GenericRecord? _selectedHomework;
    private TextBlock? _statusTextBlock;
    private ComboBox? _subjectComboBox;
    private List<Subject>? _subjects;

    public HaziFeladatKezeloView()
    {
    }

    public HaziFeladatKezeloView(ITeacherContext context)
    {
        _context = context;
    }

    public new string Name => "Házi Feladat Kezelő";
    public string Description => "Házi feladatok kiírása, szerkesztése és mellékletek hozzáadása.";

    public Control CreateView()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*")
        };

        // Left Panel for List and New button
        var leftPanel = new StackPanel { Spacing = 10, Margin = new Thickness(10) };
        _homeworkListBox = new ListBox { Height = 400 };
        _homeworkListBox.SelectionChanged += HomeworkSelectionChanged;
        leftPanel.Children.Add(new TextBlock { Text = "Kiosztott feladatok:", FontWeight = FontWeight.Bold });
        leftPanel.Children.Add(_homeworkListBox);
        _newButton = new Button { Content = "Új feladat kiírása" };
        _newButton.Click += NewButton_Click;
        leftPanel.Children.Add(_newButton);
        Grid.SetColumn(leftPanel, 0);

        // Right Panel for Editing
        _editPanel = new StackPanel { Spacing = 10, Margin = new Thickness(10), IsVisible = false };

        _classComboBox = new ComboBox { PlaceholderText = "Osztály" };
        _subjectComboBox = new ComboBox { PlaceholderText = "Tantárgy" };
        _descriptionTextBox = new TextBox
        {
            PlaceholderText = "Feladat leírása", Height = 120, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap
        };
        _deadlinePicker = new DatePicker();
        _attachmentUrlTextBox = new TextBox { PlaceholderText = "Melléklet URL (nem kötelező)" };
        _saveButton = new Button { Content = "Mentés" };
        _saveButton.Click += SaveButton_Click;

        _editPanel.Children.Add(new TextBlock { Text = "Feladat adatai:", FontWeight = FontWeight.Bold });
        _editPanel.Children.Add(CreateFormRow("Osztály:", _classComboBox));
        _editPanel.Children.Add(CreateFormRow("Tantárgy:", _subjectComboBox));
        _editPanel.Children.Add(CreateFormRow("Leírás:", _descriptionTextBox));
        _editPanel.Children.Add(CreateFormRow("Határidő:", _deadlinePicker));
        _editPanel.Children.Add(CreateFormRow("Melléklet URL:", _attachmentUrlTextBox));
        _editPanel.Children.Add(_saveButton);
        Grid.SetColumn(_editPanel, 1);

        _statusTextBlock = new TextBlock { Margin = new Thickness(10), VerticalAlignment = VerticalAlignment.Bottom };

        var containerPanel = new StackPanel();
        containerPanel.Children.Add(mainGrid);
        containerPanel.Children.Add(_statusTextBlock);

        LoadInitialData();

        return containerPanel;
    }

    private StackPanel CreateFormRow(string label, Control control)
    {
        var panel = new StackPanel { Spacing = 5 };
        panel.Children.Add(new TextBlock { Text = label });
        panel.Children.Add(control);
        return panel;
    }

    private void LoadInitialData()
    {
        if (_context == null) return;
        try
        {
            _classes = _context.GetAllClasses();
            _subjects = _context.GetAllSubjects();
            _allHomework = _context.QueryEntities(HomeworkEntityType);

            if (_classComboBox != null) _classComboBox.ItemsSource = _classes;
            if (_subjectComboBox != null) _subjectComboBox.ItemsSource = _subjects?.Select(s => s.Name).ToList();

            UpdateHomeworkList();
        }
        catch (Exception ex)
        {
            SetStatus("Hiba az adatok betöltésekor: " + ex.Message);
        }
    }

    private void UpdateHomeworkList()
    {
        if (_homeworkListBox == null || _allHomework == null) return;
        _homeworkListBox.ItemsSource = _allHomework.OrderByDescending(h => h.CreatedAt).Select(h =>
        {
            h.Data.TryGetValue("Deadline", out var deadlineStr);
            return
                $"{h.Data["ClassName"]} - {deadlineStr} - {h.Data["Description"].Substring(0, Math.Min(h.Data["Description"].Length, 20))}...";
        }).ToList();
    }

    private void HomeworkSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_homeworkListBox == null || _homeworkListBox.SelectedItem == null)
        {
            ClearForm();
            return;
        }

        _selectedHomework = _allHomework?.FirstOrDefault(h =>
        {
            h.Data.TryGetValue("Deadline", out var deadlineStr);
            return
                $"{h.Data["ClassName"]} - {deadlineStr} - {h.Data["Description"].Substring(0, Math.Min(h.Data["Description"].Length, 20))}..." ==
                _homeworkListBox.SelectedItem.ToString();
        });

        if (_selectedHomework != null)
        {
            if (_editPanel != null) _editPanel.IsVisible = true;
            PopulateForm(_selectedHomework);
        }
    }

    private void PopulateForm(GenericRecord homework)
    {
        if (_classComboBox != null) _classComboBox.SelectedItem = homework.Data["ClassName"];
        if (_subjectComboBox != null && _subjects != null)
        {
            var subject = _subjects.FirstOrDefault(s => s.Id.ToString() == homework.Data["SubjectId"]);
            if (subject != null) _subjectComboBox.SelectedItem = subject.Name;
        }

        if (_descriptionTextBox != null) _descriptionTextBox.Text = homework.Data["Description"];
        if (_deadlinePicker != null &&
            DateTime.TryParse(homework.Data["Deadline"], CultureInfo.InvariantCulture, out var deadline))
            _deadlinePicker.SelectedDate = deadline;
        if (_attachmentUrlTextBox != null)
            _attachmentUrlTextBox.Text = homework.Data.TryGetValue("AttachmentUrl", out var url) ? url : string.Empty;
    }

    private void ClearForm()
    {
        if (_editPanel != null) _editPanel.IsVisible = false;
        if (_homeworkListBox != null) _homeworkListBox.SelectedItem = null;
        _selectedHomework = null;
        if (_classComboBox != null) _classComboBox.SelectedIndex = -1;
        if (_subjectComboBox != null) _subjectComboBox.SelectedIndex = -1;
        if (_descriptionTextBox != null) _descriptionTextBox.Text = "";
        if (_deadlinePicker != null) _deadlinePicker.SelectedDate = DateTime.Now.AddDays(1);
        if (_attachmentUrlTextBox != null) _attachmentUrlTextBox.Text = "";
        SetStatus("");
    }

    private void NewButton_Click(object? sender, RoutedEventArgs e)
    {
        ClearForm();
        if (_editPanel != null) _editPanel.IsVisible = true;
        SetStatus("Új házi feladat adatainak megadása.");
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _classComboBox?.SelectedItem == null || _subjectComboBox?.SelectedItem == null ||
            string.IsNullOrWhiteSpace(_descriptionTextBox?.Text) || _deadlinePicker?.SelectedDate == null)
        {
            SetStatus("Hiba: Az osztály, tantárgy, leírás és határidő megadása kötelező!");
            return;
        }

        var selectedSubject = _subjects?.FirstOrDefault(s => s.Name == _subjectComboBox.SelectedItem.ToString());
        if (selectedSubject == null)
        {
            SetStatus("Hiba: Érvénytelen tantárgy kiválasztva.");
            return;
        }

        var data = new Dictionary<string, string>
        {
            { "ClassName", _classComboBox.SelectedItem.ToString()! },
            { "SubjectId", selectedSubject.Id.ToString() },
            { "SubjectName", selectedSubject.Name },
            { "Description", _descriptionTextBox.Text! },
            { "Deadline", _deadlinePicker.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            { "AttachmentUrl", _attachmentUrlTextBox?.Text ?? string.Empty }
        };

        try
        {
            _context.SaveEntity(HomeworkEntityType, data, _selectedHomework?.Id);
            SetStatus("Házi feladat sikeresen mentve.");
            LoadInitialData(); // Refresh list
            ClearForm();
        }
        catch (Exception ex)
        {
            SetStatus("Hiba a mentés során: " + ex.Message);
        }
    }

    private void SetStatus(string message)
    {
        if (_statusTextBlock != null) _statusTextBlock.Text = message;
    }
}