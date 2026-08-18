using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Contexts;
using Kreta.Core;

public class TanariOraszamOsszesitoView : IEvolView
{
    private const string AssignmentEntityType = "TeacherSubjectAssignment";
    private const string SubjectEntityType = "Subject";
    private readonly IDirectorContext? _context;
    private TextBlock? _statusTextBlock;
    private ListBox? _teacherHoursListBox;

    public TanariOraszamOsszesitoView()
    {
    }

    public TanariOraszamOsszesitoView(IDirectorContext context)
    {
        _context = context;
    }

    public new string Name => "Tanári Óraszám Összesítő";
    public string Description => "A tanárok heti összóraszámának és tantárgyfelosztásának részletes megjelenítése.";

    public Control CreateView()
    {
        _teacherHoursListBox = new ListBox
        {
            Margin = new Thickness(10)
        };

        _statusTextBlock = new TextBlock
        {
            Margin = new Thickness(10, 0, 10, 10),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                new TextBlock { Text = Name, FontSize = 20, FontWeight = FontWeight.Bold, Margin = new Thickness(10) },
                new ScrollViewer
                {
                    Content = _teacherHoursListBox, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 500
                },
                _statusTextBlock
            }
        };

        LoadData();

        return mainPanel;
    }

    private void LoadData()
    {
        if (_context == null || _teacherHoursListBox == null || _statusTextBlock == null) return;

        try
        {
            var assignments = _context.QueryEntities(AssignmentEntityType);
            var users = _context.GetAllUsers().Where(u => u.Role == Role.Teacher).ToDictionary(u => u.Id.ToString());
            var subjects = _context.QueryEntities(SubjectEntityType).ToDictionary(s => s.Id.ToString(),
                s => s.Data.GetValueOrDefault("Name", "Ismeretlen tantárgy"));

            if (assignments == null || !assignments.Any())
            {
                _statusTextBlock.Text = "Nincs rögzítve tantárgyfelosztás.";
                return;
            }

            var assignmentsByTeacher = assignments
                .Where(a => a.Data.ContainsKey("TeacherId") && users.ContainsKey(a.Data["TeacherId"]))
                .GroupBy(a => a.Data["TeacherId"])
                .OrderBy(g => users[g.Key].Name);

            var displayItems = new List<string>();
            foreach (var group in assignmentsByTeacher)
            {
                var teacherId = group.Key;
                var teacher = users[teacherId];
                var totalHours = group.Sum(a =>
                    int.TryParse(a.Data.GetValueOrDefault("WeeklyHours", "0"), out var h) ? h : 0);

                displayItems.Add($"{teacher.Name}: {totalHours} óra/hét");

                foreach (var assignment in group.OrderBy(a => a.Data.GetValueOrDefault("ClassName", "")))
                {
                    var subjectId = assignment.Data.GetValueOrDefault("SubjectId", "");
                    var subjectName = subjects.GetValueOrDefault(subjectId, "???");
                    var className = assignment.Data.GetValueOrDefault("ClassName", "???");
                    var hours = assignment.Data.GetValueOrDefault("WeeklyHours", "?");
                    displayItems.Add($"    - {subjectName} ({className}): {hours} óra");
                }
            }

            _teacherHoursListBox.ItemsSource = displayItems;
            _statusTextBlock.Text = $"{assignmentsByTeacher.Count()} tanár adatai betöltve.";
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = $"Hiba a betöltés során: {ex.Message}";
        }
    }
}