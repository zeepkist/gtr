using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Voting;

public class VoteSummary
{
    private readonly IReadOnlyDictionary<int, long> _counts;

    public static VoteSummary Unknown { get; } = new(null, null);

    public int? CurrentVote { get; }
    public bool CountsKnown => _counts != null;

    public VoteSummary(IReadOnlyDictionary<int, long> counts, int? currentVote)
    {
        _counts = counts;
        CurrentVote = currentVote is >= -2 and <= 2 ? currentVote : null;
    }

    public long? GetCount(int voteValue)
    {
        if (_counts == null)
            return null;

        return _counts.TryGetValue(voteValue, out long count) ? count : 0;
    }
}
