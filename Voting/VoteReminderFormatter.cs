using System.Globalization;

namespace TNRD.Zeepkist.GTR.Voting;

public static class VoteReminderFormatter
{
    public static bool ShouldShow(bool showAfterVoting, VoteSummary summary)
    {
        return showAfterVoting || !summary.CurrentVote.HasValue;
    }

    public static string Format(VoteSummary summary)
    {
        return
            "<size=80%><color=#FFFF00>Cast your vote for ZeepCentraal:</color></size><br>" +
            "<size=75%>" +
            FormatChoice(summary, -2, "--", "#FF0000") + " " +
            FormatChoice(summary, -1, "-", "#FF8000") + " " +
            FormatChoice(summary, 0, "-+/+-", "#FFFF00") + " " +
            FormatChoice(summary, 1, "+", "#80FF00") + " " +
            FormatChoice(summary, 2, "++", "#00FF00") +
            "</size>";
    }

    private static string FormatChoice(VoteSummary summary, int voteValue, string label, string colour)
    {
        string choice = $"<b><color={colour}>{label}</color></b>";
        if (summary.CurrentVote == voteValue)
            choice = $"[{choice}]";

        long? count = summary.GetCount(voteValue);
        string countText = count?.ToString(CultureInfo.InvariantCulture) ?? "?";
        return $"{choice}<size=50%>({countText})</size>";
    }
}
