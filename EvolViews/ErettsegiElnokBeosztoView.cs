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

public class ErettsegiElnokBeosztoView : IEvolView
{
    private const string ExamEntityType = "GraduationExam";
    private readonly IDirectorContext? _context;
    private StackPanel? _detailsPanel;

    private ListBox? _examListBox;

    private List<GenericRecord>? _exams;
    private ComboBox? _presidentComboBox;
    private Button? _saveButton;
    private GenericRecord? _selectedExam;
    private TextBlock? _selectedExamTextBlock;
    private TextBlock? _statusTextBlock;
    private List<User>? _teachers;

    public ErettsegiElnokBeosztoView()
    {
    }

    public ErettsegiElnokBeosztoView(IDirectorContext context)
    {
        _context = context;
    }

    public new string Name => "Érettségi Elnök Beosztása";
    public string Description => "Vizsgaelnökök hozzárendelése az érettségi vizsgákhoz.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(10)
        };

        _statusTextBlock = new TextBlock { Height = 20 };

        _examListBox = new ListBox
        {
            Height = 200,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _examListBox.SelectionChanged += ExamListBox_SelectionChanged;

        _detailsPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            IsVisible = false
        };

        _selectedExamTextBlock = new TextBlock();

        _presidentComboBox = new ComboBox
        {
            PlaceholderText = "Válasszon elnököt...",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _saveButton = new Button
        {
            Content = "Hozzárendelés mentése",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _saveButton.Click += SaveButton_Click;

        _detailsPanel.Children.Add(new TextBlock { Text = "Kiválasztott vizsga:", FontWeight = FontWeight.Bold });
        _detailsPanel.Children.Add(_selectedExamTextBlock);
        _detailsPanel.Children.Add(new TextBlock { Text = "Vizsgaelnök:", Margin = new Thickness(0, 10, 0, 0) });
        _detailsPanel.Children.Add(_presidentComboBox);
        _detailsPanel.Children.Add(_saveButton);

        mainPanel.Children.Add(
            new TextBlock { Text = "Érettségi vizsgák", FontSize = 18, FontWeight = FontWeight.Bold });
        mainPanel.Children.Add(_examListBox);
        mainPanel.Children.Add(_detailsPanel);
        mainPanel.Children.Add(_statusTextBlock);

        LoadData();

        return mainPanel;
    }

    private void LoadData()
    {
        if (_context == null) return;

        try
        {
            _exams = _context.QueryEntities(ExamEntityType);
            _teachers = _context.GetAllUsers().Where(u => u.Role == Role.Teacher).ToList();

            if (_examListBox != null)
                _examListBox.ItemsSource = _exams.Select(e =>
                        $"Vizsga: {e.Data.GetValueOrDefault("ClassName", "?")} osztály - {e.Data.GetValueOrDefault("ExamDate", "?")}")
                    .ToList();

            if (_presidentComboBox != null) _presidentComboBox.ItemsSource = _teachers.Select(t => t.Name).ToList();
        }
        catch (Exception ex)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = $"Adatok betöltése sikertelen: {ex.Message}";
        }
    }

    private void ExamListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_examListBox == null || _examListBox.SelectedIndex == -1 || _exams == null || _teachers == null ||
            _detailsPanel == null)
        {
            if (_detailsPanel != null) _detailsPanel.IsVisible = false;
            return;
        }

        _selectedExam = _exams[_examListBox.SelectedIndex];
        _detailsPanel.IsVisible = true;

        if (_selectedExamTextBlock != null)
            _selectedExamTextBlock.Text =
                $"{_selectedExam.Data.GetValueOrDefault("ClassName", "?")} - {_selectedExam.Data.GetValueOrDefault("ExamDate", "?")}";

        if (_presidentComboBox != null)
        {
            if (_selectedExam.Data.TryGetValue("PresidentUserId", out var presidentIdStr) &&
                int.TryParse(presidentIdStr, out var presidentId))
            {
                var president = _teachers.FirstOrDefault(t => t.Id == presidentId);
                if (president != null)
                    _presidentComboBox.SelectedItem = president.Name;
                else
                    _presidentComboBox.SelectedIndex = -1;
            }
            else
            {
                _presidentComboBox.SelectedIndex = -1;
            }
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _selectedExam == null || _presidentComboBox == null ||
            _presidentComboBox.SelectedIndex == -1 || _teachers == null)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Válasszon vizsgát és elnököt a mentéshez!";
            return;
        }

        try
        {
            var selectedTeacher = _teachers[_presidentComboBox.SelectedIndex];
            _selectedExam.Data["PresidentUserId"] = selectedTeacher.Id.ToString();

            _context.SaveEntity(ExamEntityType, _selectedExam.Data, _selectedExam.Id);

            if (_statusTextBlock != null) _statusTextBlock.Text = "Sikeres mentés!";
            LoadData(); // Refresh list to show changes
            if (_detailsPanel != null) _detailsPanel.IsVisible = false;
            if (_examListBox != null) _examListBox.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = $"Hiba mentés közben: {ex.Message}";
        }
    }
}