namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// Result of asking the session to select a browse row.
/// </summary>
public enum SelectionOutcome
{
    /// <summary>The row was free; the host callback received one Level Browser Selection.</summary>
    Selected,

    /// <summary>The row is already in the host's set; no Selection was emitted (host shows a toast).</summary>
    AlreadyIn,

    /// <summary>The session was not open, so nothing happened.</summary>
    NotOpen
}
