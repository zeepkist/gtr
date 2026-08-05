using UnityEngine;

namespace TNRD.Zeepkist.GTR.PlaylistBrowserHost;

/// <summary>
/// Keeps the injected "Browse levels" <see cref="GenericButton"/> disabled while any workshop download
/// is in flight, mirroring how the host controls gate playlist edits during downloads. Reads the
/// <see cref="WorkshopManager"/> singleton (not a scene search) so it is cheap to poll each frame.
/// </summary>
public sealed class BrowseLevelsDownloadGuard : MonoBehaviour
{
    public GenericButton button;

    private void Update()
    {
        if (button == null)
            return;

        bool downloading = WorkshopManager.Instance != null && WorkshopManager.Instance.IsDownloadingAnything();
        if (button.disabled == downloading)
            return;

        button.disabled = downloading;
        button.RedrawButton();
    }
}
