using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using Kreta.Core;
using Kreta.Contexts;

public class HirdetmenyKezeloView : IEvolView
{
    private IDirectorContext? _context;
    private ListBox? _noticesListBox;
    private TextBox? _titleTextBox;
    private TextBox? _contentTextBox;
    private ComboBox? _priorityComboBox;
    private CheckBox? _isActiveCheckBox;
    private TextBlock? _statusTextBlock;
    private Button? _saveButton;
    private Button? _deleteButton;
    private Button? _newButton;
    private StackPanel? _editPanel;

    private List<GenericRecord>? _notices;
    private GenericRecord? _selectedNotice;
    private const string EntityType = "IskolaiHirdetmeny";

    public new string Name => "Hirdetmény Kezelő";
    public string Description => "Iskolai hirdetmények létrehozása, szerkesztése és törlése, prioritással.";

    public HirdetmenyKezeloView() { }

    public HirdetmenyKezeloView(IDirectorContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*")
        };

        _noticesListBox = new ListBox
        {
            Margin = new Thickness(10)
        };
        _noticesListBox.SelectionChanged += NoticesListBox_SelectionChanged;
        Grid.SetColumn(_noticesListBox, 0);

        _editPanel = new StackPanel
        {
            Margin = new Thickness(10),
            Spacing = 5,
            IsVisible = false
        };
        Grid.SetColumn(_editPanel, 1);

        _newButton = new Button { Content = "Új hirdetmény" };
        _newButton.Click += NewButton_Click;

        _titleTextBox = new TextBox { PlaceholderText = "Cím" };
        _contentTextBox = new TextBox
        {
            PlaceholderText = "Tartalom",
            MinHeight = 150,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true
        };

        _priorityComboBox = new ComboBox
        {
            ItemsSource = new List<string> { "Normál", "Sürgős" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _isActiveCheckBox = new CheckBox { Content = "Aktív" };
        _statusTextBlock = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };

        _saveButton = new Button { Content = "Mentés" };
        _saveButton.Click += SaveButton_Click;

        _deleteButton = new Button { Content = "Törlés" };
        _deleteButton.Click += DeleteButton_Click;

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        buttonPanel.Children.Add(_saveButton);
        buttonPanel.Children.Add(_deleteButton);

        var listPanel = new StackPanel { Spacing = 10, Margin = new Thickness(10) };
        listPanel.Children.Add(_newButton);
        listPanel.Children.Add(_noticesListBox);

        _editPanel.Children.Add(new TextBlock { Text = "Cím:", FontWeight = FontWeight.Bold });
        _editPanel.Children.Add(_titleTextBox);
        _editPanel.Children.Add(new TextBlock { Text = "Tartalom:", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 10, 0, 0) });
        _editPanel.Children.Add(_contentTextBox);
        _editPanel.Children.Add(new TextBlock { Text = "Prioritás:", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 10, 0, 0) });
        _editPanel.Children.Add(_priorityComboBox);
        _editPanel.Children.Add(_isActiveCheckBox);
        _editPanel.Children.Add(buttonPanel);
        _editPanel.Children.Add(_statusTextBlock);

        grid.Children.Add(listPanel);
        grid.Children.Add(_editPanel);

        LoadNotices();
        return grid;
    }

    private void LoadNotices()
    {
        if (_context == null || _noticesListBox == null) return;
        _notices = _context.QueryEntities(EntityType)?.OrderByDescending(n => n.Data.GetValueOrDefault("Priority", "Normál") == "Sürgős").ThenByDescending(n => n.Id).ToList();
        if (_notices != null)
        {
            _noticesListBox.ItemsSource = _notices.Select(n => 
            {
                string title = n.Data.GetValueOrDefault("Title", "Nincs cím");
                string priority = n.Data.GetValueOrDefault("Priority", "Normál");
                return $"[{priority}] {title}";
            }).ToList();
        }
    }

    private void NoticesListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_noticesListBox == null || _editPanel == null || _titleTextBox == null || _contentTextBox == null || _isActiveCheckBox == null || _priorityComboBox == null)
            return;

        if (_noticesListBox.SelectedIndex != -1 && _notices != null)
        {
            _selectedNotice = _notices[_noticesListBox.SelectedIndex];
            _editPanel.IsVisible = true;
            _titleTextBox.Text = _selectedNotice.Data.GetValueOrDefault("Title", "");
            _contentTextBox.Text = _selectedNotice.Data.GetValueOrDefault("Content", "");
            _isActiveCheckBox.IsChecked = bool.TryParse(_selectedNotice.Data.GetValueOrDefault("IsActive", "true"), out var isActive) && isActive;
            _priorityComboBox.SelectedItem = _selectedNotice.Data.GetValueOrDefault("Priority", "Normál");
            _statusTextBlock!.Text = string.Empty;
        }
    }

    private void NewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_editPanel == null || _titleTextBox == null || _contentTextBox == null || _isActiveCheckBox == null || _priorityComboBox == null || _statusTextBlock == null || _noticesListBox == null) return;
        _selectedNotice = null;
        _noticesListBox.SelectedItem = null;
        _editPanel.IsVisible = true;
        _titleTextBox.Text = string.Empty;
        _contentTextBox.Text = string.Empty;
        _isActiveCheckBox.IsChecked = true;
        _priorityComboBox.SelectedIndex = 0;
        _statusTextBlock.Text = "Új hirdetmény létrehozása.";
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || string.IsNullOrWhiteSpace(_titleTextBox?.Text) || string.IsNullOrWhiteSpace(_contentTextBox?.Text) || _statusTextBlock == null)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Hiba: A cím és a tartalom megadása kötelező.";
            return;
        }

        var data = new Dictionary<string, string>
        {
            { "Title", _titleTextBox.Text },
            { "Content", _contentTextBox.Text },
            { "IsActive", _isActiveCheckBox?.IsChecked.ToString() ?? "true" },
            { "Priority", _priorityComboBox?.SelectedItem as string ?? "Normál" }
        };

        var idToSave = _selectedNotice?.Id;
        var savedId = _context.SaveEntity(EntityType, data, idToSave);

        _statusTextBlock.Text = idToSave.HasValue ? "Hirdetmény sikeresen frissítve." : "Hirdetmény sikeresen létrehozva.";
        LoadNotices();
    }

    private void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _selectedNotice == null || _statusTextBlock == null || _editPanel == null)
            return;

        _context.DeleteEntity(EntityType, _selectedNotice.Id);
        _selectedNotice = null;
        _editPanel.IsVisible = false;
        _statusTextBlock.Text = "Hirdetmény törölve.";
        LoadNotices();
    }
}