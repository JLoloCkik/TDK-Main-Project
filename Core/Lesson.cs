using System;

namespace Kreta.Core;

public class Lesson
{
    public int Id { get; set; }
    public string ClassName { get; set; } = string.Empty; // e.g. "9.A"
    public string SubjectName { get; set; } = string.Empty; // e.g. "Mathematics"
    public string Room { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
}