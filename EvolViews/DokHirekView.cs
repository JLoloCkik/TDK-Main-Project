using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class DokHirekView : IEvolView
{
    private const string EntityType = "StudentCouncilNews";
    private readonly IStudentContext? _context;

    private List<GenericRecord>? _hirek;
    private ListBox? _hirekListBox;
    private Border? _reszletekBorder;
    private TextBlock? _reszletekCimTextBlock;
    private TextBlock? _reszletekDatumTextBlock;
    private TextBlock? _reszletekTartalomTextBlock;
    private TextBlock? _statusTextBlock;

    public DokHirekView()
    {
    }

    public DokHirekView(IStudentContext context)
    {
        _context = context;
    }

    public new string Name => "DÖK Hírek";
    public string Description => "A Diákönkormányzat legfrissebb híreinek és közleményeinek megtekintése.";

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
            Text = "DÖK Hírek",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _statusTextBlock = new TextBlock
            { Text = "Hírek betöltése...", HorizontalAlignment = HorizontalAlignment.Center };

        _hirekListBox = new ListBox
        {
            MaxHeight = 200
        };
        _hirekListBox.SelectionChanged += HirekListBox_SelectionChanged;

        _reszletekCimTextBlock = new TextBlock
            { FontSize = 16, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
        _reszletekDatumTextBlock = new TextBlock
            { FontStyle = FontStyle.Italic, Foreground = Brushes.Gray, Margin = new Thickness(0, 5, 0, 10) };
        _reszletekTartalomTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };

        _reszletekBorder = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 10, 0, 0),
            IsVisible = false,
            Child = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    _reszletekCimTextBlock,
                    _reszletekDatumTextBlock,
                    _reszletekTartalomTextBlock
                }
            }
        };

        mainPanel.Children.Add(title);
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(_hirekListBox);
        mainPanel.Children.Add(_reszletekBorder);

        LoadHirek();

        return mainPanel;
    }

    private void LoadHirek()
    {
        if (_context == null || _hirekListBox == null || _statusTextBlock == null)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Hiba: A kontextus nincs beállítva.";
            return;
        }

        try
        {
            _hirek = _context.QueryEntities(EntityType);

            if (_hirek != null && _hirek.Any())
            {
                _hirek = _hirek.OrderByDescending(h => h.CreatedAt).ToList();
                _hirekListBox.ItemsSource = _hirek.Select(h => h.Data.GetValueOrDefault("Title", "Nincs cím")).ToList();
                _statusTextBlock.IsVisible = false;
            }
            else
            {
                _statusTextBlock.Text = "Nincsenek aktuális DÖK hírek.";
            }
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba a hírek betöltése közben: {ex.Message}";
        }
    }

    private void HirekListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_hirekListBox == null || _reszletekBorder == null || _hirek == null ||
            _reszletekCimTextBlock == null || _reszletekDatumTextBlock == null || _reszletekTartalomTextBlock == null)
            return;

        var selectedIndex = _hirekListBox.SelectedIndex;

        if (selectedIndex >= 0 && selectedIndex < _hirek.Count)
        {
            var selectedHir = _hirek[selectedIndex];
            _reszletekCimTextBlock.Text = selectedHir.Data.GetValueOrDefault("Title", "Nincs cím");
            _reszletekDatumTextBlock.Text =
                selectedHir.CreatedAt.ToString("yyyy. MMMM dd. HH:mm", CultureInfo.CurrentCulture);
            _reszletekTartalomTextBlock.Text = selectedHir.Data.GetValueOrDefault("Content", "Nincs tartalom.");
            _reszletekBorder.IsVisible = true;
        }
        else
        {
            _reszletekBorder.IsVisible = false;
        }
    }
}