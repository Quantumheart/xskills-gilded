using System;
using System.Numerics;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using xSkillGilded.Managers;
using xSkillGilded.Models;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded.UI.Renderers;

internal static class AbilityRenderer
{
    public static AbilityButton Draw(
        SkillPageManager pageManager,
        ICoreClientAPI api,
        float padd,
        float sky,
        float skh,
        float contentWidth,
        int windowHeight,
        float deltaTime,
        ImGuiWindowFlags flags,
        AbilityButton currentHoveringButton)
    {
        var abx = padd;
        var aby = sky + skh + _ui(8);
        var abw = contentWidth - abx - _ui(8);
        var abh = windowHeight - aby - _ui(8);
        var bw = _ui(128); // buttonWidth
        var bh = _ui(100); // buttonHeight

        var padX = Math.Max(0, _ui(pageManager.AbilityPageWidth) - abw + _ui(128));
        var padY = Math.Max(0, _ui(pageManager.AbilityPageHeight) - abh + _ui(128));

        var mx = ImGui.GetMousePos().X;
        var my = ImGui.GetMousePos().Y;

        float savedWindowPosX = windowPosX;
        float savedWindowPosY = windowPosY;
        
        var mrx = (mx - (savedWindowPosX + abx)) / abw - .5f;
        var mry = (my - (savedWindowPosY + aby)) / abh - .5f;

        var ofmx = (float)Math.Round(-padX * mrx);
        var ofmy = (float)Math.Round(-padY * mry);

        windowPosX = savedWindowPosX + abx; // windowX
        windowPosY = savedWindowPosY + aby; // windowY
        ImGui.SetCursorPos(new Vector2(abx, aby));
        ImGui.BeginChild("Ability", new Vector2(abw, abh), false, flags);
        var offx = ofmx + abw / 2;
        var offy = ofmy + abh / 2;
        AbilityButton _hoveringButton = null;

        var lvx = _ui(64);

        for (var i = 1; i < pageManager.LevelRequirementBars.Count; i++)
        {
            var lv = pageManager.LevelRequirementBars[i];
            var _y = offy + _ui(pageManager.AbilityPageHeight / 2 - i * (100 + 16) + 16 / 2); // buttonHeight, buttonPad

            if (mouseHover(lvx, _y - 100 - 16, lvx + abw, _y)) // buttonHeight, buttonPad
                drawSetColor(new Vector4(239 / 255f, 183 / 255f, 117 / 255f, 1));
            else
                drawSetColor(new Vector4(104 / 255f, 76 / 255f, 60 / 255f, 1));

            var lvReqText = $"Level {lv}";
            drawImage(Sprite("elements", "level_sep"), lvx, _y - _ui(64), abw - _ui(128), _ui(64));
            drawTextFont(fSubtitle, lvReqText, lvx + _ui(32), _y - _ui(2), HALIGN.Left, VALIGN.Bottom);
        }

        drawSetColor(c_white);

        foreach (var line in pageManager.DecorationLines)
        {
            drawSetColor(line.color);

            if (line.y0 == line.y1)
            {
                var _x0 = offx + _ui(Math.Min(line.x0, line.x1)) + bw;
                var _x1 = offx + _ui(Math.Max(line.x0, line.x1));

                drawImage(Sprite("elements", "pixel"), _x0, offy + _ui(line.y0 + 100 / 2 - 10), _x1 - _x0, _ui(20)); // buttonHeight
            }
        }

        drawSetColor(c_white);

        foreach (var button in pageManager.AbilityButtons.Values)
        {
            var bx = _ui(button.x) + offx;
            var by = _ui(button.y) + offy;
            var buttonSpr = "abilitybox_frame_inactive";
            var color = c_grey;

            var ability = button.Ability;
            var texture = button.Texture;
            var tier = ability.Tier;

            if (tier > 0) color = c_white;

            var abilityName = button.Ability.Ability.DisplayName;
            var reqFulfiled = ability.RequirementsFulfilled(tier + 1);

            if (reqFulfiled)
            {
                color = c_lime;
                buttonSpr = "abilitybox_frame_active";
            }

            if (tier == ability.Ability.MaxTier)
            {
                color = c_gold;
                buttonSpr = "abilitybox_frame_max";
            }

            if (mouseHover(bx, by, bx + bw, by + bh))
            {
                _hoveringButton = button;

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    ability.SetTier(ability.Tier + 1);
                    if (ability.Tier > tier)
                    {
                        button.glowAlpha = 1;

                        if (ability.Tier == ability.Ability.MaxTier)
                            api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/upgradedmax.ogg"), false, .3f);
                        else
                            api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/upgraded.ogg"), false, .3f);
                    }
                }

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    ability.SetTier(ability.Tier - 1);

                    if (ability.Tier < tier)
                        api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/downgraded.ogg"), false, .3f);
                }
            }

            if (button.glowAlpha > 0)
            {
                var glow_size = _ui(256);
                drawSetColor(tier == ability.Ability.MaxTier ? c_gold : c_lime, button.glowAlpha);
                drawImage(Sprite("elements", "ability_glow"), bx + bw / 2 - glow_size / 2, by + bh / 2 - glow_size / 2,
                    glow_size, glow_size);
                drawSetColor(c_white);
            }

            button.glowAlpha = lerpTo(button.glowAlpha, 0, .2f, deltaTime);
            button.drawColor = color;
            drawImage(Sprite("elements", "abilitybox_bg"), bx, by, bw, bh);
            if (ability.Tier == 0 && !reqFulfiled)
                drawSetColor(new Vector4(1, 1, 1, .25f));
            if (texture != null) drawImageFitOverflow(texture, bx, by, bw, bh, .75f);
            drawSetColor(c_white);
            drawImage9patch(Sprite("elements", "ability_shadow"), bx, by, bw, bh, 30);

            var _nameSize = fSubtitle.CalcTextSize(abilityName);
            var bgh = _nameSize.X > bw - _ui(8) ? bh : _ui(48);
            drawImage(Sprite("elements", "abilitybox_name_under"), bx, by + bh - bgh, bw, bgh);
            drawSetColor(color);
            if (_nameSize.X > bw - _ui(8))
                drawTextFontWrap(fSubtitle, abilityName, bx + bw / 2, by + bh - _ui(12), HALIGN.Center, VALIGN.Bottom,
                    bw - _ui(8));
            else
                drawTextFont(fSubtitle, abilityName, bx + bw / 2, by + bh - _ui(12), HALIGN.Center, VALIGN.Bottom);
            drawSetColor(c_white);

            var progress = ability.Tier / (float)ability.Ability.MaxTier;
            var prh = _ui(6);
            var prw = bw / ability.Ability.MaxTier;
            var prx = bx;
            var pry = by + bh - _ui(2) - prh;

            for (var i = 0; i < ability.Ability.MaxTier; i++)
                drawImage9patch(Sprite("elements", "abilitybox_progerss_bg"), prx + i * prw, pry, prw, prh, 2);

            var tierWidth = ability.Tier * prw;
            button.drawTierWidth = lerpTo(button.drawTierWidth, tierWidth, .85f, deltaTime);
            if (button.drawTierWidth > 0)
                drawImage9patch(Sprite("elements", "abilitybox_progerss_content"), prx, pry, button.drawTierWidth, prh,
                    2);

            for (var i = 0; i < ability.Ability.MaxTier - 1; i++)
                drawImage9patch(Sprite("elements", "abilitybox_progerss_overlay"), prx + i * prw, pry, prw + 1, prh, 2);

            drawImage9patch(Sprite("elements", buttonSpr), bx, by, bw, bh, 15);
        }

        if (_hoveringButton != null && currentHoveringButton != _hoveringButton)
            api.Gui.PlaySound("tick", false, .5f);

        if (_hoveringButton != null)
        {
            var ability = _hoveringButton.Ability;
            var bx = _ui(_hoveringButton.x) + offx;
            var by = _ui(_hoveringButton.y) + offy;
            var c = _hoveringButton.drawColor;

            drawSetColor(new Vector4(c.X, c.Y, c.Z, .5f));
            drawImage9patch(Sprite("elements", "abilitybox_frame_selected"), bx - 16, by - 16, bw + 32, bh + 32, 30);
            drawSetColor(c_white);

            var requirements = ability.Ability.Requirements;
            foreach (var req in requirements)
                UIHelpers.DrawRequirementHighlight(_hoveringButton, req, pageManager, offx, offy);
        }

        ImGui.EndChild();
        windowPosX = savedWindowPosX; // windowX
        windowPosY = savedWindowPosY; // windowY

        return _hoveringButton;
    }
}
