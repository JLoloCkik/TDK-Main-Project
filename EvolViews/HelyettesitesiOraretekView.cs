using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class HelyettesitesiOraretekView : IEvolView
{
    private ITeacherContext? _context;
    private ListBox? _substitutionListBox;
    private DatePicker? _datePicker;
    private TextBlock? _statusTextBlock;
    private List<GenericRecord>? _allSubstitutions;
    private const string SubstitutionEntityType = "Substitution";

    public new string Name => "Helyettesítési Órarend";
    public string Description => "A napi helyettesítések megtekintése, teremváltási figyelmeztetéssel.";

    public HelyettesitesiOraretekView() { }

    public HelyettesitesiOraretekView(ITeacherContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(10)
        };

        var header = new TextBlock
        {
            Text = "Helyettesítések Megtekintése",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _datePicker = new DatePicker
        {
            SelectedDate = DateTime.Today
        };
        _datePicker.SelectedDateChanged += OnDateChanged;

        var datePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Dátum:", VerticalAlignment = VerticalAlignment.Center },
                _datePicker
            }
        };

        _statusTextBlock = new TextBlock
        {
            Text = "Adatok betöltése...",
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _substitutionListBox = new ListBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 400
        };

        mainPanel.Children.Add(header);
        mainPanel.Children.Add(datePanel);
        mainPanel.Children.Add(_statusTextBlock);
        mainPanel.Children.Add(new ScrollViewer { Content = _substitutionListBox });

        LoadAllSubstitutions();

        return mainPanel;
    }

    private void LoadAllSubstitutions()
    {
        if (_context == null)
        {
            if (_statusTextBlock != null)
                _statusTextBlock.Text = "Hiba: A kontextus nincs beállítva.";
            return;
        }

        _allSubstitutions = _context.QueryEntities(SubstitutionEntityType);
        DisplaySubstitutionsForSelectedDate();
    }

    private void OnDateChanged(object? sender, DatePickerSelectedValueChangedEventArgs e)
    {
        DisplaySubstitutionsForSelectedDate();
    }

    private void DisplaySubstitutionsForSelectedDate()
    {
        if (_substitutionListBox == null || _datePicker?.SelectedDate == null || _statusTextBlock == null)
        {
            return;
        }

        if (_allSubstitutions == null)
        {
            _statusTextBlock.Text = "Helyettesítési adatok nem érhetőek el.";
            _statusTextBlock.IsVisible = true;
            _substitutionListBox.ItemsSource = null;
            return;
        }

        var selectedDate = _datePicker.SelectedDate.Value.Date;
        var substitutionsForDay = _allSubstitutions
            .Where(s => s.Data.ContainsKey("Date") && DateTime.TryParse(s.Data["Date"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var subDate) && subDate.Date == selectedDate)
            .OrderBy(s => s.Data.GetValueOrDefault("LessonTime", "99"))
            .ToList();

        if (substitutionsForDay.Any())
        {
            _statusTextBlock.IsVisible = false;
            var listBoxItems = new List<Control>();
            foreach (var sub in substitutionsForDay)
            {
                var itemBorder = new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 5),
                    CornerRadius = new CornerRadius(5)
                };

                var itemPanel = new StackPanel { Spacing = 5 };

                string className = sub.Data.GetValueOrDefault("ClassName", "N/A");
                string subjectName = sub.Data.GetValueOrDefault("SubjectName", "N/A");
                string lessonTime = sub.Data.GetValueOrDefault("LessonTime", "N/A");
                string substituteTeacher = sub.Data.GetValueOrDefault("SubstituteTeacherName", "N/A");
                string originalRoom = sub.Data.GetValueOrDefault("OriginalRoom", "N/A");
                string newRoom = sub.Data.GetValueOrDefault("NewRoom", "");

                itemPanel.Children.Add(new TextBlock { Text = $"{lessonTime}: {className} - {subjectName}", FontWeight = FontWeight.Bold, FontSize = 14 });
                itemPanel.Children.Add(new TextBlock { Text = $"Helyettesítő tanár: {substituteTeacher}" });

                if (!string.IsNullOrEmpty(newRoom) && newRoom != originalRoom)
                {
                    var warningText = new TextBlock
                    {
                        Text = $"FIGYELEM: TEREMVÁLTOZÁS! Régi: {originalRoom}, Új: {newRoom}",
                        Foreground = Brushes.OrangeRed,
                        FontWeight = FontWeight.Bold
                    };
                    itemPanel.Children.Add(warningText);
                }
                else
                {
                    itemPanel.Children.Add(new TextBlock { Text = $"Terem: {originalRoom}" });
                }

                itemBorder.Child = itemPanel;
                listBoxItems.Add(itemBorder);
            }
            _substitutionListBox.ItemsSource = listBoxItems;
        }
        else
        {
            _statusTextBlock.Text = "A kiválasztott napon nincsenek helyettesítések.";
            _statusTextBlock.IsVisible = true;
            _substitutionListBox.ItemsSource = null;
        }
    }
}
