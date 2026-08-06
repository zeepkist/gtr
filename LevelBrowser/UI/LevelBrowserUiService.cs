using TNRD.Zeepkist.GTR.Core;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.LevelBrowser.UI;

/// <summary>
/// Registers the <see cref="LevelBrowserWindow"/> as a Zeep GUI drawer so the reusable Level Browser
/// renders whenever its session is open.
/// </summary>
public class LevelBrowserUiService : IEagerService
{
    public LevelBrowserUiService(LevelBrowserWindow window)
    {
        UIApi.AddZeepGUIDrawer(window);
    }
}
