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

public class TantargyKezeloView : IEvolView
{
    private const string EntityType = "Subject";
    private readonly IDirectorContext? _context;
    private Button? _deleteButton;
    private StackPanel? _editPanel;
    private TextBox? _nameTextBox;
    private Button? _newButton;
    private Button? _saveButton;
    private GenericRecord? _selectedSubject;
    private TextBlock? _statusTextBlock;
    private ListBox? _subjectListBox;

    private List<GenericRecord>? _subjects;

    public TantargyKezeloView()
    {
    }

    public TantargyKezeloView(IDirectorContext context)
    {
        _context = context;
    }

    public new string Name => "Tantárgykezelés";
    public string Description => "Iskolai tantárgyak létrehozása, szerkesztése és törlése.";

    public Control CreateView()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*"),
            Margin = new Thickness(10)
        };

        _subjectListBox = new ListBox
        {
            Margin = new Thickness(0, 0, 10, 0)
        };
        _subjectListBox.SelectionChanged += SubjectListBox_SelectionChanged;
        Grid.SetColumn(_subjectListBox, 0);

        _editPanel = new StackPanel
        {
            Spacing = 10,
            IsVisible = false
        };

        _nameTextBox = new TextBox { PlaceholderText = "Tantárgy neve" };
        var nameLabel = new TextBlock { Text = "Tantárgy neve:" };

        _saveButton = new Button { Content = "Mentés" };
        _saveButton.Click += SaveButton_Click;

        _deleteButton = new Button
        {
            Content = "Törlés",
            Background = Brushes.Red,
            Foreground = Brushes.White,
            IsVisible = false
        };
        _deleteButton.Click += DeleteButton_Click;

        _editPanel.Children.Add(nameLabel);
        _editPanel.Children.Add(_nameTextBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        buttonPanel.Children.Add(_saveButton);
        buttonPanel.Children.Add(_deleteButton);
        _editPanel.Children.Add(buttonPanel);

        _newButton = new Button
        {
            Content = "Új tantárgy létrehozása",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _newButton.Click += NewButton_Click;

        var rightPanel = new StackPanel();
        rightPanel.Children.Add(_newButton);
        rightPanel.Children.Add(_editPanel);
        Grid.SetColumn(rightPanel, 1);

        mainGrid.Children.Add(_subjectListBox);
        mainGrid.Children.Add(rightPanel);

        _statusTextBlock = new TextBlock
        {
            Margin = new Thickness(10)
        };

        var finalPanel = new StackPanel();
        finalPanel.Children.Add(mainGrid);
        finalPanel.Children.Add(_statusTextBlock);

        LoadSubjects();

        return finalPanel;
    }

    private void LoadSubjects()
    {
        if (_context == null || _subjectListBox == null) return;

        _subjects = _context.QueryEntities(EntityType).OrderBy(s => s.Data.ContainsKey("Name") ? s.Data["Name"] : "")
            .ToList();
        _subjectListBox.ItemsSource =
            _subjects.Select(s => s.Data.ContainsKey("Name") ? s.Data["Name"] : "N/A").ToList();

        ClearForm();
    }

    private void ClearForm()
    {
        _selectedSubject = null;
        if (_subjectListBox != null) _subjectListBox.SelectedItem = null;
        if (_nameTextBox != null) _nameTextBox.Text = string.Empty;
        if (_editPanel != null) _editPanel.IsVisible = false;
        if (_deleteButton != null) _deleteButton.IsVisible = false;
        if (_statusTextBlock != null) _statusTextBlock.Text = "";
    }

    private void NewButton_Click(object? sender, RoutedEventArgs e)
    {
        ClearForm();
        if (_editPanel != null) _editPanel.IsVisible = true;
        if (_statusTextBlock != null) _statusTextBlock.Text = "Új tantárgy adatainak megadása.";
    }

    private void SubjectListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_subjectListBox?.SelectedItem == null || _subjects == null) return;

        var selectedIndex = _subjectListBox.SelectedIndex;
        if (selectedIndex >= 0 && selectedIndex < _subjects.Count)
        {
            _selectedSubject = _subjects[selectedIndex];
            if (_nameTextBox != null)
                _nameTextBox.Text = _selectedSubject.Data.ContainsKey("Name") ? _selectedSubject.Data["Name"] : "";
            if (_editPanel != null) _editPanel.IsVisible = true;
            if (_deleteButton != null) _deleteButton.IsVisible = true;
            if (_statusTextBlock != null) _statusTextBlock.Text = "";
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _nameTextBox == null || string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Hiba: A tantárgy neve nem lehet üres!";
            return;
        }

        var data = new Dictionary<string, string> { { "Name", _nameTextBox.Text } };

        try
        {
            _context.SaveEntity(EntityType, data, _selectedSubject?.Id);
            if (_statusTextBlock != null) _statusTextBlock.Text = "Sikeres mentés!";
            LoadSubjects();
        }
        catch (Exception ex)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = $"Hiba történt a mentés során: {ex.Message}";
        }
    }

    private void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _selectedSubject == null)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Nincs kiválasztott tantárgy a törléshez.";
            return;
        }

        try
        {
            _context.DeleteEntity(EntityType, _selectedSubject.Id);
            if (_statusTextBlock != null) _statusTextBlock.Text = "Sikeres törlés!";
            LoadSubjects();
        }
        catch (Exception ex)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = $"Hiba történt a törlés során: {ex.Message}";
        }
    }
}