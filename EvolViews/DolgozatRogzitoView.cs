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

public class DolgozatRogzitoView : IEvolView
{
    private readonly ITeacherContext? _context;

    private ComboBox? _classComboBox;
    private DatePicker? _datePicker;
    private TextBlock? _statusTextBlock;
    private ComboBox? _subjectComboBox;
    private TextBox? _topicTextBox;

    public DolgozatRogzitoView()
    {
    }

    public DolgozatRogzitoView(ITeacherContext context)
    {
        _context = context;
    }

    public new string Name => "Dolgozat Időpont Rögzítése";
    public string Description => "Új dolgozat időpontjának felvétele egy osztály számára.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(20)
        };

        _statusTextBlock = new TextBlock
        {
            Text = "Kérlek add meg a dolgozat adatait.",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        _classComboBox = new ComboBox
        {
            PlaceholderText = "Válassz osztályt...",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _subjectComboBox = new ComboBox
        {
            PlaceholderText = "Válassz tantárgyat...",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _topicTextBox = new TextBox
        {
            PlaceholderText = "Dolgozat témája",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _datePicker = new DatePicker
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var saveButton = new Button
        {
            Content = "Dolgozat Rögzítése",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        saveButton.Click += SaveButton_Click;

        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(new TextBlock { Text = "Osztály:" });
        mainPanel.Children.Add(_classComboBox);
        mainPanel.Children.Add(new TextBlock { Text = "Tantárgy:" });
        mainPanel.Children.Add(_subjectComboBox);
        mainPanel.Children.Add(new TextBlock { Text = "Téma:" });
        mainPanel.Children.Add(_topicTextBox);
        mainPanel.Children.Add(new TextBlock { Text = "Dátum:" });
        mainPanel.Children.Add(_datePicker);
        mainPanel.Children.Add(saveButton);

        LoadInitialData();

        return mainPanel;
    }

    private void LoadInitialData()
    {
        if (_context == null || _classComboBox == null || _subjectComboBox == null) return;

        var classes = _context.GetAllClasses();
        if (classes != null) _classComboBox.ItemsSource = classes;

        var subjects = _context.GetAllSubjects();
        if (subjects != null) _subjectComboBox.ItemsSource = subjects.Select(s => s.Name).ToList();
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _classComboBox == null || _subjectComboBox == null || _topicTextBox == null ||
            _datePicker == null || _statusTextBlock == null) return;

        if (_classComboBox.SelectedItem == null || _subjectComboBox.SelectedItem == null ||
            string.IsNullOrWhiteSpace(_topicTextBox.Text) || !_datePicker.SelectedDate.HasValue)
        {
            _statusTextBlock.Text = "Hiba: Minden mező kitöltése kötelező!";
            _statusTextBlock.Foreground = Brushes.Red;
            return;
        }

        if (_datePicker.SelectedDate.Value.Date < DateTime.Today)
        {
            _statusTextBlock.Text = "Hiba: Nem adható meg múltbéli dátum!";
            _statusTextBlock.Foreground = Brushes.Red;
            return;
        }

        var className = _classComboBox.SelectedItem.ToString();
        var subjectName = _subjectComboBox.SelectedItem.ToString();
        var topic = _topicTextBox.Text;
        var date = _datePicker.SelectedDate.Value;

        var data = new Dictionary<string, string>
        {
            { "ClassName", className! },
            { "SubjectName", subjectName! },
            { "Topic", topic },
            { "Date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }
        };

        try
        {
            _context.SaveEntity("DolgozatIdopont", data);
            _statusTextBlock.Text = "Dolgozat sikeresen rögzítve!";
            _statusTextBlock.Foreground = Brushes.Green;

            _classComboBox.SelectedIndex = -1;
            _subjectComboBox.SelectedIndex = -1;
            _topicTextBox.Text = "";
            _datePicker.SelectedDate = null;
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba történt a mentés során: {ex.Message}";
            _statusTextBlock.Foreground = Brushes.Red;
        }
    }
}