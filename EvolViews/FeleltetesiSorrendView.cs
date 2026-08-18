using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class FeleltetesiSorrendView : IEvolView
{
    private ITeacherContext? _context;
    private ComboBox? _classComboBox;
    private ListBox? _studentListBox;
    private TextBlock? _statusTextBlock;
    private List<User>? _currentStudents;
    private List<User>? _allStudents;
    private static readonly Random _rng = new Random();

    public new string Name => "Feleltetési Sorrend";
    public string Description => "Diákok sorrendjének véletlenszerű sorsolása és elmentése feleltetéshez.";

    public FeleltetesiSorrendView() { }

    public FeleltetesiSorrendView(ITeacherContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(20)
        };

        _classComboBox = new ComboBox
        {
            PlaceholderText = "Válassz osztályt...",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _classComboBox.SelectionChanged += ClassSelectionChanged;

        _studentListBox = new ListBox
        {
            Height = 300,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var randomizeButton = new Button
        {
            Content = "Sorrend Sorsolása",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        randomizeButton.Click += RandomizeOrder_Click;

        var saveButton = new Button
        {
            Content = "Sorrend Mentése",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        saveButton.Click += SaveOrder_Click;
        
        _statusTextBlock = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brushes.Green
        };

        mainPanel.Children.Add(new TextBlock { Text = "Osztály kiválasztása:", FontWeight = FontWeight.Bold });
        mainPanel.Children.Add(_classComboBox);
        mainPanel.Children.Add(new TextBlock { Text = "Diákok sorrendje:", Margin = new Thickness(0,10,0,0), FontWeight = FontWeight.Bold });
        mainPanel.Children.Add(_studentListBox);
        mainPanel.Children.Add(randomizeButton);
        mainPanel.Children.Add(saveButton);
        mainPanel.Children.Add(_statusTextBlock);

        LoadInitialData();

        return mainPanel;
    }

    private void LoadInitialData()
    {
        if (_context == null || _classComboBox == null) return;
        _classComboBox.ItemsSource = _context.GetAllClasses();
        _allStudents = _context.GetMyClassStudents();
    }

    private void ClassSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_classComboBox?.SelectedItem is not string selectedClass || _allStudents == null)
        {
            _currentStudents = null;
            if (_studentListBox != null) _studentListBox.ItemsSource = null;
            return;
        }

        _currentStudents = _allStudents.Where(s => s.ClassName == selectedClass).OrderBy(s => s.Name).ToList();
        UpdateStudentListBox();
        if (_statusTextBlock != null) _statusTextBlock.Text = string.Empty;
    }

    private void RandomizeOrder_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentStudents == null || _currentStudents.Count == 0)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Nincs diák a kiválasztott osztályban a sorsoláshoz.";
            return;
        }

        _currentStudents = _currentStudents.OrderBy(x => _rng.Next()).ToList();
        UpdateStudentListBox();
        if (_statusTextBlock != null) _statusTextBlock.Text = "Sikeres sorsolás! A mentéshez kattints a gombra.";
    }

    private void SaveOrder_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _classComboBox?.SelectedItem is not string selectedClass || _currentStudents == null || _currentStudents.Count == 0)
        {
            if (_statusTextBlock != null) 
            {
                _statusTextBlock.Text = "Hiba: Nincs kiválasztott osztály vagy nincsenek diákok a listában.";
                _statusTextBlock.Foreground = Brushes.Red;
            }
            return;
        }

        var studentIdOrder = string.Join(",", _currentStudents.Select(s => s.Id));

        var data = new Dictionary<string, string>
        {
            { "ClassName", selectedClass },
            { "StudentOrderIds", studentIdOrder },
            { "SavedAt", DateTime.UtcNow.ToString("o") }
        };

        try
        {
            _context.SaveEntity("RecitationOrder", data);
            if (_statusTextBlock != null)
            {
                _statusTextBlock.Text = "A sorrend sikeresen elmentve.";
                _statusTextBlock.Foreground = Brushes.Green;
            }
        }
        catch (Exception ex)
        {
            if (_statusTextBlock != null)
            {
                _statusTextBlock.Text = $"Mentési hiba: {ex.Message}";
                _statusTextBlock.Foreground = Brushes.Red;
            }
        }
    }

    private void UpdateStudentListBox()
    {
        if (_studentListBox == null || _currentStudents == null) return;

        _studentListBox.ItemsSource = _currentStudents.Select((student, index) => $"{index + 1}. {student.Name}").ToList();
    }
}
