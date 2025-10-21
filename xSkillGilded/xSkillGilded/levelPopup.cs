using System;
using System.Numerics;
using ImGuiNET;
using Vintagestory.API.Client;
using VSImGui;
using VSImGui.API;
using XLib.XLeveling;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded;

public class LevelPopup
{
    private readonly ICoreClientAPI api;
    private readonly ImGuiModSystem imguiModSystem;
    private bool showing = true;
    private readonly PlayerSkill skill;

    private float timer;
    private readonly float windowHeight = _ui(160);

    private readonly float windowWidth = _ui(560);

    public LevelPopup(ICoreClientAPI api, PlayerSkill skill)
    {
        this.api = api;
        this.skill = skill;

        imguiModSystem = api.ModLoader.GetModSystem<ImGuiModSystem>();
        imguiModSystem.Draw += Draw;
        imguiModSystem.Closed += Close;
    }

    public CallbackGUIStatus Draw(float deltaSecnds)
    {
        if (!showing) return CallbackGUIStatus.DontGrabMouse;

        var window = api.Gui.WindowBounds;
        var screenWidth = (float)window.OuterWidth;
        var screenHeight = (float)window.OuterHeight;

        var wx = screenWidth / 2 - windowWidth / 2;
        var wy = _ui(8);

        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight));
        ImGui.SetNextWindowPos(new Vector2(wx, wy));
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

        ImGui.Begin("effectBox", flags);
        drawSetColor(c_dkgrey, invLerp2(timer, 0f, 1f, 3.5f, 4f));
        drawImage(Sprite("elements", "level_up_glow"), 0, 0, windowWidth, windowHeight);
        drawSetColor(c_white, invLerp2(timer, 0f, .5f, 3.5f, 4f));
        var ww = invLerp(timer, 0f, .75f) * (windowWidth - _ui(80));
        drawImage(Sprite("elements", "level_sep"), windowWidth / 2 - ww / 2, windowHeight / 2 - _ui(64), ww, _ui(64));
        drawSetColor(c_white);

        var skillIcon = Sprite("skillicon", skill.Skill.Name);
        if (skillIcon.TextureId != 0)
        {
            drawSetColor(c_dkgrey, invLerp2(timer, 0f, 1f, 3.5f, 4f));
            drawImage(Sprite("elements", "level_up_glow"), windowWidth / 2 - _ui(40), windowHeight / 2 - _ui(40),
                _ui(80), _ui(80));
            drawSetColor(c_gold, invLerp2(timer, 0f, 1f, 3.5f, 4f));
            drawImage(skillIcon, windowWidth / 2 - _ui(16), windowHeight / 2 - _ui(16), _ui(32), _ui(32));
            drawSetColor(c_white);
        }

        var lvUpText = $"{skill.Skill.DisplayName} Level up";
        drawSetColor(c_white, invLerp2(timer, 0f, .3f, 3.5f, 4f));
        var lvUpText_s = drawTextFont(fTitleGold, lvUpText, windowWidth / 2, windowHeight / 2 - _ui(16), HALIGN.Center,
            VALIGN.Bottom);
        drawSetColor(c_white);

        var hk = api.Input.GetHotKeyByCode("xSkillGilded");
        var hotkeyText = $"Press {hk.CurrentMapping} to open skill tree.";
        drawSetColor(c_white, invLerp2(timer, .3f, .6f, 3.5f, 4f) * .8f);
        var hotkeyText_s = drawTextFont(fSubtitle, hotkeyText, windowWidth / 2, windowHeight / 2 + _ui(16),
            HALIGN.Center);
        drawSetColor(c_white);

        ImGui.End();

        timer += deltaSecnds;
        if (timer >= 4f) showing = false; // imguiModSystem.Draw -= Draw;

        return CallbackGUIStatus.DontGrabMouse;
    }

    private float smoothstep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private float invLerp(float time, float from, float to)
    {
        var a = Math.Clamp((time - from) / (to - from), 0f, 1f);
        return smoothstep(a);
    }

    private float invLerp2(float time, float from0, float to0, float from1, float to1)
    {
        var a = Math.Min(Math.Clamp((time - from0) / (to0 - from0), 0f, 1f),
            1f - Math.Clamp((time - from1) / (to1 - from1), 0f, 1f));
        return smoothstep(a);
    }

    private void Close()
    {
    }
}