using System.Runtime.InteropServices.JavaScript;

namespace Kreta.Core;

public class Grade
{
    public int Id { get; set; }
    
    public int Value { get; set; }
    public int Weight { get; set; } 
    public System.DateTime Date { get; set; }
    
    public int StudentId { get; set; }
    public User Student { get; set; } = null!; 
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}