using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class FaliujsagView : IEvolView
{
    private ITeacherContext? _context;
    private ListBox? _noticesListBox;
    private TextBox? _titleTextBox;
    private TextBox? _contentTextBox;
    private DatePicker? _expirationDatePicker;
    private TextBlock? _statusTextBlock;
    private Button? _saveButton;
    private Button? _deleteButton;
    private Button? _newButton;
    private StackPanel? _editPanel;

    private List<GenericRecord>? _activeNotices;
    private GenericRecord? _selectedNotice;
    private const string EntityType = "NoticeMessage";

    public new string Name => "Faliújság Kezelése";
    public string Description => "Hirdetmények létrehozása, szerkesztése és törlése a faliújságon, lejárati dátummal.";

    public FaliujsagView() { }

    public FaliujsagView(ITeacherContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*"),
            Margin = new Thickness(10)
        };

        // Left side: List of notices
        _noticesListBox = new ListBox
        {
            Margin = new Thickness(0, 0, 10, 0)
        };
        _noticesListBox.SelectionChanged += NoticesListBox_SelectionChanged;
        mainGrid.Children.Add(_noticesListBox);
        Grid.SetColumn(_noticesListBox, 0);

        // Right side: Edit panel
        _editPanel = new StackPanel { Spacing = 10, IsVisible = false };
        Grid.SetColumn(_editPanel, 1);

        _editPanel.Children.Add(new TextBlock { Text = "Cím:", FontWeight = FontWeight.Bold });
        _titleTextBox = new TextBox();
        _editPanel.Children.Add(_titleTextBox);

        _editPanel.Children.Add(new TextBlock { Text = "Tartalom:", FontWeight = FontWeight.Bold });
        _contentTextBox = new TextBox { Height = 150, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        _editPanel.Children.Add(_contentTextBox);

        _editPanel.Children.Add(new TextBlock { Text = "Lejárati dátum:", FontWeight = FontWeight.Bold });
        _expirationDatePicker = new DatePicker();
        _editPanel.Children.Add(_expirationDatePicker);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        _saveButton = new Button { Content = "Mentés" };
        _saveButton.Click += SaveButton_Click;
        buttonPanel.Children.Add(_saveButton);

        _deleteButton = new Button { Content = "Törlés" };
        _deleteButton.Click += DeleteButton_Click;
        buttonPanel.Children.Add(_deleteButton);
        _editPanel.Children.Add(buttonPanel);

        mainGrid.Children.Add(_editPanel);

        var topButtonPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        _newButton = new Button { Content = "Új hirdetmény" };
        _newButton.Click += NewButton_Click;
        topButtonPanel.Children.Add(_newButton);

        _statusTextBlock = new TextBlock { Margin = new Thickness(0, 10, 0, 0), FontWeight = FontWeight.Bold };

        var mainPanel = new StackPanel();
        mainPanel.Children.Add(topButtonPanel);
        mainPanel.Children.Add(mainGrid);
        mainPanel.Children.Add(_statusTextBlock);

        LoadNotices();
        return mainPanel;
    }

    private void LoadNotices()
    {
        if (_context == null || _noticesListBox == null) return;
        try
        {
            var allNotices = _context.QueryEntities(EntityType);
            _activeNotices = allNotices.Where(n => {
                if (n.Data.TryGetValue("ExpirationDate", out var dateString) && 
                    DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expirationDate))
                {
                    return expirationDate.Date >= DateTime.Now.Date;
                }
                return true; // If no date set, it never expires
            }).OrderByDescending(n => n.CreatedAt).ToList();

            _noticesListBox.ItemsSource = _activeNotices.Select(n => n.Data.GetValueOrDefault("Title", "Nincs cím")).ToList();
            ClearForm();
            SetStatus("Hirdetmények betöltve.", true);
        }
        catch (Exception ex)
        {
            SetStatus($"Hiba a betöltés során: {ex.Message}", false);
        }
    }

    private void NoticesListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_noticesListBox?.SelectedItem == null || _activeNotices == null) return;

        _selectedNotice = _activeNotices[_noticesListBox.SelectedIndex];
        DisplaySelectedNoticeDetails();
    }

    private void DisplaySelectedNoticeDetails()
    {
        if (_editPanel == null || _selectedNotice == null) return;

        _editPanel.IsVisible = true;
        if (_titleTextBox != null) _titleTextBox.Text = _selectedNotice.Data.GetValueOrDefault("Title", "");
        if (_contentTextBox != null) _contentTextBox.Text = _selectedNotice.Data.GetValueOrDefault("Content", "");
        if (_expirationDatePicker != null)
        {
            if (_selectedNotice.Data.TryGetValue("ExpirationDate", out var dateString) && 
                DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expirationDate))
            {
                _expirationDatePicker.SelectedDate = expirationDate;
            }
            else
            {
                _expirationDatePicker.SelectedDate = null;
            }
        }
    }

    private void ClearForm()
    {
        _selectedNotice = null;
        if (_noticesListBox != null) _noticesListBox.SelectedIndex = -1;
        if (_titleTextBox != null) _titleTextBox.Text = "";
        if (_contentTextBox != null) _contentTextBox.Text = "";
        if (_expirationDatePicker != null) _expirationDatePicker.SelectedDate = null;
        if (_editPanel != null) _editPanel.IsVisible = false;
    }

    private void NewButton_Click(object? sender, RoutedEventArgs e)
    {
        ClearForm();
        if (_editPanel != null) _editPanel.IsVisible = true;
        SetStatus("Új hirdetmény létrehozása.", true);
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _titleTextBox == null || _contentTextBox == null || string.IsNullOrWhiteSpace(_titleTextBox.Text))
        {
            SetStatus("Hiba: A cím mező nem lehet üres.", false);
            return;
        }

        var data = new Dictionary<string, string>
        {
            { "Title", _titleTextBox.Text },
            { "Content", _contentTextBox.Text ?? string.Empty }
        };

        if (_expirationDatePicker?.SelectedDate != null)
        {
            data.Add("ExpirationDate", _expirationDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        
        try
        {
            var idToSave = _selectedNotice?.Id;
            _context.SaveEntity(EntityType, data, idToSave);
            SetStatus("Hirdetmény sikeresen mentve.", true);
            LoadNotices();
        }
        catch (Exception ex)
        {
            SetStatus($"Hiba mentés közben: {ex.Message}", false);
        }
    }

    private void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_context == null || _selectedNotice == null)
        {
            SetStatus("Nincs kijelölt hirdetmény a törléshez.", false);
            return;
        }

        try
        {
            _context.DeleteEntity(EntityType, _selectedNotice.Id);
            SetStatus("Hirdetmény sikeresen törölve.", true);
            LoadNotices();
        }
        catch (Exception ex)
        {
            SetStatus($"Hiba törlés közben: {ex.Message}", false);
        }
    }

    private void SetStatus(string message, bool success)
    {
        if (_statusTextBlock == null) return;
        _statusTextBlock.Text = message;
        _statusTextBlock.Foreground = success ? Brushes.Green : Brushes.Red;
    }
}
