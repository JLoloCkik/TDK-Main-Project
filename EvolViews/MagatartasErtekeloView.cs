using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;
using System.Globalization;

public class MagatartasErtekeloView : IEvolView
{
    private ITeacherContext? _context;
    private ComboBox? _classComboBox;
    private ComboBox? _studentComboBox;
    private ComboBox? _typeComboBox;
    private DatePicker? _datePicker;
    private TextBox? _descriptionTextBox;
    private Button? _saveButton;
    private TextBlock? _statusTextBlock;

    private List<User>? _allStudents;
    private List<string>? _classNames;
    private const string EntityType = "BehavioralNote";

    public new string Name => "Magatartási Értékelő";
    public string Description => "Dicséret vagy figyelmeztetés rögzítése egy diák számára.";

    public MagatartasErtekeloView() { }

    public MagatartasErtekeloView(ITeacherContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        _classComboBox = new ComboBox { PlaceholderText = "Válassz osztályt...", HorizontalAlignment = HorizontalAlignment.Stretch };
        _studentComboBox = new ComboBox { PlaceholderText = "Válassz diákot...", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
        _typeComboBox = new ComboBox { PlaceholderText = "Válassz típust...", HorizontalAlignment = HorizontalAlignment.Stretch };
        _typeComboBox.ItemsSource = new List<string> { "Dicséret", "Figyelmeztetés" };
        _datePicker = new DatePicker { SelectedDate = DateTime.Today, HorizontalAlignment = HorizontalAlignment.Stretch };
        _descriptionTextBox = new TextBox { PlaceholderText = "Értékelés szövege...", MinHeight = 80, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        _saveButton = new Button { Content = "Mentés", HorizontalAlignment = HorizontalAlignment.Stretch };
        _statusTextBlock = new TextBlock { Text = "", Margin = new Thickness(0, 10, 0, 0) };

        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(20)
        };

        mainPanel.Children.Add(new TextBlock { Text = "Magatartási Értékelés Rögzítése", FontSize = 18, FontWeight = FontWeight.Bold, Margin = new Thickness(0,0,0,10) });
        mainPanel.Children.Add(new TextBlock { Text = "Osztály:" });
        mainPanel.Children.Add(_classComboBox);
        mainPanel.Children.Add(new TextBlock { Text = "Diák:" });
        mainPanel.Children.Add(_studentComboBox);
        mainPanel.Children.Add(new TextBlock { Text = "Típus:" });
        mainPanel.Children.Add(_typeComboBox);
        mainPanel.Children.Add(new TextBlock { Text = "Dátum:" });
        mainPanel.Children.Add(_datePicker);
        mainPanel.Children.Add(new TextBlock { Text = "Leírás:" });
        mainPanel.Children.Add(_descriptionTextBox);
        mainPanel.Children.Add(_saveButton);
        mainPanel.Children.Add(_statusTextBlock);

        _classComboBox.SelectionChanged += OnClassSelectionChanged;
        _saveButton.Click += OnSaveButtonClick;

        LoadInitialData();

        return mainPanel;
    }

    private void LoadInitialData()
    {
        if (_context == null)
        {
            SetStatus("Hiba: A kontextus nem elérhető.", true);
            return;
        }

        _allStudents = _context.GetMyClassStudents();
        if (_allStudents != null && _allStudents.Any())
        {
            _classNames = _allStudents.Select(s => s.ClassName).Distinct().OrderBy(cn => cn).ToList();
            if (_classComboBox != null)
            {
                _classComboBox.ItemsSource = _classNames;
            }
        }
        else
        {
            SetStatus("Nincsenek diákok hozzárendelve.", true);
            if (_classComboBox != null) _classComboBox.IsEnabled = false;
        }
    }

    private void OnClassSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_studentComboBox == null || _allStudents == null || _classComboBox == null || _classComboBox.SelectedItem == null) return;
        
        string selectedClass = _classComboBox.SelectedItem.ToString() ?? "";
        var studentsInClass = _allStudents
            .Where(s => s.ClassName == selectedClass)
            .OrderBy(s => s.Name)
            .ToList();
        
        _studentComboBox.ItemsSource = studentsInClass.Select(s => s.Name).ToList();
        _studentComboBox.IsEnabled = studentsInClass.Any();
        _studentComboBox.SelectedIndex = -1;
    }

    private void OnSaveButtonClick(object? sender, RoutedEventArgs e)
    {
        SaveAssessment();
    }

    private void SaveAssessment()
    {
        if (_context == null || _classComboBox == null || _studentComboBox == null || _typeComboBox == null || _datePicker == null || _descriptionTextBox == null) return;

        if (_classComboBox.SelectedItem == null || _studentComboBox.SelectedItem == null || _typeComboBox.SelectedItem == null || string.IsNullOrWhiteSpace(_descriptionTextBox.Text) || _datePicker.SelectedDate == null)
        {
            SetStatus("Hiba: Minden mező kitöltése kötelező!", true);
            return;
        }

        var selectedClassName = _classComboBox.SelectedItem.ToString();
        var selectedStudentName = _studentComboBox.SelectedItem.ToString();
        var selectedStudent = _allStudents?.FirstOrDefault(s => s.ClassName == selectedClassName && s.Name == selectedStudentName);

        if (selectedStudent == null)
        {
            SetStatus("Hiba: A kiválasztott diák nem található!", true);
            return;
        }

        var data = new Dictionary<string, string>
        {
            { "StudentId", selectedStudent.Id.ToString() },
            { "StudentName", selectedStudent.Name },
            { "ClassName", selectedStudent.ClassName },
            { "AssessmentType", _typeComboBox.SelectedItem.ToString()! },
            { "Description", _descriptionTextBox.Text },
            { "Date", _datePicker.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }
        };

        try
        {
            _context.SaveEntity(EntityType, data);
            SetStatus("Értékelés sikeresen elmentve.", false);
            ClearForm();
        }
        catch (Exception ex)
        {
            SetStatus($"Hiba történt a mentés során: {ex.Message}", true);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        if (_statusTextBlock == null) return;
        _statusTextBlock.Text = message;
        _statusTextBlock.Foreground = isError ? Brushes.Red : Brushes.Green;
    }

    private void ClearForm()
    {
        if (_classComboBox != null) _classComboBox.SelectedIndex = -1;
        if (_studentComboBox != null)
        {
            _studentComboBox.ItemsSource = null;
            _studentComboBox.IsEnabled = false;
        }
        if (_typeComboBox != null) _typeComboBox.SelectedIndex = -1;
        if (_datePicker != null) _datePicker.SelectedDate = DateTime.Today;
        if (_descriptionTextBox != null) _descriptionTextBox.Text = "";
    }
}
