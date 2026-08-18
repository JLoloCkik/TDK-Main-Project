using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Kreta.Contexts;
using Kreta.Core;

public class HaziFeladatokView : IEvolView
{
    private const string HomeworkEntityType = "Homework";
    private readonly IStudentContext? _context;
    private ListBox? _homeworkListBox;
    private List<GenericRecord>? _myHomework;
    private TextBlock? _statusTextBlock;
    private ComboBox? _subjectComboBox;

    public HaziFeladatokView()
    {
    }

    public HaziFeladatokView(IStudentContext context)
    {
        _context = context;
    }

    public new string Name => "Házi Feladatok";
    public string Description => "A diák házi feladatainak listázása tantárgyi szűrési lehetőséggel.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(15)
        };

        _statusTextBlock = new TextBlock
        {
            Text = "Adatok betöltése...",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10)
        };

        _subjectComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false
        };
        _subjectComboBox.SelectionChanged += OnFilterChanged;

        var filterPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                new TextBlock { Text = "Szűrés tantárgyra:", VerticalAlignment = VerticalAlignment.Center },
                _subjectComboBox
            }
        };

        _homeworkListBox = new ListBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 400
        };

        mainPanel.Children.Add(filterPanel);
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(_homeworkListBox);

        LoadData();

        return mainPanel;
    }

    private void LoadData()
    {
        if (_context == null || _statusTextBlock == null)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Hiba: A nézet nincs megfelelően inicializálva.";
            return;
        }

        var currentUser = _context.GetMyProfile();
        if (currentUser == null)
        {
            _statusTextBlock.Text = "Hiba: Felhasználói profil nem található.";
            return;
        }

        var allHomework = _context.QueryEntities(HomeworkEntityType);
        _myHomework = allHomework
            .Where(h => h.Data.ContainsKey("ClassName") && h.Data["ClassName"] == currentUser.ClassName)
            .OrderByDescending(h => h.Data.ContainsKey("DueDate") ? h.Data["DueDate"] : string.Empty)
            .ToList();

        if (!_myHomework.Any())
        {
            _statusTextBlock.Text = "Nincsenek aktív házi feladatok.";
            _statusTextBlock.IsVisible = true;
        }
        else
        {
            _statusTextBlock.IsVisible = false;
            PopulateFilter();
            UpdateDisplay();
        }
    }

    private void PopulateFilter()
    {
        if (_myHomework == null || _subjectComboBox == null) return;

        var subjects = _myHomework
            .Where(h => h.Data.ContainsKey("SubjectName"))
            .Select(h => h.Data["SubjectName"])
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        subjects.Insert(0, "Minden tantárgy");

        _subjectComboBox.ItemsSource = subjects;
        _subjectComboBox.SelectedIndex = 0;
        _subjectComboBox.IsEnabled = true;
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_myHomework == null || _subjectComboBox == null || _homeworkListBox == null ||
            _statusTextBlock == null) return;

        var selectedSubject = _subjectComboBox.SelectedItem as string;

        IEnumerable<GenericRecord> filteredHomework = _myHomework;

        if (selectedSubject != null && selectedSubject != "Minden tantárgy")
            filteredHomework = _myHomework.Where(h =>
                h.Data.ContainsKey("SubjectName") && h.Data["SubjectName"] == selectedSubject);

        var displayItems = filteredHomework.Select(h =>
        {
            var subject = h.Data.GetValueOrDefault("SubjectName", "N/A");
            var dueDate = h.Data.GetValueOrDefault("DueDate", "N/A");
            var description = h.Data.GetValueOrDefault("Description", "Nincs leírás.");
            return $"Tantárgy: {subject}\nHatáridő: {dueDate}\nFeladat: {description}";
        }).ToList();

        _homeworkListBox.ItemsSource = displayItems;

        if (!displayItems.Any())
        {
            _statusTextBlock.Text = "A szűrési feltételeknek megfelelő házi feladat nem található.";
            _statusTextBlock.IsVisible = true;
            _homeworkListBox.IsVisible = false;
        }
        else
        {
            _statusTextBlock.IsVisible = false;
            _homeworkListBox.IsVisible = true;
        }
    }
}