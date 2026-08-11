using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kreta.Core;
using Kreta.Contexts;

public class JegyBeirasView : IEvolView
{
    private readonly ITeacherContext? _teacherContext;

    public JegyBeirasView() { }

    public JegyBeirasView(ITeacherContext context)
    {
        _teacherContext = context;
    }

    public new string Name => "Jegy beírása";
    public string Description => "Új jegy rögzítése a diákok számára.";

    public Control CreateView()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(20)
        };

        if (_teacherContext == null)
        {
            panel.Children.Add(new TextBlock { Text = "A nézet betöltéséhez tanári jogosultság szükséges." });
            return panel;
        }

        var students = _teacherContext.GetMyClassStudents();
        var subjects = _teacherContext.GetAllSubjects();

        var statusMessage = new TextBlock { Text = "", Margin = new Thickness(0, 10) };

        panel.Children.Add(new TextBlock { Text = "Diák kiválasztása:" });
        var studentComboBox = new ComboBox
        {
            ItemsSource = students.Select(s => s.Name).ToList(),
            SelectedIndex = students.Any() ? 0 : -1
        };
        panel.Children.Add(studentComboBox);

        panel.Children.Add(new TextBlock { Text = "Tantárgy kiválasztása:" });
        var subjectComboBox = new ComboBox
        {
            ItemsSource = subjects.Select(s => s.Name).ToList(),
            SelectedIndex = subjects.Any() ? 0 : -1
        };
        panel.Children.Add(subjectComboBox);

        panel.Children.Add(new TextBlock { Text = "Érdemjegy:" });
        var gradeComboBox = new ComboBox
        {
            ItemsSource = new List<int> { 1, 2, 3, 4, 5 },
            SelectedIndex = 4
        };
        panel.Children.Add(gradeComboBox);

        var addButton = new Button { Content = "Jegy beírása" };
        addButton.Click += (sender, e) =>
        {
            if (studentComboBox.SelectedIndex < 0 || subjectComboBox.SelectedIndex < 0 || gradeComboBox.SelectedIndex < 0)
            {
                statusMessage.Text = "Kérem, válasszon diákot, tantárgyat és érdemjegyet!";
                statusMessage.Foreground = Brushes.Red;
                return;
            }

            try
            {
                var selectedStudent = students[studentComboBox.SelectedIndex];
                var selectedSubject = subjects[subjectComboBox.SelectedIndex];
                var selectedGradeValue = (int)gradeComboBox.SelectedItem!;

                var newGrade = new Grade
                {
                    StudentId = selectedStudent.Id,
                    SubjectId = selectedSubject.Id,
                    Value = selectedGradeValue,
                    Date = DateTime.Now
                };

                _teacherContext.AddGrade(selectedStudent.Id, newGrade);

                statusMessage.Text = $"{selectedStudent.Name} diák {selectedSubject.Name} tantárgyból {selectedGradeValue} érdemjegyet kapott.";
                statusMessage.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                statusMessage.Text = $"Hiba történt a jegy beírása közben: {ex.Message}";
                statusMessage.Foreground = Brushes.Red;
            }
        };

        panel.Children.Add(addButton);
        panel.Children.Add(statusMessage);

        return panel;
    }
}
