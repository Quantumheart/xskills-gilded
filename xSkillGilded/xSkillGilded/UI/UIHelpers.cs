using System;
using System.Numerics;
using Vintagestory.API.Config;
using XLib.XLeveling;
using xSkillGilded.Managers;
using xSkillGilded.Models;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded.UI;

internal static class UIHelpers
{
    public static float DrawSkillLevelDetail(PlayerSkill skill, SkillPageManager pageManager, float x, float y, float w, bool title, ref TooltipObject hoveringTooltip)
    {
        var ys = y;
        var sx = x;

        var skillTitle = skill.Skill.DisplayName;

        var skillTitle_size = drawTextFont(title ? fTitleGold : fSubtitleGold, skillTitle, sx, y);

        if (!title)
        {
            var abilityPoint = skill.AbilityPoints;
            var skillPointTitle = abilityPoint.ToString();

            var unlearnPoint = pageManager.CurrentPlayerSkill.PlayerSkillSet.UnlearnPoints;
            float unlearnPointReq = pageManager.XLevelingClient.GetPointsForUnlearn();
            var unlearnAmount = (float)Math.Floor(unlearnPoint / unlearnPointReq);
            var unlearnPointTitle = unlearnAmount.ToString();

            var _sx = x + w - _ui(8);
            Vector2 _s;

            drawSetColor(c_red);
            _s = drawTextFont(fSubtitle, unlearnPointTitle, _sx, y, HALIGN.Right);
            _sx -= _s.X;

            drawSetColor(c_grey);
            _s = drawTextFont(fSubtitle, "/", _sx, y, HALIGN.Right);
            _sx -= _s.X;

            drawSetColor(c_lime);
            _s = drawTextFont(fSubtitle, skillPointTitle, _sx, y, HALIGN.Right);
            drawSetColor(c_white);
        }

        y += skillTitle_size.Y + _ui(title ? 4 : 0);

        var skillLvTitle = "Lv." + skill.Level;
        var skillLvTitle_size = drawTextFont(fSubtitle, skillLvTitle, x, y);

        var currXp = (float)Math.Round(skill.Experience);
        var nextXp = (float)Math.Round(skill.RequiredExperience);
        var xpProgress = currXp / nextXp;

        drawSetColor(c_grey);
        drawTextFont(fSubtitle, $"{currXp}/{nextXp} xp", x + w - _ui(8), y, HALIGN.Right);
        drawSetColor(c_white);

        var expBonus = skill.Skill.GetExperienceMultiplier(skill.PlayerSkillSet, false) - 1f;
        if (expBonus != 0f)
        {
            var bonusText = (expBonus > 0 ? "+" : "-") + Math.Round(expBonus * 100f) + "%";
            drawSetColor(expBonus > 0 ? c_lime : c_red);
            var bonusTextSize = drawTextFont(fSubtitle, bonusText, x + w, y);

            if (mouseHover(x + w - 4, y - 4, x + w + bonusTextSize.X + 4, y + bonusTextSize.Y + 4))
            {
                var totalBonus = skill.Skill.GetExperienceMultiplier(skill.PlayerSkillSet) - 1f;

                var desc = Lang.GetUnformatted("xskillgilded:expBonusDesc");
                var _bonusText = (expBonus > 0 ? "+" : "-") + Math.Round(expBonus * 100f) + "%%";
                var totalBonusText = (totalBonus > 0 ? "+" : "-") + Math.Round(totalBonus * 100f) + "%%";

                desc = string.Format(desc, VTML.WrapFont(_bonusText, expBonus > 0 ? "#7ac62f" : "#bf663f"),
                    VTML.WrapFont(totalBonusText, totalBonus > 0 ? "#7ac62f" : "#bf663f"));

                hoveringTooltip = new TooltipObject(Lang.GetUnformatted("xskillgilded:expBonusTitle"), desc);
            }
        }

        y += skillLvTitle_size.Y;
        drawProgressBar(xpProgress, x, y, w, _ui(4), c_dkgrey, c_lime);
        y += _ui(6);

        if (title)
        {
            var abilityPoint = skill.AbilityPoints;
            var skillPointTitle =
                string.Format(Lang.GetUnformatted("xskillgilded:pointsAvailable"), abilityPoint.ToString());
            if (abilityPoint > 0)
            {
                var skillPoint_size = fSubtitle.CalcTextSize(abilityPoint.ToString());
                drawSetColor(c_lime, .3f);
                drawImage9patch(Sprite("elements", "glow"), x - 16, y - 12, skillPoint_size.X + 32,
                    skillPoint_size.Y + 24, 15);
                drawSetColor(c_white);
            }

            drawTextFont(fSubtitle, skillPointTitle, x, y);
            y += fSubtitle.getLineHeight();
        }

        y += _ui(8);
        return y - ys;
    }

    public static void DrawRequirementHighlight(AbilityButton button, Requirement requirement, SkillPageManager pageManager, float offx, float offy)
    {
        var ability = button.Ability;
        var isFulfilled = requirement.IsFulfilled(ability, ability.Tier + 1);

        var bx = _ui(button.x) + offx;
        var by = _ui(button.y) + offy;
        var bw = _ui(128); // buttonWidth
        var bh = _ui(100); // buttonHeight

        var abilityRequirement = requirement as AbilityRequirement;
        if (abilityRequirement != null)
        {
            var name = abilityRequirement.Ability.Name;
            if (pageManager.AbilityButtons.ContainsKey(name))
            {
                var _button = pageManager.AbilityButtons[name];

                var _bx = _ui(_button.x) + offx;
                var _by = _ui(_button.y) + offy;
                Vector4 _c = isFulfilled
                    ? new Vector4(c_lime.X, c_lime.Y, c_lime.Z, .5f)
                    : new Vector4(c_red.X, c_red.Y, c_red.Z, .9f);

                drawSetColor(_c);
                drawImage9patch(Sprite("elements", "abilitybox_frame_selected"), _bx - 16, _by - 16, bw + 32, bh + 32,
                    30);
                drawSetColor(c_white);
            }
        }

        var andRequirement = requirement as AndRequirement;
        if (andRequirement != null)
            foreach (var _req in andRequirement.Requirements)
                DrawRequirementHighlight(button, _req, pageManager, offx, offy);

        var orRequirement = requirement as OrRequirement;
        if (orRequirement != null)
            foreach (var _req in orRequirement.Requirements)
                DrawRequirementHighlight(button, _req, pageManager, offx, offy);

        var exclusiveAbilityRequirement = requirement as ExclusiveAbilityRequirement;
        if (exclusiveAbilityRequirement != null)
        {
            var name = exclusiveAbilityRequirement.Ability.Name;
            if (pageManager.AbilityButtons.ContainsKey(name))
            {
                var _button = pageManager.AbilityButtons[name];

                var _bx = _ui(_button.x) + offx;
                var _by = _ui(_button.y) + offy;

                drawSetColor(new Vector4(c_red.X, c_red.Y, c_red.Z, .9f));
                drawImage9patch(Sprite("elements", "abilitybox_frame_selected"), _bx - 16, _by - 16, bw + 32, bh + 32,
                    30);
                drawSetColor(c_white);
            }
        }
    }
}
