using Avalonia.Controls;

namespace Kreta.Core;

public interface IEvolView
{
    string Name { get; }
    string Description { get; }
    Control CreateView();
}