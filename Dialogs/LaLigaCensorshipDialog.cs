using System;
using Imui.Controls;
using Imui.Core;
using Imui.Style;
using UnityEngine;
using ZeepSDK.Controls;
using ZeepSDK.UI;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Dialogs;

internal sealed class LaLigaCensorshipDialog : IZeepGUIDrawer, IDisposable
{
    public const string MoreInformationUrl = "https://hayahora.futbol";

    private readonly Action<bool> _onDecision;
    private readonly DisposableBag _inputOverride;
    private bool _active = true;
    private bool _useAlternativeDomains;

    public LaLigaCensorshipDialog(bool useAlternativeDomains, Action<bool> onDecision)
    {
        _useAlternativeDomains = useAlternativeDomains;
        _onDecision = onDecision ?? throw new ArgumentNullException(nameof(onDecision));
        _inputOverride = ControlsApi.DisableAllInputExceptEventSystem(() => _active);
    }

    public void OnZeepGUI(ImGui gui)
    {
        if (!_active)
            return;

        uint id = gui.PushId("LaLigaCensorshipPopup");
        try
        {
            gui.BeginPopup();
            ImRect rect = ImWindow.GetInitialWindowRect(gui, new ImSize(920, 650));

            gui.Canvas.PushClipRect(rect);
            gui.Canvas.PushRectMask(rect, 0);
            gui.Box(rect, gui.Style.Window.Box);
            gui.RegisterControl(id, rect);
            gui.RegisterGroup(id, rect);
            gui.Layout.Push(ImAxis.Vertical, rect.WithPadding(24));

            gui.Text("Connection issues in Spain", new ImTextSettings(36, 0.5f));
            gui.AddSpacing(10);
            DrawParagraph(gui,
                "During La Liga matches, Spanish internet providers block Cloudflare IP addresses at La Liga's " +
                "request. This goes beyond the websites covered by the court orders and blocks unrelated services " +
                "using Cloudflare infrastructure, including ZeepCentraal.");
            gui.AddSpacing(10);
            DrawParagraph(gui,
                "If you're affected, GTR may be unable to connect to ZeepCentraal, submit records, download ghosts " +
                "or use other online features.");
            gui.AddSpacing(10);
            DrawParagraph(gui,
                "You can use alternative ZeepCentraal domains that don't go through Cloudflare to avoid these blocks.");
            gui.AddSpacing(14);

            _useAlternativeDomains = gui.Checkbox(
                _useAlternativeDomains,
                "Use alternative domains in Spain");

            gui.AddSpacing(14);
            DrawParagraph(gui,
                "Please only enable this if you're having connection problems due to ISP blocking. You can turn it " +
                "off or on at any time in the GTR mod settings under \"Other -> 5. URLs\".");
            gui.AddSpacing(10);
            DrawParagraph(gui,
                "For more information about La Liga's censorship and how it affects internet access in Spain, visit:");
            gui.AddSpacing(6);

            if (gui.Button(MoreInformationUrl, new ImSize(320, 42)))
                Application.OpenURL(MoreInformationUrl);

            const float buttonWidth = 220;
            const float buttonHeight = 52;
            gui.AddSpacing(Mathf.Max(12, gui.GetLayoutHeight() - buttonHeight));

            using (gui.Horizontal(gui.GetLayoutWidth(), buttonHeight))
            {
                gui.AddSpacing(Mathf.Max(0, gui.GetLayoutWidth() - buttonWidth));
                if (gui.Button("Continue", new ImSize(buttonWidth, buttonHeight)))
                    Decide();
            }

            gui.Layout.Pop();
            gui.Canvas.PopClipRect();
            gui.Canvas.PopClipRect();
            gui.EndPopup();
        }
        finally
        {
            gui.PopId();
        }
    }

    public void Dispose()
    {
        if (!_active)
            return;

        _active = false;
        _inputOverride.Dispose();
    }

    private static void DrawParagraph(ImGui gui, string text)
    {
        gui.Text(text, new ImTextSettings(18, 0f, 0f, true));
    }

    private void Decide()
    {
        if (!_active)
            return;

        _active = false;
        _inputOverride.Dispose();
        _onDecision(_useAlternativeDomains);
    }
}
