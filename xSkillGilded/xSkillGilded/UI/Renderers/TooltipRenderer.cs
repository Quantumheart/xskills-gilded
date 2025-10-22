using System;
using System.Collections.Generic;
using Vintagestory.API.Config;
using XLib.XLeveling;
using xSkillGilded.Managers;
using xSkillGilded.Models;
using xSkillGilded.Utilities;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded.UI.Renderers;

internal static class TooltipRenderer
{
    public static void Draw(
        SkillPageManager pageManager,
        float padd,
        float sky,
        float skh,
        int windowWidth,
        int windowHeight,
        float tooltipWidth,
        AbilityButton hoveringButton,
        TooltipObject hoveringTooltip,
        ref string currentTooltip,
        ref List<VTMLblock> tooltipVTML)
    {
        var tooltipX = windowWidth - tooltipWidth - padd;
        var tooltipY = sky + skh + _ui(32);
        var tooltipW = tooltipWidth - padd;
        var tooltipH = windowHeight - tooltipY - padd;

        drawImage(Sprite("elements", "tooltip_sep_v"), tooltipX - _ui(16), tooltipY, 2, tooltipH);

        if (hoveringTooltip != null)
        {
            tooltipY += fTitleGold.getLineHeight();
            drawTextFont(fTitleGold, hoveringTooltip.Title, tooltipX + _ui(8), tooltipY, HALIGN.Left, VALIGN.Bottom);

            tooltipY += _ui(2);
            drawProgressBar(0, tooltipX, tooltipY, tooltipW, _ui(4), c_dkgrey, c_lime);
            tooltipY += _ui(12);

            if (currentTooltip != hoveringTooltip.Description)
            {
                tooltipVTML = VTML.parseVTML(hoveringTooltip.Description);
                currentTooltip = hoveringTooltip.Description;
            }

            var h = drawTextVTML(tooltipVTML, tooltipX + _ui(8), tooltipY, tooltipW - _ui(16));
        }
        else if (hoveringButton != null)
        {
            var ability = hoveringButton.Ability;

            var name = ability.Ability.DisplayName;
            var skillName = ability.Ability.Skill.DisplayName;
            var tier = ability.Tier;
            var tierMax = ability.Ability.MaxTier;
            var tierText = "Lv. " + tier + "/" + tierMax;

            tooltipY += fTitleGold.getLineHeight();
            drawTextFont(fTitleGold, name, tooltipX + _ui(8), tooltipY, HALIGN.Left, VALIGN.Bottom);
            drawTextFont(fSubtitle, tierText, tooltipX + tooltipW - _ui(8), tooltipY, HALIGN.Right, VALIGN.Bottom);

            tooltipY += _ui(2);
            drawProgressBar((float)tier / tierMax, tooltipX, tooltipY, tooltipW, _ui(4), c_dkgrey,
                tier == tierMax ? c_gold : c_lime);
            tooltipY += _ui(12);

            var descCurrTier = AbilityFormatter.FormatAbilityDescription(ability.Ability, tier);
            if (currentTooltip != descCurrTier)
            {
                tooltipVTML = VTML.parseVTML(descCurrTier);
                currentTooltip = descCurrTier;
            }

            var h = drawTextVTML(tooltipVTML, tooltipX + _ui(8), tooltipY, tooltipW - _ui(16));
            tooltipY += Math.Max(h + _ui(16), _ui(160));

            drawSetColor(new System.Numerics.Vector4(104 / 255f, 76 / 255f, 60 / 255f, 1));
            drawImage(Sprite("elements", "tooltip_sep"), tooltipX + _ui(8), tooltipY, tooltipW - _ui(16), 1);
            drawSetColor(c_white);
            tooltipY += _ui(16);

            if (tier < tierMax)
            {
                var requiredLevel = ability.Ability.RequiredLevel(tier + 1);
                var reqText = string.Format(Lang.GetUnformatted("xskillgilded:abilityLevelRequired"), skillName,
                    requiredLevel);

                drawSetColor(pageManager.CurrentPlayerSkill.Level >= requiredLevel ? c_lime : c_red);
                drawTextFont(fSubtitle, reqText, tooltipX + _ui(8), tooltipY);
                drawSetColor(c_white);
                tooltipY += fSubtitle.getLineHeight() + _ui(4);

                var requirements = ability.Ability.Requirements;
                foreach (var req in requirements)
                {
                    if (req.MinimumTier > tier + 1) continue;
                    reqText = req.ShortDescription(ability);

                    if (reqText == null || reqText.Length == 0) continue;
                    var reqLines = reqText.Split('\n');

                    var isFulfilled = req.IsFulfilled(ability, ability.Tier + 1);
                    drawSetColor(isFulfilled ? c_lime : c_red);

                    var exReq = req as ExclusiveAbilityRequirement;
                    if (exReq != null)
                        drawSetColor(isFulfilled ? c_grey : c_red);

                    foreach (var reqLine in reqLines)
                    {
                        if (reqLine.Length == 0) continue;
                        drawTextFont(fSubtitle, reqLine, tooltipX + _ui(8), tooltipY);
                        tooltipY += fSubtitle.getLineHeight() + _ui(2);
                    }

                    drawSetColor(c_white);

                    tooltipY += _ui(4);
                }
            }

            var actX = windowWidth - padd - _ui(16);
            var actY = windowHeight - padd - _ui(8);

            drawSetColor(c_grey);
            var _mouseRsize = drawTextFont(fSubtitle, Lang.GetUnformatted("xskillgilded:actionUnlearn"), actX, actY,
                HALIGN.Right, VALIGN.Bottom);
            drawImage(Sprite("elements", "mouse_right"), actX - _mouseRsize.X / 2 - _ui(64 / 2), actY - _ui(32 + 16),
                _ui(64), _ui(32));
            actX -= _mouseRsize.X + _ui(16);

            var _mouseLsize = drawTextFont(fSubtitle, Lang.GetUnformatted("xskillgilded:actionLearn"), actX, actY,
                HALIGN.Right, VALIGN.Bottom);
            drawImage(Sprite("elements", "mouse_left"), actX - _mouseLsize.X / 2 - _ui(64 / 2), actY - _ui(32 + 16),
                _ui(64), _ui(32));
            actX -= _mouseLsize.X + _ui(16);
            drawSetColor(c_white);
        }
    }
}
