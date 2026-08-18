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

public class SzuloiErtekezletView : IEvolView
{
    private const string EntityType = "ParentTeacherConference";
    private readonly ITeacherContext? _context;
    private ComboBox? _classComboBox;
    private List<string>? _classes;

    private ListBox? _conferenceListBox;

    private List<GenericRecord>? _conferences;
    private DatePicker? _datePicker;
    private TextBox? _locationTextBox;
    private Button? _saveButton;
    private TextBlock? _statusTextBlock;
    private TextBox? _timeTextBox;
    private TextBox? _topicTextBox;

    public SzuloiErtekezletView()
    {
    }

    public SzuloiErtekezletView(ITeacherContext context)
    {
        _context = context;
    }

    public new string Name => "Szülői Értekezlet Kezelő";
    public string Description => "Modul szülői értekezletek időpontjainak rögzítésére és megtekintésére.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel { Spacing = 10, Margin = new Thickness(10) };

        _statusTextBlock = new TextBlock
            { Text = "Itt hozhat létre új szülői értekezletet.", FontWeight = FontWeight.Bold };

        var formGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"),
            Margin = new Thickness(0, 5)
        };

        _classComboBox = new ComboBox { PlaceholderText = "Válasszon osztályt" };
        _datePicker = new DatePicker();
        _timeTextBox = new TextBox { PlaceholderText = "Időpont (pl. 17:00)" };
        _locationTextBox = new TextBox { PlaceholderText = "Helyszín (pl. 12-es terem)" };
        _topicTextBox = new TextBox { PlaceholderText = "Téma", Height = 80, TextWrapping = TextWrapping.Wrap };
        _saveButton = new Button { Content = "Értekezlet mentése" };
        _saveButton.Click += SaveButton_Click;

        AddFormControl(formGrid, "Osztály:", _classComboBox, 0);
        AddFormControl(formGrid, "Dátum:", _datePicker, 1);
        AddFormControl(formGrid, "Időpont:", _timeTextBox, 2);
        AddFormControl(formGrid, "Helyszín:", _locationTextBox, 3);
        AddFormControl(formGrid, "Téma:", _topicTextBox, 4);
        Grid.SetColumn(_saveButton, 0);
        Grid.SetRow(_saveButton, 5);
        Grid.SetColumnSpan(_saveButton, 2);
        formGrid.Children.Add(_saveButton);

        var existingLabel = new TextBlock { Text = "\nKiírt értekezletek:", FontWeight = FontWeight.Bold };
        _conferenceListBox = new ListBox { Height = 200 };

        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(formGrid);
        mainPanel.Children.Add(existingLabel);
        mainPanel.Children.Add(_conferenceListBox);

        LoadData();

        return mainPanel;
    }

    private void AddFormControl(Grid grid, string labelText, Control control, int row)
    {
        var label = new TextBlock
            { Text = labelText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        Grid.SetRow(label, row);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        Grid.SetRow(control, row);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
    }

    private void LoadData()
    {
        if (_context == null)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Hiba: A kontextus nincs beállítva.";
            return;
        }

        try
        {
            _classes = _context.GetAllClasses();
            if (_classComboBox != null) _classComboBox.ItemsSource = _classes;

            _conferences = _context.QueryEntities(EntityType);
            if (_conferenceListBox != null)
                _conferenceListBox.ItemsSource = _conferences
                    .OrderByDescending(c =>
                        c.Data.ContainsKey("Date") ? DateTime.Parse(c.Data["Date"]) : DateTime.MinValue)
                    .Select(c =>
                        $"{c.Data.GetValueOrDefault("ClassName", "?")} - {c.Data.GetValueOrDefault("Date", "?")} {c.Data.GetValueOrDefault("Time", "?")} - {c.Data.GetValueOrDefault("Topic", "Nincs téma")}")
                    .ToList();
        }
        catch (Exception ex)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = $"Adatok betöltése sikertelen: {ex.Message}";
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _classComboBox == null || _datePicker == null || _timeTextBox == null ||
            _locationTextBox == null || _topicTextBox == null || _statusTextBlock == null) return;

        if (_classComboBox.SelectedItem == null || string.IsNullOrWhiteSpace(_datePicker.SelectedDate?.ToString()) ||
            string.IsNullOrWhiteSpace(_timeTextBox.Text))
        {
            _statusTextBlock.Text = "Hiba: Az osztály, dátum és időpont megadása kötelező!";
            _statusTextBlock.Foreground = Brushes.Red;
            return;
        }

        var data = new Dictionary<string, string>
        {
            { "ClassName", _classComboBox.SelectedItem.ToString()! },
            { "Date", _datePicker.SelectedDate.Value.ToString("yyyy-MM-dd") },
            { "Time", _timeTextBox.Text },
            { "Location", _locationTextBox.Text ?? string.Empty },
            { "Topic", _topicTextBox.Text ?? string.Empty }
        };

        try
        {
            var newId = _context.SaveEntity(EntityType, data);
            _statusTextBlock.Text = $"Értekezlet sikeresen mentve (ID: {newId})!";
            _statusTextBlock.Foreground = Brushes.Green;

            _timeTextBox.Text = "";
            _locationTextBox.Text = "";
            _topicTextBox.Text = "";

            LoadData();
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba mentés közben: {ex.Message}";
            _statusTextBlock.Foreground = Brushes.Red;
        }
    }
}