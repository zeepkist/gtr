using System;
using System.Net.Http;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Steamworks;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Level;
using ZeepSDK.Messaging;
using ZeepSDK.Multiplayer;

namespace TNRD.Zeepkist.GTR.Voting;

[UsedImplicitly]
public class VotingService : IEagerService
{
    private const string TIME_LEFT = "00:30";

    private readonly PlayerLoopService _playerLoopService;
    private readonly ILogger<VotingService> _logger;
    private readonly ApiHttpClient _apiHttpClient;
    private readonly ConfigService _configService;
    private readonly VotingGraphqlService _votingGraphqlService;

    private string _previousTimeLeft;
    private int _reminderRequestVersion;

    public VotingService(PlayerLoopService playerLoopService, ILogger<VotingService> logger,
        ApiHttpClient apiHttpClient, ConfigService configService, VotingGraphqlService votingGraphqlService)
    {
        _playerLoopService = playerLoopService;
        _logger = logger;
        _apiHttpClient = apiHttpClient;
        _configService = configService;
        _votingGraphqlService = votingGraphqlService;
        _playerLoopService.SubscribeUpdate(OnUpdate);
    }

    private void OnUpdate()
    {
        if (!MultiplayerApi.IsPlayingOnline)
        {
            if (_previousTimeLeft != null)
            {
                _previousTimeLeft = null;
                _reminderRequestVersion++;
            }
            return;
        }

        string currentTimeLeft = ZeepkistNetwork.CurrentLobby.timeLeftString;

        if (currentTimeLeft == TIME_LEFT && _previousTimeLeft != TIME_LEFT)
            ShowVoteReminderAsync(++_reminderRequestVersion).Forget();

        _previousTimeLeft = currentTimeLeft;
    }

    private async UniTaskVoid ShowVoteReminderAsync(int requestVersion)
    {
        string currentHash = LevelApi.CurrentHashV2?.Hash;
        VoteSummary summary = VoteSummary.Unknown;

        if (string.IsNullOrEmpty(currentHash))
        {
            _logger.LogError("Unable to get vote summary because current level hash is empty");
        }
        else
        {
            Result<VoteSummary> result = await _votingGraphqlService.GetVoteSummary(
                currentHash,
                SteamClient.SteamId.Value,
                CancellationToken.None);
            if (result.IsSuccess)
            {
                summary = result.Value;
            }
            else
            {
                _logger.LogError("Failed to get vote summary: {Result}", result);
            }
        }

        if (requestVersion != _reminderRequestVersion ||
            !MultiplayerApi.IsPlayingOnline ||
            !string.Equals(currentHash, LevelApi.CurrentHashV2?.Hash, StringComparison.Ordinal))
        {
            return;
        }

        if (!VoteReminderFormatter.ShouldShow(_configService.ShowVoteReminderAfterVoting.Value, summary))
            return;

        ChatApi.AddLocalMessage(VoteReminderFormatter.Format(summary));
    }

    private void OnVoteSuccess(string vote)
    {
        MessengerApi.LogSuccess($"Voted {vote}");
    }

    private void OnVoteFail(string vote)
    {
        MessengerApi.LogError($"Vote {vote} failed");
    }

    public void DoubleDownvote()
    {
        VoteAsync(
            -2,
            () => { OnVoteSuccess("--"); },
            () => { OnVoteFail("--"); }
        ).Forget();
    }

    public void DoubleUpvote()
    {
        VoteAsync(
            2,
            () => { OnVoteSuccess("++"); },
            () => { OnVoteFail("++"); }
        ).Forget();
    }

    public void Downvote()
    {
        VoteAsync(
            -1,
            () => { OnVoteSuccess("-"); },
            () => { OnVoteFail("-"); }
        ).Forget();
    }

    public void Upvote()
    {
        VoteAsync(
            1,
            () => { OnVoteSuccess("+"); },
            () => { OnVoteFail("+"); }
        ).Forget();
    }

    public void NeutralVote()
    {
        VoteAsync(
            0,
            () => { OnVoteSuccess("-+/+-"); },
            () => { OnVoteFail("-+/+-"); }
        ).Forget();
    }

    private async UniTaskVoid VoteAsync(int voteValue, Action onSuccess, Action onFail)
    {
        string currentHash = LevelApi.CurrentHashV2?.Hash;

        if (string.IsNullOrEmpty(currentHash))
        {
            _logger.LogError("Unable to vote because current level hash is empty");
            onFail();
            return;
        }

        using HttpResponseMessage response = await _apiHttpClient.PostAsync(
            "vote/submit",
            new VoteResource
            {
                Hash = currentHash,
                Value = voteValue
            }
        );

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to vote");
            onFail();
            return;
        }

        onSuccess();
    }
}
