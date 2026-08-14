using TNRD.Zeepkist.GTR.Ghosting.Playback;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public interface IGhost
{
    Color Color { get; }
    float Duration { get; }
    void Initialize(
        GhostData ghost,
        BulkGhostModeState bulkModeState,
        GhostTimingService timingService);
    void ApplyCosmetics(string steamName);
    void Start(float time);
    void Stop(float time);
    void Seek(float time);
    void Pause(float time);
    void Resume(float time);
    void Sample(float time);
}
