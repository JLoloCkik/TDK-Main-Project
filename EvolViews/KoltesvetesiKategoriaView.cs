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

public class KoltesvetesiKategoriaView : IEvolView
{
    private IDirectorContext? _context;
    private ListBox? _categoryListBox;
    private TextBox? _nameTextBox;
    private TextBox? _codeTextBox;
    private TextBox? _descriptionTextBox;
    private TextBlock? _statusTextBlock;
    private Button? _saveButton;
    private Button? _deleteButton;
    private Button? _newButton;
    private StackPanel? _editPanel;

    private List<GenericRecord>? _categories;
    private GenericRecord? _selectedCategory;
    private const string EntityType = "BudgetCategory";

    public new string Name => "Költségvetési Kategóriák";
    public string Description => "Iskolai költségvetési kategóriák létrehozása, szerkesztése és törlése.";

    public KoltesvetesiKategoriaView() { }

    public KoltesvetesiKategoriaView(IDirectorContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,3*"),
            Margin = new Thickness(10)
        };

        // Left Panel - List of categories
        var leftPanel = new StackPanel { Spacing = 10 };
        leftPanel.Children.Add(new TextBlock { Text = "Kategóriák", FontSize = 16, FontWeight = FontWeight.Bold });

        _categoryListBox = new ListBox { Height = 400 };
        _categoryListBox.SelectionChanged += CategorySelectionChanged;
        leftPanel.Children.Add(_categoryListBox);

        var buttonPanelLeft = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        _newButton = new Button { Content = "Új kategória" };
        _newButton.Click += NewButton_Click;
        buttonPanelLeft.Children.Add(_newButton);

        leftPanel.Children.Add(buttonPanelLeft);

        Grid.SetColumn(leftPanel, 0);
        mainGrid.Children.Add(leftPanel);

        // Right Panel - Editor
        _editPanel = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(20, 0, 0, 0),
            IsEnabled = false
        };

        _editPanel.Children.Add(new TextBlock { Text = "Kategória adatai", FontSize = 16, FontWeight = FontWeight.Bold });

        _editPanel.Children.Add(new TextBlock { Text = "Megnevezés:" });
        _nameTextBox = new TextBox();
        _editPanel.Children.Add(_nameTextBox);

        _editPanel.Children.Add(new TextBlock { Text = "Kód:" });
        _codeTextBox = new TextBox();
        _editPanel.Children.Add(_codeTextBox);

        _editPanel.Children.Add(new TextBlock { Text = "Leírás:" });
        _descriptionTextBox = new TextBox { Height = 100, TextWrapping = TextWrapping.Wrap };
        _editPanel.Children.Add(_descriptionTextBox);

        var buttonPanelRight = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        _saveButton = new Button { Content = "Mentés" };
        _saveButton.Click += SaveButton_Click;
        buttonPanelRight.Children.Add(_saveButton);

        _deleteButton = new Button { Content = "Törlés" };
        _deleteButton.Click += DeleteButton_Click;
        buttonPanelRight.Children.Add(_deleteButton);
        _editPanel.Children.Add(buttonPanelRight);

        _statusTextBlock = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };
        _editPanel.Children.Add(_statusTextBlock);

        Grid.SetColumn(_editPanel, 1);
        mainGrid.Children.Add(_editPanel);

        LoadCategories();

        return mainGrid;
    }

    private void LoadCategories()
    {
        if (_context == null || _categoryListBox == null) return;
        try
        {
            _categories = _context.QueryEntities(EntityType).OrderBy(c => c.Data.GetValueOrDefault("Name", string.Empty)).ToList();
            _categoryListBox.ItemsSource = _categories.Select(c => c.Data.GetValueOrDefault("Name", "N/A")).ToList();
        }
        catch (Exception ex)
        {
            SetStatus("Hiba a kategóriák betöltése közben: " + ex.Message, true);
        }
    }

    private void CategorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_categoryListBox?.SelectedItem == null)
        {
            ClearForm();
            if (_editPanel != null) _editPanel.IsEnabled = false;
            return;
        }

        _selectedCategory = _categories?[_categoryListBox.SelectedIndex];

        if (_selectedCategory != null && _nameTextBox != null && _codeTextBox != null && _descriptionTextBox != null && _editPanel != null)
        {
            _nameTextBox.Text = _selectedCategory.Data.GetValueOrDefault("Name");
            _codeTextBox.Text = _selectedCategory.Data.GetValueOrDefault("Code");
            _descriptionTextBox.Text = _selectedCategory.Data.GetValueOrDefault("Description");
            _editPanel.IsEnabled = true;
            SetStatus("Kiválasztva: " + _nameTextBox.Text, false);
        }
    }

    private void NewButton_Click(object? sender, RoutedEventArgs e)
    {
        ClearForm();
        _selectedCategory = null;
        if (_editPanel != null) _editPanel.IsEnabled = true;
        if (_nameTextBox != null) _nameTextBox.Focus();
        SetStatus("Új kategória létrehozása.", false);
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _nameTextBox == null || string.IsNullOrWhiteSpace(_nameTextBox.Text) || _codeTextBox == null || string.IsNullOrWhiteSpace(_codeTextBox.Text))
        {
            SetStatus("A megnevezés és a kód megadása kötelező!", true);
            return;
        }

        var data = new Dictionary<string, string>
        {
            { "Name", _nameTextBox.Text },
            { "Code", _codeTextBox.Text },
            { "Description", _descriptionTextBox?.Text ?? string.Empty }
        };

        try
        {
            int id = _context.SaveEntity(EntityType, data, _selectedCategory?.Id);
            SetStatus($"Kategória sikeresen mentve (ID: {id}).", false);
            LoadCategories();
            ClearForm();
            if (_editPanel != null) _editPanel.IsEnabled = false;
        }
        catch (Exception ex)
        {
            SetStatus("Hiba mentés közben: " + ex.Message, true);
        }
    }

    private void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _selectedCategory == null)
        {
            SetStatus("Nincs kategória kiválasztva a törléshez.", true);
            return;
        }

        try
        {
            _context.DeleteEntity(EntityType, _selectedCategory.Id);
            SetStatus("Kategória sikeresen törölve.", false);
            LoadCategories();
            ClearForm();
             if (_editPanel != null) _editPanel.IsEnabled = false;
        }
        catch (Exception ex)
        {
            SetStatus("Hiba a törlés közben: " + ex.Message, true);
        }
    }

    private void ClearForm()
    {
        if (_categoryListBox != null) _categoryListBox.SelectedItem = null;
        if (_nameTextBox != null) _nameTextBox.Text = string.Empty;
        if (_codeTextBox != null) _codeTextBox.Text = string.Empty;
        if (_descriptionTextBox != null) _descriptionTextBox.Text = string.Empty;
        _selectedCategory = null;
    }

    private void SetStatus(string message, bool isError)
    {
        if (_statusTextBlock == null) return;
        _statusTextBlock.Text = message;
        _statusTextBlock.Foreground = isError ? Brushes.Red : Brushes.Green;
    }
}