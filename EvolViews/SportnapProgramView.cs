using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class SportnapProgramView : IEvolView
{
    private const string EntityType = "SportsDayEvent";
    private readonly IStudentContext? _context;
    private ListBox? _programListBox;
    private TextBlock? _statusTextBlock;

    public SportnapProgramView()
    {
    }

    public SportnapProgramView(IStudentContext context)
    {
        _context = context;
    }

    public new string Name => "Sportnap Program";
    public string Description => "A sportnapi események időbeosztásának és helyszíneinek megtekintése.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10, Margin = new Thickness(15) };
        var title = new TextBlock
        {
            Text = "Sportnapi programok", FontSize = 20, FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _statusTextBlock = new TextBlock
            { Text = "Programok betöltése...", HorizontalAlignment = HorizontalAlignment.Center };
        _programListBox = new ListBox { IsVisible = false, Margin = new Thickness(10) };
        mainPanel.Children.Add(title);
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(_programListBox);
        LoadEventData();
        return mainPanel;
    }

    private void LoadEventData()
    {
        if (_context == null || _programListBox == null || _statusTextBlock == null)
        {
            if (_statusTextBlock != null) _statusTextBlock.Text = "Hiba: A kontextus nincs beállítva.";
            return;
        }

        try
        {
            var events = _context.QueryEntities(EntityType);
            if (events == null || !events.Any())
            {
                _statusTextBlock.Text = "Nincsenek meghirdetett sportnapi programok.";
                _programListBox.IsVisible = false;
                return;
            }

            var formattedEvents = events.OrderBy(e => e.Data.ContainsKey("Time") ? e.Data["Time"] : "99:99").Select(e =>
            {
                var time = e.Data.ContainsKey("Time") ? e.Data["Time"] : "Ismeretlen időpont";
                var name = e.Data.ContainsKey("EventName") ? e.Data["EventName"] : "Névtelen esemény";
                var location = e.Data.ContainsKey("Location") ? e.Data["Location"] : "Ismeretlen helyszín";
                return $"{time}: {name} ({location})";
            }).ToList();
            _programListBox.ItemsSource = formattedEvents;
            _programListBox.IsVisible = true;
            _statusTextBlock.IsVisible = false;
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba történt a programok betöltése közben: {ex.Message}";
            _programListBox.IsVisible = false;
        }
    }
}