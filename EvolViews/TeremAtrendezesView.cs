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

public class TeremAtrendezesView : IEvolView
{
    private IDirectorContext? _context;

    private ListBox? _classroomListBox;
    private StackPanel? _editPanel;
    private TextBlock? _statusTextBlock;

    private TextBox? _nameTextBox;
    private TextBox? _capacityTextBox;
    private TextBox? _layoutTextBox;
    private TextBox? _equipmentTextBox;
    private ComboBox? _statusComboBox;
    private Button? _saveButton;
    private Button? _newButton;

    private List<GenericRecord>? _classrooms;
    private GenericRecord? _selectedClassroom;
    private const string EntityType = "ClassroomState";

    public new string Name => "Terem Állapot Kezelő";
    public string Description => "Tantermek állapotának, berendezésének és felszereltségének nyilvántartására és módosítására szolgáló felület.";

    public TeremAtrendezesView() { }

    public TeremAtrendezesView(IDirectorContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("250,*"),
            Margin = new Thickness(10)
        };

        // Left panel - List of classrooms
        _classroomListBox = new ListBox
        {
            Margin = new Thickness(0, 0, 10, 0)
        };
        _classroomListBox.SelectionChanged += ClassroomListBox_SelectionChanged;
        Grid.SetColumn(_classroomListBox, 0);

        // Right panel - Editor
        _editPanel = new StackPanel
        {
            Spacing = 10,
            IsEnabled = false
        };

        _statusTextBlock = new TextBlock { Text = "Válasszon egy termet a listából vagy hozzon létre újat.", FontWeight = FontWeight.Bold };

        _newButton = new Button { Content = "Új terem" };
        _newButton.Click += NewButton_Click;

        _nameTextBox = new TextBox { PlaceholderText = "Terem neve (pl. 101/A)" };
        _capacityTextBox = new TextBox { PlaceholderText = "Kapacitás (férőhely)" };
        _layoutTextBox = new TextBox { PlaceholderText = "Berendezés leírása (pl. Csoportos)" };
        _equipmentTextBox = new TextBox { PlaceholderText = "Felszereltség (vesszővel elválasztva)" };
        
        _statusComboBox = new ComboBox
        {
            PlaceholderText = "Állapot",
            ItemsSource = new List<string> { "Szabad", "Foglalt", "Karbantartás alatt" }
        };

        _saveButton = new Button { Content = "Mentés" };
        _saveButton.Click += SaveButton_Click;

        _editPanel.Children.Add(new TextBlock { Text = "Terem adatai", FontSize = 16, FontWeight = FontWeight.Bold });
        _editPanel.Children.Add(new Label { Content = "Terem neve:" });
        _editPanel.Children.Add(_nameTextBox);
        _editPanel.Children.Add(new Label { Content = "Kapacitás:" });
        _editPanel.Children.Add(_capacityTextBox);
        _editPanel.Children.Add(new Label { Content = "Berendezés:" });
        _editPanel.Children.Add(_layoutTextBox);
        _editPanel.Children.Add(new Label { Content = "Felszereltség:" });
        _editPanel.Children.Add(_equipmentTextBox);
        _editPanel.Children.Add(new Label { Content = "Állapot:" });
        _editPanel.Children.Add(_statusComboBox);
        _editPanel.Children.Add(_saveButton);

        var rightSidePanel = new StackPanel
        {
            Spacing = 15
        };
        rightSidePanel.Children.Add(_statusTextBlock);
        rightSidePanel.Children.Add(_newButton);
        rightSidePanel.Children.Add(_editPanel);
        Grid.SetColumn(rightSidePanel, 1);

        mainGrid.Children.Add(_classroomListBox);
        mainGrid.Children.Add(rightSidePanel);

        LoadClassrooms();

        return mainGrid;
    }

    private void LoadClassrooms()
    {
        if (_context == null || _classroomListBox == null) return;
        try
        {
            _classrooms = _context.QueryEntities(EntityType).OrderBy(r => r.Data.GetValueOrDefault("Name", "N/A")).ToList();
            _classroomListBox.ItemsSource = _classrooms.Select(r => r.Data.GetValueOrDefault("Name", "Ismeretlen terem")).ToList();
            ClearAndDisableEditPanel();
        }
        catch (Exception ex)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = $"Hiba a termek betöltése közben: {ex.Message}";
        }
    }

    private void ClassroomListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_classroomListBox?.SelectedItem == null)
        {
            _selectedClassroom = null;
            ClearAndDisableEditPanel();
            return;
        }

        _selectedClassroom = _classrooms?[_classroomListBox.SelectedIndex];
        PopulateEditPanel();
    }

    private void PopulateEditPanel()
    {
        if (_editPanel == null || _selectedClassroom == null) return;

        _editPanel.IsEnabled = true;
        if (_statusTextBlock != null) _statusTextBlock.Text = $"Szerkesztés: {_selectedClassroom.Data.GetValueOrDefault("Name", "")}";
        if (_nameTextBox != null) _nameTextBox.Text = _selectedClassroom.Data.GetValueOrDefault("Name", "");
        if (_capacityTextBox != null) _capacityTextBox.Text = _selectedClassroom.Data.GetValueOrDefault("Capacity", "");
        if (_layoutTextBox != null) _layoutTextBox.Text = _selectedClassroom.Data.GetValueOrDefault("Layout", "");
        if (_equipmentTextBox != null) _equipmentTextBox.Text = _selectedClassroom.Data.GetValueOrDefault("Equipment", "");
        if (_statusComboBox != null) _statusComboBox.SelectedItem = _selectedClassroom.Data.GetValueOrDefault("Status", null);
    }

    private void ClearAndDisableEditPanel()
    {
        if (_editPanel == null) return;
        _editPanel.IsEnabled = false;
        if (_statusTextBlock != null) _statusTextBlock.Text = "Válasszon egy termet a listából vagy hozzon létre újat.";
        if (_nameTextBox != null) _nameTextBox.Text = string.Empty;
        if (_capacityTextBox != null) _capacityTextBox.Text = string.Empty;
        if (_layoutTextBox != null) _layoutTextBox.Text = string.Empty;
        if (_equipmentTextBox != null) _equipmentTextBox.Text = string.Empty;
        if (_statusComboBox != null) _statusComboBox.SelectedIndex = -1;
        _selectedClassroom = null;
    }

    private void NewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_classroomListBox != null) _classroomListBox.SelectedIndex = -1;
        _selectedClassroom = null;
        if (_editPanel != null) _editPanel.IsEnabled = true;
        if (_statusTextBlock != null) _statusTextBlock.Text = "Új terem adatainak megadása.";
        if (_nameTextBox != null) _nameTextBox.Text = string.Empty;
        if (_capacityTextBox != null) _capacityTextBox.Text = string.Empty;
        if (_layoutTextBox != null) _layoutTextBox.Text = string.Empty;
        if (_equipmentTextBox != null) _equipmentTextBox.Text = string.Empty;
        if (_statusComboBox != null) _statusComboBox.SelectedIndex = -1;
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _statusTextBlock == null || _nameTextBox == null || string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Hiba: A terem neve kötelező.";
            return;
        }

        var data = new Dictionary<string, string>
        {
            { "Name", _nameTextBox.Text ?? string.Empty },
            { "Capacity", _capacityTextBox?.Text ?? string.Empty },
            { "Layout", _layoutTextBox?.Text ?? string.Empty },
            { "Equipment", _equipmentTextBox?.Text ?? string.Empty },
            { "Status", _statusComboBox?.SelectedItem as string ?? string.Empty }
        };

        try
        {
            int? idToSave = _selectedClassroom?.Id;
            _context.SaveEntity(EntityType, data, idToSave);
            _statusTextBlock.Text = "Mentés sikeres!";
            LoadClassrooms();
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba a mentés során: {ex.Message}";
        }
    }
}