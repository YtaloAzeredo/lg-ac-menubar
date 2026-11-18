namespace LGAC.Linux.Platform;

internal sealed class ActivationRelay
{
    private Action? target;
    private bool pending;

    public void Request()
    {
        if (target is null)
        {
            pending = true;
            return;
        }

        target();
    }

    public void Attach(Action activate)
    {
        target = activate;
        if (pending)
        {
            pending = false;
            activate();
        }
    }
}
