using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using Kreta.Core;
using Kreta.Contexts;

public class HianyzasaimView : IEvolView
{
    private IStudentContext? _context;
    private ListBox? _absencesListBox;
    private TextBlock? _unjustifiedCountText;
    private TextBlock? _statusText;
    private User? _currentUser;

    public new string Name => "Hiányzásaim";
    public string Description => "A diák saját hiányzásainak listázása és az igazolatlan órák számának összegzése.";

    public HianyzasaimView() { }

    public HianyzasaimView(IStudentContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(15)
        };

        _unjustifiedCountText = new TextBlock
        {
            Text = "Igazolatlan órák száma: Betöltés...",
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var refreshButton = new Button
        {
            Content = "Frissítés",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        refreshButton.Click += RefreshButton_Click;

        _absencesListBox = new ListBox
        {
            Height = 400,
            Margin = new Thickness(0, 10, 0, 0)
        };
        
        _statusText = new TextBlock
        {
            Text = "Adatok betöltése...",
            IsVisible = true
        };

        mainPanel.Children.Add(_unjustifiedCountText);
        mainPanel.Children.Add(refreshButton);
        mainPanel.Children.Add(_absencesListBox);
        mainPanel.Children.Add(_statusText);

        LoadAbsences();

        return mainPanel;
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        LoadAbsences();
    }

    private void LoadAbsences()
    {
        if (_context == null || _absencesListBox == null || _unjustifiedCountText == null || _statusText == null)
        {
            if (_statusText != null) 
                _statusText.Text = "Hiba: A nézet nincs megfelelően inicializálva.";
            return;
        }

        _currentUser = _context.GetMyProfile();
        if (_currentUser == null)
        {
            _statusText.Text = "Hiba: Nem sikerült lekérni a diák adatait.";
            return;
        }

        _statusText.Text = "Hiányzások lekérdezése...";
        var allAbsences = _context.QueryEntities("Absence");

        if (allAbsences == null || !allAbsences.Any())
        {
            _statusText.Text = "Nincsenek rögzített hiányzások.";
            _unjustifiedCountText.Text = "Igazolatlan órák száma: 0";
            _absencesListBox.ItemsSource = new List<string>();
            return;
        }

        var myAbsences = allAbsences
            .Where(a => a.Data.ContainsKey("StudentId") && a.Data["StudentId"] == _currentUser.Id.ToString())
            .OrderByDescending(a => a.Data.ContainsKey("Date") ? DateTime.Parse(a.Data["Date"]) : DateTime.MinValue)
            .ToList();

        var formattedAbsences = myAbsences.Select(a => {
            string date = a.Data.TryGetValue("Date", out var d) ? d : "N/A";
            string subject = a.Data.TryGetValue("SubjectName", out var s) ? s : "Ismeretlen";
            string state = a.Data.TryGetValue("JustificationState", out var st) ? st : "Ismeretlen";
            return $"{date} - {subject} ({state})";
        }).ToList();

        _absencesListBox.ItemsSource = formattedAbsences;

        int unjustifiedCount = myAbsences.Count(a => a.Data.TryGetValue("JustificationState", out var st) && st == "Unjustified");
        
        _unjustifiedCountText.Text = $"Igazolatlan órák száma: {unjustifiedCount}";
        _statusText.Text = $"Sikeresen betöltve {myAbsences.Count} hiányzás.";
    }
}