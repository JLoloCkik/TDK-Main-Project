using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class TanoraiHianyzasokView : IEvolView
{
    private const string AbsenceEntityType = "Absence";
    private readonly IStudentContext? _context;
    private ListBox? _missedLessonsListBox;
    private TextBlock? _statusTextBlock;

    public TanoraiHianyzasokView()
    {
    }

    public TanoraiHianyzasokView(IStudentContext context)
    {
        _context = context;
    }

    public new string Name => "Tanórai Hiányzások";
    public string Description => "A diák által mulasztott tanórák részletes listája.";

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
            Text = "Mulasztott Tanóráim",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _statusTextBlock = new TextBlock
        {
            Text = "Adatok betöltése...",
            HorizontalAlignment = HorizontalAlignment.Center,
            IsVisible = true
        };

        _missedLessonsListBox = new ListBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 300
        };

        mainPanel.Children.Add(title);
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(_missedLessonsListBox);

        LoadMissedLessons();

        return mainPanel;
    }

    private void LoadMissedLessons()
    {
        if (_context == null || _missedLessonsListBox == null || _statusTextBlock == null)
        {
            if (_statusTextBlock != null)
                _statusTextBlock.Text = "Hiba: A nézet nincs megfelelően beállítva.";
            return;
        }

        try
        {
            var currentUser = _context.GetMyProfile();
            if (currentUser == null)
            {
                _statusTextBlock.Text = "Hiba: Nem sikerült azonosítani a felhasználót.";
                return;
            }

            var allAbsences = _context.QueryEntities(AbsenceEntityType);

            var myAbsences = allAbsences
                .Where(a => a.Data.ContainsKey("StudentId") && a.Data["StudentId"] == currentUser.Id.ToString())
                .OrderByDescending(a =>
                    a.Data.ContainsKey("Date")
                        ? DateTime.Parse(a.Data["Date"], CultureInfo.InvariantCulture)
                        : DateTime.MinValue)
                .ToList();

            if (!myAbsences.Any())
            {
                _statusTextBlock.Text = "Nincsenek rögzített hiányzások.";
                _missedLessonsListBox.IsVisible = false;
            }
            else
            {
                var displayItems = myAbsences.Select(absence =>
                {
                    var dateStr = absence.Data.TryGetValue("Date", out var d)
                        ? DateTime.Parse(d, CultureInfo.InvariantCulture).ToString("yyyy.MM.dd.")
                        : "Ismeretlen dátum";
                    var subjectStr = absence.Data.TryGetValue("SubjectName", out var s) ? s : "Ismeretlen tantárgy";
                    var topicStr = absence.Data.TryGetValue("Topic", out var t) ? $": {t}" : "";
                    var statusStr = absence.Data.TryGetValue("JustificationStatus", out var js) ? js : "Igazolandó";

                    return $"{dateStr} - {subjectStr}{topicStr} ({statusStr})";
                }).ToList();

                _missedLessonsListBox.ItemsSource = displayItems;
                _statusTextBlock.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba történt az adatok lekérése során: {ex.Message}";
            _statusTextBlock.Foreground = Brushes.Red;
            _missedLessonsListBox.IsVisible = false;
        }
    }
}