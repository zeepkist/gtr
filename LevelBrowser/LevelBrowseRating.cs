namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// Vote-quality rating preset. Well-rated = average vote value &gt; 0; Top-rated = net vote sum
/// ≥ <see cref="LevelItemsBrowseQuery.TopRatedNetScore"/>.
/// </summary>
public enum LevelBrowseRating
{
    Any = 0,
    WellRated = 1,
    TopRated = 2
}
