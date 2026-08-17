using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class TalaltTargyakView : IEvolView
{
    private IStudentContext? _context;
    private ListBox? _itemsListBox;
    private TextBlock? _statusTextBlock;
    private const string EntityType = "LostAndFoundItem";

    public new string Name => "Elveszett és Megtalált Tárgyak";
    public string Description => "Az iskolában elveszett és megtalált tárgyak listája.";

    public TalaltTargyakView() { }

    public TalaltTargyakView(IStudentContext context)
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

        var title = new TextBlock
        {
            Text = Name,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _statusTextBlock = new TextBlock
        {
            Text = "Adatok betöltése...",
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brushes.Gray
        };

        _itemsListBox = new ListBox
        {
            Height = 400,
            SelectionMode = SelectionMode.Single
        };

        mainPanel.Children.Add(title);
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Avalonia.Thickness(1), Child = _itemsListBox });

        LoadItems();

        return mainPanel;
    }

    private void LoadItems()
    {
        if (_context == null || _itemsListBox == null || _statusTextBlock == null)
        {
            if (_statusTextBlock != null)
                _statusTextBlock.Text = "Hiba: A nézet nincs megfelelően inicializálva.";
            return;
        }

        try
        {
            var items = _context.QueryEntities(EntityType);

            if (items == null || !items.Any())
            {
                _statusTextBlock.Text = "Nincsenek elveszett vagy megtalált tárgyak a listán.";
                _itemsListBox.ItemsSource = null;
                return;
            }

            var displayItems = items.OrderByDescending(i => i.CreatedAt).Select(item =>
            {
                string itemName = item.Data.GetValueOrDefault("ItemName", "Nincs megadva");
                string description = item.Data.GetValueOrDefault("Description", "Nincs leírás");
                string foundDate = item.Data.GetValueOrDefault("FoundDate", "Ismeretlen dátum");
                string location = item.Data.GetValueOrDefault("Location", "Ismeretlen helyszín");
                string status = item.Data.GetValueOrDefault("Status", "Elérhető");

                return $"Tárgy: {itemName} ({status})\nLeírás: {description}\nMegtalálás helye/ideje: {location}, {foundDate}";
            }).ToList();

            _itemsListBox.ItemsSource = displayItems;
            _statusTextBlock.Text = $"{items.Count} tárgy a listában.";
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba történt az adatok betöltése közben: {ex.Message}";
        }
    }
}
