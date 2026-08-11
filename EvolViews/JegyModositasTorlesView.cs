using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class JegyModositasTorlesView : IEvolView
{
    private readonly ITeacherContext? _teacherContext;
    private List<User>? _students;
    private List<GenericRecord>? _allGrades;
    private List<GenericRecord>? _subjects;
    private List<GenericRecord>? _selectedStudentGrades;

    private ComboBox? _studentComboBox;
    private ListBox? _gradesListBox;
    private Button? _deleteButton;
    private TextBlock? _statusTextBlock;

    public JegyModositasTorlesView() { }

    public JegyModositasTorlesView(ITeacherContext context)
    {
        _teacherContext = context;
    }

    public new string Name => "Jegyek törlése";
    public string Description => "Diákok meglévő érdemjegyeinek törlése.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 10
        };

        if (_teacherContext == null)
        {
            mainPanel.Children.Add(new TextBlock { Text = "Hiba: A tanári kontextus nem elérhető." });
            return mainPanel;
        }
        
        try 
        {
            _students = _teacherContext.GetMyClassStudents();
            _allGrades = _teacherContext.QueryEntities("Grade");
            _subjects = _teacherContext.QueryEntities("Subject");
        }
        catch (Exception ex)
        {
            mainPanel.Children.Add(new TextBlock { Text = $"Hiba az adatok betöltése közben: {ex.Message}", Foreground = Brushes.Red });
            return mainPanel;
        }

        _studentComboBox = new ComboBox
        {
            PlaceholderText = "Válasszon diákot...",
            ItemsSource = _students?.Select(s => s.Name).ToList(),
            Width = 250
        };
        _studentComboBox.SelectionChanged += StudentComboBox_SelectionChanged;

        _gradesListBox = new ListBox
        {
            Height = 200,
            Width = 400
        };
        _gradesListBox.SelectionChanged += GradesListBox_SelectionChanged;

        _deleteButton = new Button
        {
            Content = "Kijelölt jegy törlése",
            IsEnabled = false
        };
        _deleteButton.Click += DeleteButton_Click;
        
        _statusTextBlock = new TextBlock();

        mainPanel.Children.Add(new TextBlock { Text = "Diák kiválasztása:" });
        mainPanel.Children.Add(_studentComboBox);
        mainPanel.Children.Add(new TextBlock { Text = "A diák jegyei:", Margin=new Thickness(0,10,0,0) });
        mainPanel.Children.Add(_gradesListBox);
        mainPanel.Children.Add(_deleteButton);
        mainPanel.Children.Add(_statusTextBlock);

        return mainPanel;
    }

    private void UpdateGradesForSelectedStudent()
    {
        if (_studentComboBox == null || _studentComboBox.SelectedIndex == -1 || _students == null || _allGrades == null || _gradesListBox == null || _deleteButton == null)
            return;

        var selectedStudent = _students[_studentComboBox.SelectedIndex];
        var studentIdStr = selectedStudent.Id.ToString();

        _selectedStudentGrades = _allGrades.Where(g => g.Data.TryGetValue("StudentId", out var id) && id == studentIdStr).ToList();

        var subjectDict = _subjects?.ToDictionary(s => s.Id.ToString(), s => s.Data.TryGetValue("Name", out var name) ? name : "Ismeretlen") ?? new Dictionary<string, string>();

        _gradesListBox.ItemsSource = _selectedStudentGrades.Select(g => {
            g.Data.TryGetValue("SubjectId", out var subjectId);
            var subjectName = (subjectId != null && subjectDict.ContainsKey(subjectId)) ? subjectDict[subjectId] : "N/A";
            g.Data.TryGetValue("Value", out var gradeValue);
            g.Data.TryGetValue("Date", out var gradeDate);
            return $"Tantárgy: {subjectName}, Jegy: {gradeValue}, Dátum: {gradeDate}";
        }).ToList();

        _deleteButton.IsEnabled = false;
        if (_gradesListBox.Items.Count > 0)
        {
             _gradesListBox.SelectedIndex = -1;
        }
    }

    private void StudentComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_statusTextBlock != null) _statusTextBlock.Text = string.Empty;
        UpdateGradesForSelectedStudent();
    }

    private void GradesListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_deleteButton != null)
        {
            _deleteButton.IsEnabled = _gradesListBox?.SelectedIndex != -1;
        }
    }

    private void DeleteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_teacherContext == null || _gradesListBox?.SelectedIndex == -1 || _selectedStudentGrades == null || _statusTextBlock == null)
            return;

        try
        {
            var selectedGrade = _selectedStudentGrades[_gradesListBox.SelectedIndex];
            _teacherContext.DeleteEntity("Grade", selectedGrade.Id);
            
            _statusTextBlock.Text = "A jegy sikeresen törölve.";
            _statusTextBlock.Foreground = Brushes.Green;

            _allGrades = _teacherContext.QueryEntities("Grade");
            
            UpdateGradesForSelectedStudent();
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba a törlés során: {ex.Message}";
            _statusTextBlock.Foreground = Brushes.Red;
        }
    }
}