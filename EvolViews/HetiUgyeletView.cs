using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class HetiUgyeletView : IEvolView
{
    private IStudentContext? _context;
    private ListBox? _dutyListBox;
    private TextBlock? _statusTextBlock;
    private const string EntityType = "StudentDutySchedule";

    public new string Name => "Heti Ügyeleti Beosztás";
    public string Description => "A heti diákügyeleti beosztás megtekintése.";

    public HetiUgyeletView() { }

    public HetiUgyeletView(IStudentContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Avalonia.Thickness(20)
        };

        var title = new TextBlock
        {
            Text = "Heti Ügyeleti Beosztás",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _statusTextBlock = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brushes.Red
        };

        _dutyListBox = new ListBox
        {
            Height = 400,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        mainPanel.Children.Add(title);
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(_dutyListBox);

        LoadDutySchedule();

        return mainPanel;
    }

    private void LoadDutySchedule()
    {
        if (_context == null || _dutyListBox == null || _statusTextBlock == null)
        {
            if (_statusTextBlock != null)
                _statusTextBlock.Text = "A nézet nincs megfelelően inicializálva.";
            return;
        }

        try
        {
            _statusTextBlock.Text = string.Empty;
            var allDuties = _context.QueryEntities(EntityType);

            if (allDuties == null || !allDuties.Any())
            {
                _statusTextBlock.Text = "Nincs rögzítve ügyeleti beosztás.";
                return;
            }

            var today = DateTime.Today;
            var cultureInfo = new CultureInfo("hu-HU");
            int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            var startOfWeek = today.AddDays(-1 * diff).Date;
            var endOfWeek = startOfWeek.AddDays(6).Date;

            var weeklyDuties = allDuties
                .Select(duty => {
                    DateTime.TryParse(duty.Data.GetValueOrDefault("DutyDate"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dutyDate);
                    return new { Duty = duty, Date = dutyDate, HasDate = duty.Data.ContainsKey("DutyDate") };
                })
                .Where(d => d.HasDate && d.Date >= startOfWeek && d.Date <= endOfWeek)
                .OrderBy(d => d.Date)
                .ToList();

            if (!weeklyDuties.Any())
            {
                _statusTextBlock.Text = "Erre a hétre nincs beosztás.";
                _dutyListBox.ItemsSource = null;
                return;
            }
            
            var displayItems = weeklyDuties.Select(item => 
            {
                var dateDisplay = item.Date.ToString("yyyy. MM. dd.");
                var dayName = cultureInfo.DateTimeFormat.GetDayName(item.Date.DayOfWeek);
                var location = item.Duty.Data.GetValueOrDefault("Location", "N/A");
                var studentName = item.Duty.Data.GetValueOrDefault("StudentName", "N/A");
                return $"{dateDisplay} ({dayName}): {studentName} - {location}";
            }).ToList();

            _dutyListBox.ItemsSource = displayItems;
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba történt az adatok betöltése közben: {ex.Message}";
        }
    }
}