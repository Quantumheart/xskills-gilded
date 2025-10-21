using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using VSImGui;
using VSImGui.API;
using XLib.XEffects;
using XLib.XLeveling;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded;

public class EffectBox : IRenderer
{
    private readonly ICoreClientAPI api;

    private string currentTooltip;
    private readonly ImGuiModSystem imguiModSystem;

    public Effect tooltip;
    private List<VTMLblock> tooltipVTML;
    private float windowHeight = _ui(240);

    private readonly float windowWidth = _ui(400);
    public XEffectsSystem xEffect;
    public XLeveling xLeveling;
    public XLevelingClient xLevelingClient;

    public EffectBox(ICoreClientAPI api)
    {
        this.api = api;
        api.Event.RegisterRenderer(this, EnumRenderStage.Ortho);

        imguiModSystem = api.ModLoader.GetModSystem<ImGuiModSystem>();
        imguiModSystem.Draw += Draw;
        imguiModSystem.Closed += Close;
    }

    public double RenderOrder => 1;
    public int RenderRange => 0;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        var config = xSkillGraphicalUI.config;
        if (!config.effectBoxEnabled) return;

        if (xLeveling == null || xLevelingClient == null) return;
        if (xEffect == null) xEffect = api.ModLoader.GetModSystem<XEffectsSystem>();

        var affected = api.World.Player.Entity.GetBehavior("Affected") as AffectedEntityBehavior;
        if (affected == null) return;

        var window = api.Gui.WindowBounds;
        var windowWidth = (float)window.OuterWidth;
        var windowHeight = (float)window.OuterHeight;

        var fxx = config.effectBoxOriginX;
        var fxy = config.effectBoxOriginY;
        var fxs = config.effectBoxSize;

        if (config.effectBoxOrientation == 2) fxx -= fxs;
        else if (config.effectBoxOrientation == 3) fxy -= fxs;

        float mx = api.Input.MouseX;
        float my = api.Input.MouseY;

        tooltip = null;

        foreach (var affectName in affected.Effects.Keys)
        {
            var effect = affected.Effects[affectName];

            api.Render.RenderTexture(Sprite("effecticon", affectName).TextureId, fxx, fxy, fxs, fxs);

            if (effect.Duration > 0)
            {
                var ratio = effect.Runtime / effect.Duration;
                api.Render.RenderTexture(Sprite("elements", "pixel").TextureId, fxx, fxy, fxs, fxs * ratio, 50,
                    new Vec4f(0, 0, 0, 0.5f));
            }

            api.Render.RenderTexture(Sprite("elements", "abilitybox_frame_idle").TextureId, fxx, fxy, fxs, fxs);

            var stack = effect.Stacks;
            var stackMax = effect.MaxStacks;

            if (stackMax > 1f)
            {
                var sts = _ui(6);
                var stx = fxx + fxs / 2 - (stackMax * sts + (stackMax - 1) * _ui(2)) / 2;
                var sty = fxy + fxs + _ui(3);

                for (float i = 0; i < stackMax; i++)
                {
                    var _stx = stx + i * (sts + _ui(2));
                    api.Render.RenderTexture(
                        Sprite("elements", i < stack ? "skill_stack_on" : "skill_stack_off").TextureId, _stx, sty, sts,
                        sts);
                }
            }

            if (pointInRectangle(mx, my, fxx, fxy, fxx + fxs, fxy + fxs))
                tooltip = effect;

            if (config.effectBoxOrientation == 0) fxx += fxs + _ui(8);
            else if (config.effectBoxOrientation == 1) fxy += fxs + _ui(8);
            else if (config.effectBoxOrientation == 2) fxx -= fxs + _ui(8);
            else if (config.effectBoxOrientation == 3) fxy -= fxs + _ui(8);
        }
    }

    public void Dispose()
    {
        api.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
    }

    public CallbackGUIStatus Draw(float deltaSecnds)
    {
        var config = xSkillGraphicalUI.config;

        if (!config.effectBoxEnabled) return CallbackGUIStatus.DontGrabMouse;
        if (tooltip == null) return CallbackGUIStatus.DontGrabMouse;

        var window = api.Gui.WindowBounds;
        var screenWidth = (float)window.OuterWidth;
        var screenHeight = (float)window.OuterHeight;

        var mousePos = ImGui.GetMousePos();
        var mx = mousePos.X;
        var my = mousePos.Y;
        var ww = windowWidth - _ui(48);
        var hh = _ui(240);

        var wx = Math.Clamp(mx + _ui(16), 0, screenWidth - windowWidth);
        var wy = Math.Clamp(my + _ui(32), 0, screenHeight - windowHeight);

        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight));
        ImGui.SetNextWindowPos(new Vector2(wx, wy));
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground;

        ImGui.Begin("effectBox", flags);
        drawImage(Sprite("elements", "bg"), 0, 0, windowWidth, windowHeight);

        var name = tooltip.GetName();

        var tx = _ui(24);
        var ty = _ui(28);

        var th = fTitleGold.getLineHeight();
        var s = drawTextFont(fTitleGold, name, tx, ty + th, HALIGN.Left, VALIGN.Bottom);
        ty += th;

        if (tooltip.Duration > 0)
        {
            var _time = FormatTime(tooltip.Duration - tooltip.Runtime) + "/" + FormatTime(tooltip.Duration);
            drawSetColor(c_white);
            drawTextFont(fSubtitle, _time, tx + ww, ty, HALIGN.Right, VALIGN.Bottom);
            ty += _ui(6);

            var ratio = 1 - tooltip.Runtime / tooltip.Duration;
            // drawProgressBar(ratio, tx, ty, ww, _ui(4), c_dkgrey, c_lime);
            drawImage9patch(Sprite("elements", "abilitybox_progerss_bg"), tx, ty, ww, _ui(4), 2);
            drawImage9patch(Sprite("elements", "abilitybox_progerss_content"), tx, ty, ww * ratio, _ui(4), 2);
            ty += _ui(10);
        }

        if (tooltip.MaxStacks > 1)
        {
            float ratio = tooltip.Stacks / tooltip.MaxStacks;
            // drawProgressBar(ratio, tx, ty, ww, _ui(4), c_dkgrey, c_lime);
            drawImage9patch(Sprite("elements", "abilitybox_progerss_bg"), tx, ty, ww, _ui(4), 2);
            drawImage9patch(Sprite("elements", "abilitybox_progerss_content_white"), tx, ty, ww * ratio, _ui(4), 2);
            ty += _ui(10);
        }

        if (tooltip.Duration == 0 && tooltip.MaxStacks == 0)
        {
            drawImage(Sprite("elements", "tooltip_sep"), tx, ty + _ui(4), ww, 1);
            ty += _ui(16);
        }

        var desc = tooltip.GetDescription().Replace("%", "%%");
        var h = drawTextWrap(desc, tx, ty, HALIGN.Left, VALIGN.Top, ww);
        ty += h + _ui(8);

        if (tooltip.MaxStacks > 1)
        {
            var mh = drawText(Lang.Get("xeffects:stacks") + ": " + tooltip.Stacks + "/" + tooltip.MaxStacks, tx, ty);
            ty += mh.Y + _ui(8);
        }

        if (tooltip.Interval > 0)
        {
            var mh = drawText(Lang.Get("xeffects:interval") + ": " + FormatTime(tooltip.Interval), tx, ty);
            ty += mh.Y + _ui(8);
        }

        if (tooltip is DiseaseEffect disease)
        {
            var _dis = disease.HealingRate != 0.0f
                ? Lang.Get("xeffects:healingrate") + ": " + string.Format("{0:0.00####}", disease.HealingRate * 60.0f)
                : "";
            var mh = drawText(_dis, tx, ty);
            ty += mh.Y + _ui(8);
        }

        drawImage9patch(Sprite("elements", "frame"), 0, 0, windowWidth, windowHeight, 60);
        ImGui.End();

        windowHeight = Math.Max(ty + _ui(24), _ui(240));

        return CallbackGUIStatus.DontGrabMouse;
    }

    private void Close()
    {
    }
}