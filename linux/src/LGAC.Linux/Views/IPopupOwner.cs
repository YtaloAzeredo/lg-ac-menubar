namespace LGAC.Linux.Views;

public interface IPopupOwner
{
    bool HasOpenPopup { get; }

    event EventHandler? PopupClosed;
}
