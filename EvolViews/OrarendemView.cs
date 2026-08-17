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

public class OrarendemView : IEvolView
{
    private IStudentContext? _context;

    public new string Name => "Órarendem";
    public string Description => "A diák személyes órarendjének megtekintése, a mai nap kiemelésével.";

    public OrarendemView() { }

    public OrarendemView(IStudentContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        var mainScrollViewer = new ScrollViewer();
        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 5,
            Margin = new Thickness(10)
        };
        mainScrollViewer.Content = mainPanel;

        var statusTextBlock = new TextBlock
        {
            Text = "Órarend betöltése...",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(10)
        };
        mainPanel.Children.Add(statusTextBlock);

        if (_context == null)
        {
            statusTextBlock.Text = "Hiba: A diák kontextus nem elérhető.";
            return mainScrollViewer;
        }

        try
        {
            var lessons = _context.GetMyLessons();

            if (lessons == null || !lessons.Any())
            {
                statusTextBlock.Text = "Nincsenek óráid rögzítve a rendszerben.";
                return mainScrollViewer;
            }

            mainPanel.Children.Remove(statusTextBlock);

            var culture = new CultureInfo("hu-HU");
            var today = DateTime.Now.Date;

            var groupedLessons = lessons
                .OrderBy(l => l.Date)
                .GroupBy(l => l.Date.Date)
                .ToList();

            foreach (var dayGroup in groupedLessons)
            {
                var lessonDate = dayGroup.Key;
                bool isToday = lessonDate == today;

                var dayHeader = new TextBlock
                {
                    Text = $"{culture.DateTimeFormat.GetDayName(lessonDate.DayOfWeek).ToUpper()}, {lessonDate:yyyy. MM. dd.}",
                    FontSize = 16,
                    FontWeight = FontWeight.Bold,
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 10, 0, 5),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    TextAlignment = TextAlignment.Center,
                    Background = isToday ? Brushes.CornflowerBlue : Brushes.LightGray,
                    Foreground = isToday ? Brushes.White : Brushes.Black
                };
                mainPanel.Children.Add(dayHeader);

                foreach (var lesson in dayGroup.OrderBy(l => l.Date))
                {
                    var lessonBorder = new Border
                    {
                        Padding = new Thickness(10),
                        Margin = new Thickness(5, 0, 5, 0),
                        CornerRadius = new CornerRadius(5),
                        Background = isToday ? Brushes.LightGoldenrodYellow : Brushes.WhiteSmoke,
                        BorderBrush = Brushes.Gray,
                        BorderThickness = new Thickness(1)
                    };

                    var lessonGrid = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto")
                    };

                    var subjectText = new TextBlock
                    {
                        Text = lesson.SubjectName,
                        FontWeight = FontWeight.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(subjectText, 0);

                    var roomText = new TextBlock
                    {
                        Text = $"Terem: {lesson.Room}",
                        Foreground = Brushes.DarkSlateGray,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(roomText, 1);
                    
                    lessonGrid.Children.Add(subjectText);
                    lessonGrid.Children.Add(roomText);
                    lessonBorder.Child = lessonGrid;

                    mainPanel.Children.Add(lessonBorder);
                }
            }
        }
        catch (Exception ex)
        {
            statusTextBlock.Text = $"Hiba történt az órarend betöltése közben: {ex.Message}";
            if(!mainPanel.Children.Contains(statusTextBlock))
            {
                 mainPanel.Children.Insert(0, statusTextBlock);
            }
        }

        return mainScrollViewer;
    }
}
