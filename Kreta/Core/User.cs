namespace Kreta.Core;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Role Role { get; set; }
    public string ClassName { get; set; } = string.Empty;
}