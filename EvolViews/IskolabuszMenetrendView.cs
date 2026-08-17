using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class IskolabuszMenetrendView : IEvolView
{
    private IStudentContext? _context;
    private ListBox? _scheduleListBox;
    private TextBlock? _statusTextBlock;
    private const string EntityType = "SchoolBusSchedule";

    public new string Name => "Iskolabusz Menetrend";
    public string Description => "Az iskolabuszok járatainak és megállóinak megtekintése.";

    public IskolabuszMenetrendView() { }

    public IskolabuszMenetrendView(IStudentContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        _scheduleListBox = new ListBox
        {
            Margin = new Avalonia.Thickness(10)
        };

        _statusTextBlock = new TextBlock
        {
            Margin = new Avalonia.Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = "Iskolabusz Menetrend",
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Avalonia.Thickness(0, 10)
                },
                _statusTextBlock,
                _scheduleListBox
            }
        };

        LoadSchedule();

        return mainPanel;
    }

    private void LoadSchedule()
    {
        if (_context == null || _scheduleListBox == null || _statusTextBlock == null)
        {
            if (_statusTextBlock != null)
                _statusTextBlock.Text = "A nézet nincs megfelelően beállítva.";
            return;
        }

        try
        {
            _statusTextBlock.Text = "Menetrend betöltése...";
            var scheduleRecords = _context.QueryEntities(EntityType);

            if (scheduleRecords == null || !scheduleRecords.Any())
            {
                _statusTextBlock.Text = "Nincs elérhető menetrend.";
                _scheduleListBox.IsVisible = false;
                return;
            }

            var displayItems = scheduleRecords
                .OrderBy(r => r.Data.GetValueOrDefault("Route", string.Empty))
                .ThenBy(r => r.Data.GetValueOrDefault("Time", string.Empty))
                .Select(record =>
                {
                    string route = record.Data.GetValueOrDefault("Route", "Ismeretlen járat");
                    string time = record.Data.GetValueOrDefault("Time", "--:--");
                    string stop = record.Data.GetValueOrDefault("Stop", "Ismeretlen megálló");
                    return $"[{time}] {route} járat - {stop}";
                })
                .ToList();

            _scheduleListBox.ItemsSource = displayItems;
            _statusTextBlock.Text = string.Empty;
            _statusTextBlock.IsVisible = false;
            _scheduleListBox.IsVisible = true;
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba a menetrend lekérése közben: {ex.Message}";
        }
    }
}
