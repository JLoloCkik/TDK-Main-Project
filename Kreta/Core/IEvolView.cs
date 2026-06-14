namespace Kreta.Core;

public interface IEvolView {
    string ViewName { get; }
    Role RequiredRole { get; }
}