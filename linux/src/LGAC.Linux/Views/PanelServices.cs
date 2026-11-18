using LGAC.Core;
using LGAC.Linux.Platform;

namespace LGAC.Linux.Views;

public sealed record PanelServices(ACController Controller, ICredentialStore Store, AutostartEntry Autostart, Action Quit);
