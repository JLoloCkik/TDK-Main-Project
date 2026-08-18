using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class KonyvtarListaView : IEvolView
{
    private const string EntityType = "LibraryBook";
    private readonly IStudentContext? _context;
    private ListBox? _bookListBox;
    private TextBlock? _statusTextBlock;

    public KonyvtarListaView()
    {
    }

    public KonyvtarListaView(IStudentContext context)
    {
        _context = context;
    }

    public new string Name => "Könyvtári Könyvek";
    public string Description => "Az iskolai könyvtárban elérhető könyvek listájának megtekintése.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(15)
        };

        var title = new TextBlock
        {
            Text = "Elérhető Könyvek a Könyvtárban",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _statusTextBlock = new TextBlock
        {
            Text = "Könyvek betöltése...",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0)
        };

        _bookListBox = new ListBox
        {
            IsVisible = false
        };

        mainPanel.Children.Add(title);
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(_bookListBox);

        LoadBooks();

        return mainPanel;
    }

    private void LoadBooks()
    {
        if (_context == null || _bookListBox == null || _statusTextBlock == null)
        {
            if (_statusTextBlock != null)
                _statusTextBlock.Text = "Hiba: A nézet nincs megfelelően inicializálva.";
            return;
        }

        var books = _context.QueryEntities(EntityType);

        if (books != null && books.Any())
        {
            var bookItems = books.Select(book =>
            {
                var title = book.Data.GetValueOrDefault("Title", "Ismeretlen cím");
                var author = book.Data.GetValueOrDefault("Author", "Ismeretlen szerző");
                var available = book.Data.GetValueOrDefault("AvailableCopies", "0");
                return $"{title} - {author} ({available} db elérhető)";
            }).ToList();

            _bookListBox.ItemsSource = bookItems;
            _bookListBox.IsVisible = true;
            _statusTextBlock.IsVisible = false;
        }
        else
        {
            _statusTextBlock.Text = "Jelenleg nincsenek könyvek regisztrálva a könyvtárban.";
            _bookListBox.IsVisible = false;
            _statusTextBlock.IsVisible = true;
        }
    }
}