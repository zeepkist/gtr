namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// Track-length preset based on <c>validationTimeAuthor</c> (seconds): Short &lt; 30,
/// Medium 30–90, Long ≥ 90.
/// </summary>
public enum LevelBrowseTrackLength
{
    Any = 0,
    Short = 1,
    Medium = 2,
    Long = 3
}
