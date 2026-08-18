using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class KonyvtarKeresoView : IEvolView
{
    private IStudentContext? _context;
    private TextBox? _keresoBox;
    private ListBox? _eredmenyListBox;

    public new string Name => "Könyvtári Kereső";
    public string Description => "Egy egyszerű felület az iskolai könyvtár könyveinek kereséséhez.";

    public KonyvtarKeresoView() { }

    public KonyvtarKeresoView(IStudentContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Avalonia.Thickness(15)
        };

        var searchPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        _keresoBox = new TextBox
        {
            PlaceholderText = "Könyv címe vagy szerzője...",
            Width = 300
        };

        var searchButton = new Button
        {
            Content = "Keresés"
        };
        searchButton.Click += KeresesButton_Click;

        searchPanel.Children.Add(_keresoBox);
        searchPanel.Children.Add(searchButton);

        _eredmenyListBox = new ListBox
        {
            Height = 400,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };

        mainPanel.Children.Add(new TextBlock { Text = "Könyvkeresés", FontSize = 20, FontWeight = FontWeight.Bold });
        mainPanel.Children.Add(searchPanel);
        mainPanel.Children.Add(_eredmenyListBox);

        // Load all books initially
        LoadBooks("");

        return mainPanel;
    }

    private void KeresesButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var searchTerm = _keresoBox?.Text ?? string.Empty;
        LoadBooks(searchTerm);
    }

    private void LoadBooks(string searchTerm)
    {
        if (_context == null || _eredmenyListBox == null)
        {
            return;
        }

        var allBooks = _context.QueryEntities("LibraryBook");

        var filteredBooks = allBooks.Where(book =>
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return true;

            var title = book.Data.GetValueOrDefault("Title", string.Empty);
            var author = book.Data.GetValueOrDefault("Author", string.Empty);

            return title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || 
                   author.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        if (!filteredBooks.Any())
        {
            _eredmenyListBox.ItemsSource = new List<string> { "Nincs a keresésnek megfelelő találat." };
        }
        else
        {
            var displayItems = filteredBooks.Select(book =>
            {
                var title = book.Data.GetValueOrDefault("Title", "Ismeretlen cím");
                var author = book.Data.GetValueOrDefault("Author", "Ismeretlen szerző");
                var available = book.Data.GetValueOrDefault("Available", "nem");
                return $"{title} - {author} (Elérhető: {available})";
            }).ToList();
            _eredmenyListBox.ItemsSource = displayItems;
        }
    }
}