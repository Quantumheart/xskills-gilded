using System;
using Vintagestory.API.Config;
using xSkillGilded.Managers;
using xSkillGilded.Models;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded.UI.Renderers;

internal static class SkillDescriptionRenderer
{
    public static void Draw(SkillPageManager pageManager, float padd, float sky, float skh, ref TooltipObject hoveringTooltip)
    {
        var sdx = padd + _ui(16);
        var sdy = sky + skh + _ui(16);
        var sdw = _ui(200);

        if (pageManager.Page == "_Specialize")
        {
            var skillTitle = Lang.GetUnformatted("xlib:specialisations");
            var skillTitle_size = drawTextFont(fTitleGold, skillTitle, sdx, sdy);
            sdy += fTitleGold.getLineHeight() + _ui(8);

            foreach (var skill in pageManager.AllSkills)
            {
                var hh = UIHelpers.DrawSkillLevelDetail(skill, pageManager, sdx, sdy, sdw, false, ref hoveringTooltip);
                sdy += hh;
            }
        }
        else
        {
            var hh = UIHelpers.DrawSkillLevelDetail(pageManager.CurrentPlayerSkill, pageManager, sdx, sdy, sdw, true, ref hoveringTooltip);
            sdy += hh;

            var unlearnPoint = pageManager.CurrentPlayerSkill.PlayerSkillSet.UnlearnPoints;
            float unlearnPointReq = pageManager.XLevelingClient.GetPointsForUnlearn();
            var unlearnAmount = (float)Math.Floor(unlearnPoint / unlearnPointReq);
            var unlearnProgress = unlearnPoint / unlearnPointReq - unlearnAmount;
            var unx = sdx + sdw - _ui(8);
            var uny = sdy;

            drawSetColor(c_red);
            drawTextFont(fSubtitle, Lang.GetUnformatted("xlib:unlearnpoints"), sdx, sdy);

            if (unlearnAmount > 0)
            {
                var unlearnPoint_size = fSubtitle.CalcTextSize(unlearnAmount.ToString());
                drawSetColor(c_red, .3f);
                drawImage9patch(Sprite("elements", "glow"), unx - unlearnPoint_size.X - 16, sdy - 12,
                    unlearnPoint_size.X + 32, unlearnPoint_size.Y + 24, 15);
                drawSetColor(c_white);
            }

            drawTextFont(fSubtitle, unlearnAmount.ToString(), unx, sdy, HALIGN.Right);

            sdy += fSubtitle.getLineHeight();
            drawProgressBar(unlearnProgress, sdx, sdy, sdw, _ui(4), c_dkgrey, c_red);
            sdy += _ui(4);

            var unlearnCooldown = pageManager.CurrentPlayerSkill.PlayerSkillSet.UnlearnCooldown;
            var unlearnCooldownMax = pageManager.XLevelingClient.Config.unlearnCooldown;
            if (unlearnCooldown > 0)
            {
                drawSetColor(c_grey);
                drawTextFont(fSubtitle, "Cooldown", sdx, sdy);
                drawTextFont(fSubtitle, FormatTime((float)Math.Round(unlearnCooldown)), unx, sdy, HALIGN.Right);
                drawSetColor(c_white);
            }

            if (mouseHover(sdx, uny - 4, sdx + sdw, sdy + 4))
            {
                var desc = string.Format(Lang.GetUnformatted("xskillgilded:unlearnDesc"),
                    FormatTime(unlearnCooldownMax * 60f));
                hoveringTooltip = new TooltipObject(Lang.GetUnformatted("xskillgilded:unlearnTitle"), desc);
            }
        }
    }
}
