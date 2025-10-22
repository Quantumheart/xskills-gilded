using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Vintagestory.API.Client;
using xSkillGilded.Managers;
using xSkillGilded.Models;
using xSkillGilded.UI.Renderers;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded.UI;

internal class SkillUIRenderer
{
    private readonly ICoreClientAPI api;
    private readonly SkillPageManager pageManager;
    private readonly float tooltipWidth = 400;

    private AbilityButton hoveringButton;
    private string hoveringID;
    private TooltipObject hoveringTooltip;
    private string currentTooltip = "";
    private List<VTMLblock> tooltipVTML;

    public SkillUIRenderer(ICoreClientAPI api, SkillPageManager pageManager, ImGuiViewportPtr viewPort)
    {
        this.api = api;
        this.pageManager = pageManager;
        tooltipVTML = new List<VTMLblock>();
    }

    public void Draw(int windowWidth, int windowHeight, float deltaTime, ImGuiWindowFlags flags, System.Action<bool> onSparringToggle)
    {
        var padd = _ui(16); // contentPadding
        var contentWidth = windowWidth - _ui(tooltipWidth) - padd * 2;

        string _hoveringID = null;

        var bty = SkillGroupTabRenderer.Draw(pageManager, api, padd, windowWidth, ref _hoveringID);
        var bth = _ui(32);

        var sky = SkillsTabRenderer.Draw(pageManager, api, padd, bty, bth, windowWidth, ref _hoveringID);
        var skh = _ui(32);

        var newHoveringButton = AbilityRenderer.Draw(
            pageManager,
            api,
            padd,
            sky,
            skh,
            contentWidth,
            windowHeight,
            deltaTime,
            flags,
            hoveringButton);

        hoveringButton = newHoveringButton;

        SkillDescriptionRenderer.Draw(pageManager, padd, sky, skh, ref hoveringTooltip);

        SkillActionsRenderer.Draw(pageManager, api, padd, windowHeight, ref _hoveringID, ref hoveringTooltip, onSparringToggle);

        TooltipRenderer.Draw(
            pageManager,
            padd,
            sky,
            skh,
            windowWidth,
            windowHeight,
            tooltipWidth,
            hoveringButton,
            hoveringTooltip,
            ref currentTooltip,
            ref tooltipVTML);

        hoveringID = _hoveringID;
        hoveringTooltip = null;
    }
}
