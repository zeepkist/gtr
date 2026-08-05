using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace TNRD.Zeepkist.GTR.LevelBrowser.UI;

/// <summary>
/// Immediate-mode texture cache for level thumbnails. Downloads each <c>imageUrl</c> once via
/// <see cref="UnityWebRequestTexture"/> and hands the loaded <see cref="Texture2D"/> back to the
/// browser window on later frames. Failures are remembered so a broken URL is not retried in a loop.
/// </summary>
public sealed class LevelThumbnailCache
{
    private enum Status
    {
        Loading,
        Loaded,
        Failed
    }

    private sealed class Entry
    {
        public Status Status;
        public Texture2D Texture;
        public UnityWebRequest Request;
    }

    private readonly Dictionary<string, Entry> _entries = new();

    /// <summary>
    /// Returns the loaded texture for <paramref name="url"/>, or <c>null</c> while it is loading,
    /// failed, or empty. Starts a download the first time a URL is seen.
    /// </summary>
    public Texture2D Get(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        if (_entries.TryGetValue(url, out Entry entry))
            return entry.Status == Status.Loaded ? entry.Texture : null;

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        request.SendWebRequest();
        _entries[url] = new Entry { Status = Status.Loading, Request = request };
        return null;
    }

    /// <summary>Advances all in-flight downloads. Call once per frame before drawing cards.</summary>
    public void Poll()
    {
        foreach (Entry entry in _entries.Values)
        {
            if (entry.Status != Status.Loading)
                continue;

            if (entry.Request == null || !entry.Request.isDone)
                continue;

            if (entry.Request.result == UnityWebRequest.Result.Success)
            {
                entry.Texture = DownloadHandlerTexture.GetContent(entry.Request);
                entry.Status = Status.Loaded;
            }
            else
            {
                entry.Status = Status.Failed;
            }

            entry.Request.Dispose();
            entry.Request = null;
        }
    }
}
