using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class SzakkorInfoView : IEvolView
{
    private const string EntityType = "AfterSchoolClub";
    private readonly IStudentContext? _context;
    private ListBox? _clubListBox;

    private List<GenericRecord>? _clubs;
    private Border? _detailsBorder;
    private TextBlock? _detailsDescriptionTextBlock;
    private TextBlock? _detailsLeaderTextBlock;
    private TextBlock? _detailsNameTextBlock;
    private TextBlock? _detailsScheduleTextBlock;
    private TextBlock? _statusTextBlock;

    public SzakkorInfoView()
    {
    }

    public SzakkorInfoView(IStudentContext context)
    {
        _context = context;
    }

    public new string Name => "Szakkör Információk";
    public string Description => "Választható délutáni szakkörök és információik megtekintése.";

    public Control CreateView()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("250,*"),
            Margin = new Thickness(10)
        };

        _statusTextBlock = new TextBlock { Text = "Szakkörök betöltése...", Margin = new Thickness(10) };

        _clubListBox = new ListBox
        {
            Margin = new Thickness(0, 0, 10, 0)
        };
        _clubListBox.SelectionChanged += ClubListBox_SelectionChanged;
        Grid.SetColumn(_clubListBox, 0);

        _detailsNameTextBlock = new TextBlock { FontSize = 18, FontWeight = FontWeight.Bold };
        _detailsLeaderTextBlock = new TextBlock { FontStyle = FontStyle.Italic, Margin = new Thickness(0, 5, 0, 10) };
        _detailsScheduleTextBlock = new TextBlock { Margin = new Thickness(0, 5, 0, 10) };
        _detailsDescriptionTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };

        var detailsPanel = new StackPanel
        {
            Spacing = 5,
            Children =
            {
                _detailsNameTextBlock,
                _detailsLeaderTextBlock,
                _detailsScheduleTextBlock,
                _detailsDescriptionTextBlock
            }
        };

        _detailsBorder = new Border
        {
            Padding = new Thickness(15),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            IsVisible = false,
            Child = detailsPanel
        };
        Grid.SetColumn(_detailsBorder, 1);

        var listPanel = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "Választható szakkörök", FontSize = 16, Margin = new Thickness(0, 0, 0, 10) },
                _clubListBox, _statusTextBlock
            }
        };

        mainGrid.Children.Add(listPanel);
        mainGrid.Children.Add(_detailsBorder);

        LoadClubs();

        return mainGrid;
    }

    private async void LoadClubs()
    {
        if (_context == null || _clubListBox == null || _statusTextBlock == null)
            return;

        try
        {
            _clubs = _context.QueryEntities(EntityType);

            if (_clubs != null && _clubs.Any())
            {
                _clubListBox.ItemsSource = _clubs
                    .Select(c => c.Data.TryGetValue("Name", out var name) ? name : "Névtelen szakkör")
                    .ToList();
                _statusTextBlock.Text = $"{_clubs.Count} szakkör található.";
            }
            else
            {
                _statusTextBlock.Text = "Jelenleg nincsenek meghirdetett szakkörök.";
            }
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba a szakkörök betöltése közben: {ex.Message}";
        }
    }

    private void ClubListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_detailsBorder == null || _clubs == null || _clubListBox == null || _clubListBox.SelectedIndex == -1)
        {
            if (_detailsBorder != null) _detailsBorder.IsVisible = false;
            return;
        }

        var selectedClub = _clubs[_clubListBox.SelectedIndex];

        if (_detailsNameTextBlock != null)
            _detailsNameTextBlock.Text = selectedClub.Data.TryGetValue("Name", out var name) ? name : "Nincs név";

        if (_detailsLeaderTextBlock != null)
            _detailsLeaderTextBlock.Text = selectedClub.Data.TryGetValue("Leader", out var leader)
                ? $"Vezető: {leader}"
                : "Vezető: Ismeretlen";

        if (_detailsScheduleTextBlock != null)
            _detailsScheduleTextBlock.Text = selectedClub.Data.TryGetValue("Schedule", out var schedule)
                ? $"Időpont: {schedule}"
                : "Időpont: Ismeretlen";

        if (_detailsDescriptionTextBlock != null)
            _detailsDescriptionTextBlock.Text =
                selectedClub.Data.TryGetValue("Description", out var desc) ? desc : "Nincs leírás.";

        _detailsBorder.IsVisible = true;
    }
}