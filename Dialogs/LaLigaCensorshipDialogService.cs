using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Patching.Patches;
using UnityEngine;
using ZeepSDK.Controls;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.Dialogs;

internal sealed class LaLigaCensorshipDialogService : IEagerService, IDisposable
{
    public const string CountryDetectionClientKey = "IP Country Detection";

    private const string NoticeSeenKey = "TNRD.Zeepkist.GTR.HasSeenLaLigaCensorshipNotice1";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConfigService _configService;
    private readonly ILogger<LaLigaCensorshipDialogService> _logger;
    private LaLigaCensorshipDialog _dialog;
    private bool _checking;
    private bool _disposed;

    public LaLigaCensorshipDialogService(
        IHttpClientFactory httpClientFactory,
        ConfigService configService,
        ILogger<LaLigaCensorshipDialogService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configService = configService;
        _logger = logger;
        MainMenuUi_Awake.Postfixed += OnMainMenuAwake;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        MainMenuUi_Awake.Postfixed -= OnMainMenuAwake;
        RemoveDialog();
    }

    private void OnMainMenuAwake()
    {
        if (_disposed || _checking || _dialog != null || HasSeenNotice())
            return;

        _checking = true;
        DetectAndShowAsync().Forget(exception =>
            _logger.LogError(exception, "Failed to show Spain connection dialog"));
    }

    private async UniTask DetectAndShowAsync()
    {
        try
        {
            if (_disposed || HasSeenNotice() || !await IsUserInSpainAsync())
                return;

            await UniTask.SwitchToMainThread();
            await UniTask.WaitUntil(() => _disposed || ControlsApi.MenuInputOverride.Value);

            if (_disposed || HasSeenNotice())
                return;

            _dialog = new LaLigaCensorshipDialog(
                _configService.UseAlternativeDomainsInSpain.Value,
                ApplyDecision);
            UIApi.AddZeepGUIDrawer(_dialog);
        }
        finally
        {
            _checking = false;
        }
    }

    private async UniTask<bool> IsUserInSpainAsync()
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient(CountryDetectionClientKey);
            using HttpResponseMessage response = await client.GetAsync("json");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "IP country detection failed with status {StatusCode}",
                    response.StatusCode);
                return false;
            }

            string content = await response.Content.ReadAsStringAsync();
            return SpainCountryDetection.IsSpanishResponse(content);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "IP country detection failed");
            return false;
        }
    }

    private void ApplyDecision(bool useAlternativeDomains)
    {
        PlayerPrefs.SetInt(NoticeSeenKey, 1);
        PlayerPrefs.Save();
        _configService.UseAlternativeDomainsInSpain.Value = useAlternativeDomains;
        RemoveDialog();
    }

    private void RemoveDialog()
    {
        if (_dialog == null)
            return;

        UIApi.RemoveZeepGUIDrawer(_dialog);
        _dialog.Dispose();
        _dialog = null;
    }

    private static bool HasSeenNotice() => PlayerPrefs.GetInt(NoticeSeenKey, 0) == 1;
}
