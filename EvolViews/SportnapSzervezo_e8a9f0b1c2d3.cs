using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

namespace Kreta.Dynamic;

public class SportnapSzervezo_e8a9f0b1c2d3 : UserControl, IEvolView
{
    public string Name => "Sportnap Szervező";
    public string Description => "Új felhasználók létrehozása és osztályhoz rendelésük.";

    private readonly IDirectorContext? _context;

    private ListBox _userListBox;
    private ComboBox _classComboBox;
    private TextBlock _selectedUserText;
    private TextBox _newUserNameBox;
    private ComboBox _newUserRoleBox;

    public SportnapSzervezo_e8a9f0b1c2d3()
    {
    }

    public SportnapSzervezo_e8a9f0b1c2d3(IDirectorContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, 2*"),
            Margin = new Thickness(15)
        };

        // --- Left Panel: User List ---
        var userListPanel = new StackPanel { Spacing = 10 };
        userListPanel.Children.Add(new TextBlock { Text = "Regisztrált Felhasználók", FontSize = 18, FontWeight = FontWeight.Bold });

        _userListBox = new ListBox { Height = 400 };
        _userListBox.SelectionChanged += OnUserSelectionChanged;
        userListPanel.Children.Add(_userListBox);

        Grid.SetColumn(userListPanel, 0);
        mainGrid.Children.Add(userListPanel);

        // --- Right Panel: Actions ---
        var actionsPanel = new StackPanel { Spacing = 20 };
        Grid.SetColumn(actionsPanel, 1);

        // --- Action 1: Create New User ---
        var createUserBorder = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(15)
        };
        var createUserPanel = new StackPanel { Spacing = 10 };
        createUserPanel.Children.Add(new TextBlock { Text = "Új Felhasználó Létrehozása", FontSize = 16, FontWeight = FontWeight.SemiBold });
        _newUserNameBox = new TextBox { PlaceholderText = "Felhasználó neve" };
        _newUserRoleBox = new ComboBox { PlaceholderText = "Válassz szerepkört", ItemsSource = Enum.GetValues(typeof(Role)) };
        var createUserButton = new Button { Content = "Felhasználó Hozzáadása" };
        createUserButton.Click += CreateUser_Click;

        createUserPanel.Children.Add(_newUserNameBox);
        createUserPanel.Children.Add(_newUserRoleBox);
        createUserPanel.Children.Add(createUserButton);
        createUserBorder.Child = createUserPanel;
        actionsPanel.Children.Add(createUserBorder);

        // --- Action 2: Assign Class ---
        var assignClassBorder = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(15)
        };

        var assignClassPanel = new StackPanel { Spacing = 10 };
        assignClassPanel.Children.Add(new TextBlock { Text = "Tanuló Osztályhoz Rendelése", FontSize = 16, FontWeight = FontWeight.SemiBold });
        _selectedUserText = new TextBlock { Text = "Nincs tanuló kiválasztva", FontStyle = FontStyle.Italic };
        _classComboBox = new ComboBox { PlaceholderText = "Válassz osztályt" };
        var assignButton = new Button { Content = "Hozzárendelés" };
        assignButton.Click += AssignClass_Click;

        assignClassPanel.Children.Add(_selectedUserText);
        assignClassPanel.Children.Add(_classComboBox);
        assignClassPanel.Children.Add(assignButton);
        assignClassBorder.Child = assignClassPanel;
        actionsPanel.Children.Add(assignClassBorder);

        mainGrid.Children.Add(actionsPanel);

        LoadInitialData();

        return mainGrid;
    }

    private void LoadInitialData()
    {
        RefreshUserList();
        if (_context != null)
        {
            _classComboBox.ItemsSource = _context.GetAllClasses();
        }
    }

    private void RefreshUserList()
    {
        if (_context == null) return;
        _userListBox.ItemsSource = _context.GetAllUsers()
                                           .OrderBy(u => u.Name)
                                           .Select(u => new { Display = $"{u.Name} ({u.Role}) - {u.ClassName ?? "N/A"}", UserObject = u })
                                           .ToList();
        _userListBox.DisplayMemberBinding = new Avalonia.Data.Binding("Display");
    }

    private void OnUserSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_userListBox.SelectedItem is { } selectedItem)
        {
            var userProperty = selectedItem.GetType().GetProperty("UserObject");
            if (userProperty?.GetValue(selectedItem) is User selectedUser && selectedUser.Role == Role.Student)
            {
                 _selectedUserText.Text = $"Kiválasztott tanuló: {selectedUser.Name}";
                 _selectedUserText.FontStyle = FontStyle.Normal;
                 return;
            }
        }
        _selectedUserText.Text = "Nincs tanuló kiválasztva";
        _selectedUserText.FontStyle = FontStyle.Italic;
    }

    private void CreateUser_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_context == null || string.IsNullOrWhiteSpace(_newUserNameBox.Text) || _newUserRoleBox.SelectedItem == null)
        {
            return;
        }

        var newUser = new User
        {
            Name = _newUserNameBox.Text,
            Role = (Role)_newUserRoleBox.SelectedItem
        };

        _context.CreateUser(newUser);
        _newUserNameBox.Text = "";
        _newUserRoleBox.SelectedIndex = -1;
        RefreshUserList();
    }

    private void AssignClass_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_context == null || _userListBox.SelectedItem == null || _classComboBox.SelectedItem == null)
        {
            return;
        }

        var selectedItem = _userListBox.SelectedItem;
        var userProperty = selectedItem.GetType().GetProperty("UserObject");
        if (userProperty?.GetValue(selectedItem) is User selectedUser && selectedUser.Role == Role.Student)
        {
             _context.AssignClassToStudent(selectedUser.Id, _classComboBox.SelectedItem.ToString());
             RefreshUserList();
        }
    }
}