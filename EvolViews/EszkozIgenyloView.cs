using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;
using System.Globalization;

public class EszkozIgenyloView : IEvolView
{
    private ITeacherContext? _context;

    private ComboBox? _equipmentComboBox;
    private TextBox? _quantityTextBox;
    private TextBox? _reasonTextBox;
    private Button? _submitButton;
    private TextBlock? _statusTextBlock;
    private ListBox? _myRequestsListBox;

    private const string EntityType = "LabEquipmentRequest";

    public new string Name => "Szertári Eszközigénylő";
    public string Description => "Laboratóriumi eszközök igénylése tanórákhoz és kísérletekhez.";

    public EszkozIgenyloView() { }

    public EszkozIgenyloView(ITeacherContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        _statusTextBlock = new TextBlock { Text = "Töltse ki az űrlapot az igényléshez.", Margin = new Avalonia.Thickness(5) };

        var formGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            Margin = new Avalonia.Thickness(10)
        };

        var equipmentLabel = new TextBlock { Text = "Eszköz:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(equipmentLabel, 0);
        Grid.SetColumn(equipmentLabel, 0);

        _equipmentComboBox = new ComboBox
        {
            ItemsSource = new List<string> { "Főzőpohár", "Kémcső", "Bunsen-égő", "Mikroszkóp", "Petri-csésze", "Mérőhenger" },
            SelectedIndex = 0,
            Margin = new Avalonia.Thickness(5)
        };
        Grid.SetRow(_equipmentComboBox, 0);
        Grid.SetColumn(_equipmentComboBox, 1);

        var quantityLabel = new TextBlock { Text = "Mennyiség:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(quantityLabel, 1);
        Grid.SetColumn(quantityLabel, 0);

        _quantityTextBox = new TextBox { Margin = new Avalonia.Thickness(5) };
        Grid.SetRow(_quantityTextBox, 1);
        Grid.SetColumn(_quantityTextBox, 1);

        var reasonLabel = new TextBlock { Text = "Indoklás:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(reasonLabel, 2);
        Grid.SetColumn(reasonLabel, 0);

        _reasonTextBox = new TextBox { Margin = new Avalonia.Thickness(5) };
        Grid.SetRow(_reasonTextBox, 2);
        Grid.SetColumn(_reasonTextBox, 1);

        _submitButton = new Button { Content = "Igénylés elküldése", Margin = new Avalonia.Thickness(5) };
        _submitButton.Click += SubmitRequest_Click;
        Grid.SetRow(_submitButton, 3);
        Grid.SetColumn(_submitButton, 1);

        formGrid.Children.Add(equipmentLabel);
        formGrid.Children.Add(_equipmentComboBox);
        formGrid.Children.Add(quantityLabel);
        formGrid.Children.Add(_quantityTextBox);
        formGrid.Children.Add(reasonLabel);
        formGrid.Children.Add(_reasonTextBox);
        formGrid.Children.Add(_submitButton);
        
        var requestHeader = new TextBlock 
        {
             Text = "Leadott igényléseim", 
             FontWeight = FontWeight.Bold, 
             Margin = new Avalonia.Thickness(10,20,10,5)
        };

        _myRequestsListBox = new ListBox { Margin = new Avalonia.Thickness(10) };

        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Children =
            {
                new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(5),
                    Padding = new Avalonia.Thickness(10),
                    Child = new StackPanel
                    {
                        Children = 
                        {
                             new TextBlock { Text = "Új igénylés leadása", FontWeight = FontWeight.Bold, Margin=new Avalonia.Thickness(0,0,0,10) },
                             formGrid
                        }
                    }
                },
                _statusTextBlock,
                requestHeader,
                _myRequestsListBox
            }
        };
        
        LoadMyRequests();

        return mainPanel;
    }

    private void LoadMyRequests()
    {
        if (_context == null || _myRequestsListBox == null) return;
        try
        {
            var allRequests = _context.QueryEntities(EntityType);
            // Mivel a kontextus nem adja vissza a jelenlegi felhasználót,
            // a biztonság kedvéért az összes igénylést megjelenítjük.
            // Egy fejlettebb rendszerben itt a tanár saját ID-jára szűrnénk.
            _myRequestsListBox.ItemsSource = allRequests
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => $"{r.CreatedAt:yyyy-MM-dd} - {r.Data["EquipmentName"]} ({r.Data["Quantity"]} db) - Állapot: {r.Data["Status"]}")
                .ToList();
        }
        catch (Exception ex)
        {
             if (_statusTextBlock != null) _statusTextBlock.Text = $"Hiba a korábbi igénylések betöltésekor: {ex.Message}";
        }
    }

    private void SubmitRequest_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _statusTextBlock == null || _equipmentComboBox == null || _quantityTextBox == null || _reasonTextBox == null)
        {
            return;
        }

        if (_equipmentComboBox.SelectedItem == null)
        {
            _statusTextBlock.Text = "Hiba: Válasszon eszközt!";
            return;
        }

        if (!int.TryParse(_quantityTextBox.Text, out int quantity) || quantity <= 0)
        {
            _statusTextBlock.Text = "Hiba: A mennyiség csak pozitív egész szám lehet!";
            return;
        }

        if (string.IsNullOrWhiteSpace(_reasonTextBox.Text))
        {
            _statusTextBlock.Text = "Hiba: Az indoklás megadása kötelező!";
            return;
        }

        var data = new Dictionary<string, string>
        {
            { "EquipmentName", _equipmentComboBox.SelectedItem.ToString() ?? "Ismeretlen" },
            { "Quantity", quantity.ToString() },
            { "Reason", _reasonTextBox.Text },
            { "Status", "Requested" },
            { "RequestDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) }
        };

        try
        {
            _context.SaveEntity(EntityType, data);
            _statusTextBlock.Text = "Igénylés sikeresen elküldve!";

            _quantityTextBox.Text = "";
            _reasonTextBox.Text = "";
            _equipmentComboBox.SelectedIndex = 0;

            LoadMyRequests();
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba történt a mentés során: {ex.Message}";
        }
    }
}
