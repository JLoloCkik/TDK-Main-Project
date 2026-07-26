using System;

namespace Kreta.Core;

public class Grade
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int SubjectId { get; set; }
    public int Value { get; set; } // 1-5
    public DateTime Date { get; set; } = DateTime.Now;

    public User? Student { get; set; }
    public Subject? Subject { get; set; }
}