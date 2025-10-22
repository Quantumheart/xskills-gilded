using System.Numerics;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using xSkillGilded.Managers;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded.UI.Renderers;

internal static class SkillsTabRenderer
{
    public static float Draw(SkillPageManager pageManager, ICoreClientAPI api, float padd, float bty, float bth, int windowWidth, ref string hoveringID)
    {
        var skx = padd;
        var sky = bty + bth + _ui(4);
        var skw = (windowWidth - padd * 2) / pageManager.CurrentSkills.Count;
        var skh = _ui(32);

        if (!pageManager.MetaPage)
            for (var i = 0; i < pageManager.CurrentSkills.Count; i++)
            {
                var skill = pageManager.CurrentSkills[i];
                var skillName = skill.Skill.DisplayName;
                var skxc = skx + skw / 2;
                var skww = skw * .5f / 2;
                var color = new Vector4(1, 1, 1, 1);
                var _fTitle = fSubtitle;

                if (i != pageManager.SkillPage)
                {
                    if (mouseHover(skx, sky, skx + skw, sky + skh))
                    {
                        hoveringID = skillName;
                        drawImage(Sprite("elements", "tab_sep_hover"), skxc - skww, sky + skh - 4, skww * 2, 4);
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            pageManager.SetSkillPage(i);
                            api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/pagesub.ogg"), false, .3f);
                        }
                    }
                    else
                    {
                        drawImage(Sprite("elements", "tab_sep"), skxc - skww, sky + skh - 4, skww * 2, 4);
                        color.W = .5f;
                    }
                }
                else
                {
                    drawImage(Sprite("elements", "tab_sep_selected"), skxc - skww, sky + skh - 4, skww * 2, 4);
                    _fTitle = fSubtitleGold;
                }

                drawSetColor(color);
                var skillName_size = drawTextFont(_fTitle, skillName, skx + skw / 2, sky + skh / 2, HALIGN.Center,
                    VALIGN.Center);
                drawSetColor(c_white);

                float points = skill.AbilityPoints;
                if (points > 0)
                {
                    var _pax = skxc + skillName_size.X / 2 + _ui(20);
                    var _pay = sky + skh / 2;

                    var pointsText = points.ToString();
                    var pointsText_size = fSubtitle.CalcTextSize(pointsText);
                    drawSetColor(c_lime, .3f);
                    drawImage9patch(Sprite("elements", "glow"), _pax - 16, _pay - pointsText_size.Y / 2 - 12,
                        pointsText_size.X + 32, pointsText_size.Y + 24, 15);
                    drawSetColor(c_white);
                    drawTextFont(fSubtitle, pointsText, _pax, _pay, HALIGN.Left, VALIGN.Center);
                }

                skx += skw;
            }

        return sky + skh;
    }
}
