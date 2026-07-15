using System;

namespace Kreta.Core;

public class Lesson
{
    public int Id { get; set; }
    public string ClassName { get; set; } = string.Empty; // Pl. "9.A"
    public string SubjectName { get; set; } = string.Empty; // Pl. "Matematika"
    public string Room { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
}