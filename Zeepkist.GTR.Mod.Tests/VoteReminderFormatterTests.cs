using TNRD.Zeepkist.GTR.Voting;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class VoteReminderFormatterTests
{
    [Fact]
    public void FormatsCountsAndHighlightsCurrentVote()
    {
        var summary = new VoteSummary(
            new Dictionary<int, long>
            {
                [-2] = 0,
                [-1] = 3,
                [0] = 0,
                [1] = 4,
                [2] = 8
            },
            2);

        string message = VoteReminderFormatter.Format(summary);

        Assert.Contains("<size=50%>(3)</size>", message);
        Assert.Contains("<size=50%>(8)</size>", message);
        Assert.Contains("[<b><color=#00FF00>++</color></b>]", message);
        Assert.DoesNotContain("[<b><color=#80FF00>+</color></b>]", message);
    }

    [Fact]
    public void HighlightsEntireNeutralVoteLabel()
    {
        var summary = new VoteSummary(new Dictionary<int, long>(), 0);

        string message = VoteReminderFormatter.Format(summary);

        Assert.Contains("[<b><color=#FFFF00>-+/+-</color></b>]", message);
    }

    [Fact]
    public void MissingKnownGroupsUseZeroCounts()
    {
        var summary = new VoteSummary(new Dictionary<int, long> { [2] = 8 }, null);

        string message = VoteReminderFormatter.Format(summary);

        Assert.Equal(4, CountOccurrences(message, "<size=50%>(0)</size>"));
        Assert.Equal(1, CountOccurrences(message, "<size=50%>(8)</size>"));
    }

    [Fact]
    public void UnknownSummaryUsesQuestionMarkCountsWithoutHighlight()
    {
        string message = VoteReminderFormatter.Format(VoteSummary.Unknown);

        Assert.Equal(5, CountOccurrences(message, "<size=50%>(?)</size>"));
        Assert.DoesNotContain("[<b>", message);
    }

    [Fact]
    public void ReminderPolicyOnlyHidesConfirmedExistingVote()
    {
        var voted = new VoteSummary(new Dictionary<int, long>(), 1);
        var unvoted = new VoteSummary(new Dictionary<int, long>(), null);

        Assert.True(VoteReminderFormatter.ShouldShow(true, voted));
        Assert.False(VoteReminderFormatter.ShouldShow(false, voted));
        Assert.True(VoteReminderFormatter.ShouldShow(false, unvoted));
        Assert.True(VoteReminderFormatter.ShouldShow(false, VoteSummary.Unknown));
    }

    private static int CountOccurrences(string value, string expected)
    {
        return value.Split(new[] { expected }, StringSplitOptions.None).Length - 1;
    }
}
