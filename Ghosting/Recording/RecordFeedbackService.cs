using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Steamworks;
using StrawberryShake;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.UI;
using ZeepSDK.Chat;
using ZeepSDK.Extensions;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public sealed class RecordFeedbackBaseline
{
    public string LevelKey { get; set; }
    public double? PersonalBestTime { get; set; }
    public int? PersonalBestPosition { get; set; }
    public double? WorldRecordTime { get; set; }
    public string WorldRecordSteamId { get; set; }
}

public sealed class RecordFeedbackService : IEagerService, IDisposable
{
    private readonly CurrentLevelRecordService _currentLevelRecordService;
    private readonly ConfigService _configService;
    private readonly IGtrClient _gtrClient;
    private readonly ILogger<RecordFeedbackService> _logger;

    private CancellationTokenSource _pendingCancellationTokenSource;
    private PendingFeedback _pending;
    private int _generation;

    public RecordFeedbackService(
        CurrentLevelRecordService currentLevelRecordService,
        ConfigService configService,
        IGtrClient gtrClient,
        ILogger<RecordFeedbackService> logger)
    {
        _currentLevelRecordService = currentLevelRecordService;
        _configService = configService;
        _gtrClient = gtrClient;
        _logger = logger;
        _currentLevelRecordService.SnapshotChanged += OnSnapshotChanged;
        RacingApi.LevelLoaded += CancelForLevelChange;
        RacingApi.Quit += CancelForLevelChange;
        MultiplayerApi.DisconnectedFromGame += CancelForLevelChange;
    }

    public RecordFeedbackBaseline CaptureBaseline()
    {
        CurrentLevelRecordSnapshot snapshot = _currentLevelRecordService.Snapshot;
        if (snapshot == null)
            return null;

        RecordFeedbackBaseline baseline = new()
        {
            LevelKey = snapshot.LevelKey,
            PersonalBestTime = snapshot.PersonalBest?.Time,
            PersonalBestPosition = snapshot.PersonalBest?.Rank,
            WorldRecordTime = snapshot.WorldRecord?.Time,
            WorldRecordSteamId = snapshot.WorldRecord?.SteamId
        };

        PendingFeedback pending = _pending;
        if (pending == null || !string.Equals(pending.Baseline.LevelKey, baseline.LevelKey, StringComparison.Ordinal))
            return baseline;

        if (!baseline.PersonalBestTime.HasValue || pending.SubmittedTime < baseline.PersonalBestTime.Value)
            baseline.PersonalBestTime = pending.SubmittedTime;

        if ((pending.Kind is RecordFeedbackKind.NewWorldRecord or RecordFeedbackKind.ImprovedWorldRecord) &&
            (!baseline.WorldRecordTime.HasValue || pending.SubmittedTime < baseline.WorldRecordTime.Value))
        {
            baseline.WorldRecordTime = pending.SubmittedTime;
            baseline.WorldRecordSteamId = SteamClient.SteamId.ToString();
        }

        return baseline;
    }

    public void HandleSuccessfulSubmission(double submittedTime, RecordFeedbackBaseline baseline)
    {
        if (baseline == null)
        {
            _logger.LogWarning("Skipping record feedback because no pre-submit snapshot was available");
            return;
        }

        RecordFeedbackKind kind = RecordFeedbackFormatter.Classify(
            submittedTime,
            baseline.PersonalBestTime,
            baseline.WorldRecordTime,
            baseline.WorldRecordSteamId,
            SteamClient.SteamId.ToString());
        if (kind == RecordFeedbackKind.None || !ShouldShow(kind))
            return;

        CancelPending();
        int generation = ++_generation;
        _pending = new PendingFeedback(baseline, submittedTime, kind, generation);
        _pendingCancellationTokenSource = new CancellationTokenSource();
        WaitForProjectionAsync(generation, _pendingCancellationTokenSource.Token).Forget();
        TryComplete(_currentLevelRecordService.Snapshot, false).Forget();
    }

    private bool ShouldShow(RecordFeedbackKind kind)
    {
        return RecordFeedbackFormatter.ShouldShow(
            kind,
            _configService.ShowPersonalBestImprovementMessages.Value,
            _configService.ShowPersonalBestBecameWorldRecordMessages.Value,
            _configService.ShowWorldRecordImprovementMessages.Value);
    }

    private void OnSnapshotChanged(CurrentLevelRecordSnapshot snapshot)
    {
        TryComplete(snapshot, false).Forget();
    }

    private async UniTaskVoid WaitForProjectionAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(45), cancellationToken: cancellationToken);
            if (generation != _generation)
                return;
            _currentLevelRecordService.Refresh();
            await UniTask.Delay(TimeSpan.FromSeconds(15), cancellationToken: cancellationToken);
            if (generation != _generation)
                return;
            await TryComplete(_currentLevelRecordService.Snapshot, true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTask TryComplete(CurrentLevelRecordSnapshot snapshot, bool allowPartial)
    {
        PendingFeedback pending = _pending;
        if (pending == null || pending.Generation != _generation)
            return;
        if (snapshot != null && !string.Equals(snapshot.LevelKey, pending.Baseline.LevelKey, StringComparison.Ordinal))
            return;

        PersonalBestHolder personalBest = snapshot?.PersonalBest;
        bool matchingPersonalBest = personalBest != null && Math.Abs(personalBest.Time - pending.SubmittedTime) < 0.001;
        bool projectionReady = matchingPersonalBest && personalBest.Rank.HasValue &&
                               personalBest.LevelDecayedPoints.HasValue &&
                               personalBest.PlayerDecayedPoints.HasValue;
        if (!projectionReady && !allowPartial)
            return;

        RecordFeedbackMessageData data = new()
        {
            Kind = pending.Kind,
            PreviousDelta = FormatPreviousDelta(pending),
            WasFirstPersonalBest = !pending.Baseline.PersonalBestTime.HasValue,
            PreviousPosition = pending.Baseline.PersonalBestPosition,
            Position = matchingPersonalBest ? personalBest.Rank : null,
            LevelDecayedPoints = matchingPersonalBest ? personalBest.LevelDecayedPoints : null,
            PlayerDecayedPoints = matchingPersonalBest ? personalBest.PlayerDecayedPoints : null
        };

        if (pending.Kind is RecordFeedbackKind.PersonalBest or RecordFeedbackKind.FirstPersonalBest)
            data.NextDelta = await GetNextDelta(pending);

        if (pending.Generation != _generation)
            return;

        _generation++;
        CancelPending();
        await UniTask.SwitchToMainThread();
        ChatApi.AddLocalMessage(RecordFeedbackFormatter.Format(data));
    }

    private async UniTask<string> GetNextDelta(PendingFeedback pending)
    {
        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable || !string.Equals(level.CacheKey, pending.Baseline.LevelKey, StringComparison.Ordinal))
            return null;

        try
        {
            IOperationResult<IGetNextFastestPersonalBestResult> result =
                await _gtrClient.GetNextFastestPersonalBest.ExecuteAsync(
                    level.XxHash,
                    level.Hash,
                    pending.SubmittedTime,
                    _pendingCancellationTokenSource?.Token ?? default);
            result.EnsureNoErrors();
            double? nextTime = result.Data?.Records?.Nodes.FirstOrDefault()?.Time;
            return nextTime.HasValue && pending.SubmittedTime > nextTime.Value
                ? (pending.SubmittedTime - nextTime.Value).GetFormattedTime()
                : null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to load next fastest PB for record feedback");
            return null;
        }
    }

    private static string FormatPreviousDelta(PendingFeedback pending)
    {
        double? previousTime = pending.Kind == RecordFeedbackKind.ImprovedWorldRecord
            ? pending.Baseline.WorldRecordTime
            : pending.Baseline.PersonalBestTime;
        return previousTime.HasValue && previousTime.Value > pending.SubmittedTime
            ? (previousTime.Value - pending.SubmittedTime).GetFormattedTime()
            : null;
    }

    private void CancelPending()
    {
        _pending = null;
        _pendingCancellationTokenSource?.Cancel();
        _pendingCancellationTokenSource?.Dispose();
        _pendingCancellationTokenSource = null;
    }

    private void CancelForLevelChange()
    {
        _generation++;
        CancelPending();
    }

    public void Dispose()
    {
        _currentLevelRecordService.SnapshotChanged -= OnSnapshotChanged;
        RacingApi.LevelLoaded -= CancelForLevelChange;
        RacingApi.Quit -= CancelForLevelChange;
        MultiplayerApi.DisconnectedFromGame -= CancelForLevelChange;
        CancelForLevelChange();
    }

    private sealed class PendingFeedback
    {
        public PendingFeedback(
            RecordFeedbackBaseline baseline,
            double submittedTime,
            RecordFeedbackKind kind,
            int generation)
        {
            Baseline = baseline;
            SubmittedTime = submittedTime;
            Kind = kind;
            Generation = generation;
        }

        public RecordFeedbackBaseline Baseline { get; }
        public double SubmittedTime { get; }
        public RecordFeedbackKind Kind { get; }
        public int Generation { get; }
    }
}
