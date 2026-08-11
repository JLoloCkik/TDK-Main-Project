using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class JegyekAtlagokView : IEvolView
{
    private readonly IStudentContext? _studentContext;

    public JegyekAtlagokView() { }

    public JegyekAtlagokView(IStudentContext context)
    {
        _studentContext = context;
    }

    public new string Name => "Jegyek és Átlagok";
    public string Description => "Itt láthatod a jegyeidet és a tantárgyankénti átlagaidat.";

    public Control CreateView()
    {
        var mainPanel = new StackPanel
        {
            Margin = new Thickness(10),
            Spacing = 10
        };

        if (_studentContext == null)
        {
            mainPanel.Children.Add(new TextBlock { Text = "A nézet betöltéséhez context szükséges." });
            return mainPanel;
        }

        var grades = _studentContext.GetMyGrades();

        if (grades == null || !grades.Any())
        {
            mainPanel.Children.Add(new TextBlock { Text = "Nincsenek rögzített jegyeid." });
            return mainPanel;
        }

        var gradesBySubject = grades
            .Where(g => g.Subject != null && !string.IsNullOrEmpty(g.Subject.Name))
            .GroupBy(g => g.Subject!.Name)
            .OrderBy(g => g.Key);

        if (!gradesBySubject.Any())
        {
             mainPanel.Children.Add(new TextBlock { Text = "Nincsenek rögzített jegyeid." });
             return mainPanel;
        }

        foreach (var subjectGroup in gradesBySubject)
        {
            var subjectBorder = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 5)
            };

            var contentStack = new StackPanel { Spacing = 5 };

            contentStack.Children.Add(new TextBlock
            {
                Text = subjectGroup.Key,
                FontWeight = FontWeight.Bold,
                FontSize = 16
            });

            var gradeValues = subjectGroup.Select(g => g.Value).ToList();
            var gradesText = string.Join(", ", gradeValues);

            contentStack.Children.Add(new TextBlock
            {
                Text = "Jegyek: " + gradesText
            });

            var average = gradeValues.Average();
            var atlagText = "Tantargyi atlag: " + average.ToString("F2");

            contentStack.Children.Add(new TextBlock
            {
                Text = atlagText,
                FontWeight = FontWeight.SemiBold
            });

            subjectBorder.Child = contentStack;
            mainPanel.Children.Add(subjectBorder);
        }
        
        var scrollViewer = new ScrollViewer();
        scrollViewer.Content = mainPanel;

        return scrollViewer;
    }
}
