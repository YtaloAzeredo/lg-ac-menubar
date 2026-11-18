using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace LGAC.Linux.Views;

internal sealed class EffectiveVisibility : IDisposable
{
    private readonly Control owner;
    private readonly Action<bool> changed;
    private readonly List<Visual> watched = [];
    private bool disposed;

    public EffectiveVisibility(Control owner, Action<bool> changed)
    {
        this.owner = owner;
        this.changed = changed;
        owner.AttachedToVisualTree += OnTreeChanged;
        owner.DetachedFromVisualTree += OnTreeChanged;
        Rewatch();
        IsVisible = Compute();
    }

    public bool IsVisible { get; private set; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        owner.AttachedToVisualTree -= OnTreeChanged;
        owner.DetachedFromVisualTree -= OnTreeChanged;
        Unwatch();
        Update();
    }

    private bool Compute()
    {
        if (disposed || !owner.IsVisible || TopLevel.GetTopLevel(owner) is not { } top || !top.IsVisible)
        {
            return false;
        }

        return owner.GetVisualAncestors().All(ancestor => ancestor.IsVisible);
    }

    private void Rewatch()
    {
        Unwatch();
        if (disposed)
        {
            return;
        }

        watched.Add(owner);
        watched.AddRange(owner.GetVisualAncestors());
        foreach (var visual in watched)
        {
            visual.PropertyChanged += OnPropertyChanged;
        }
    }

    private void Unwatch()
    {
        foreach (var visual in watched)
        {
            visual.PropertyChanged -= OnPropertyChanged;
        }

        watched.Clear();
    }

    private void OnTreeChanged(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Rewatch();
        Update();
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty)
        {
            Update();
        }
    }

    private void Update()
    {
        var now = Compute();
        if (now == IsVisible)
        {
            return;
        }

        IsVisible = now;
        changed(now);
    }
}
