using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class TanariOraszamOsszesitoView : IEvolView
{
    private IDirectorContext? _context;
    private ListBox? _teacherHoursListBox;
    private TextBlock? _statusTextBlock;
    private const string AssignmentEntityType = "TeacherSubjectAssignment";
    private const string SubjectEntityType = "Subject";

    public new string Name => "Tanári Óraszám Összesítő";
    public string Description => "A tanárok heti összóraszámának és tantárgyfelosztásának részletes megjelenítése.";

    public TanariOraszamOsszesitoView() { }

    public TanariOraszamOsszesitoView(IDirectorContext context)
    {
        _context = context;
    }

    public Control CreateView()
    {
        _teacherHoursListBox = new ListBox
        {
            Margin = new Avalonia.Thickness(10)
        };

        _statusTextBlock = new TextBlock
        {
            Margin = new Avalonia.Thickness(10, 0, 10, 10),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var mainPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                new TextBlock { Text = Name, FontSize = 20, FontWeight = FontWeight.Bold, Margin = new Avalonia.Thickness(10) },
                new ScrollViewer { Content = _teacherHoursListBox, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Height=500 },
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
            var subjects = _context.QueryEntities(SubjectEntityType).ToDictionary(s => s.Id.ToString(), s => s.Data.GetValueOrDefault("Name", "Ismeretlen tantárgy"));

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
                string teacherId = group.Key;
                var teacher = users[teacherId];
                int totalHours = group.Sum(a => int.TryParse(a.Data.GetValueOrDefault("WeeklyHours", "0"), out int h) ? h : 0);

                displayItems.Add($"{teacher.Name}: {totalHours} óra/hét");

                foreach (var assignment in group.OrderBy(a => a.Data.GetValueOrDefault("ClassName", "")))
                {
                    string subjectId = assignment.Data.GetValueOrDefault("SubjectId", "");
                    string subjectName = subjects.GetValueOrDefault(subjectId, "???");
                    string className = assignment.Data.GetValueOrDefault("ClassName", "???");
                    string hours = assignment.Data.GetValueOrDefault("WeeklyHours", "?");
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
