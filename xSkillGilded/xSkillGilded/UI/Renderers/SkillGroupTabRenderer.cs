using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using xSkillGilded.Managers;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded.UI.Renderers;

internal static class SkillGroupTabRenderer
{
    public static float Draw(SkillPageManager pageManager, ICoreClientAPI api, float padd, int windowWidth, ref string hoveringID)
    {
        var btx = padd;
        var bty = padd;
        var bth = _ui(32);

        var _btsw = _ui(96);
        var btxc = btx + _btsw / 2;
        var btww = _btsw * .5f / 2;
        var _alpha = 1f;

        if (pageManager.Page == "_Specialize")
        {
            drawImage(Sprite("elements", "tab_sep_selected"), btxc - btww, bty + bth - 4, btww * 2, 4);
            _alpha = 1f;
        }
        else if (mouseHover(btx, bty, btx + _btsw, bty + bth))
        {
            hoveringID = "_Specialize";
            drawImage(Sprite("elements", "tab_sep_hover"), btxc - btww, bty + bth - 4, btww * 2, 4);
            _alpha = 1f;
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                pageManager.SetPage("_Specialize");
                api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/page.ogg"), false, .3f);
            }
        }
        else
        {
            drawImage(Sprite("elements", "tab_sep"), btxc - btww, bty + bth - 4, btww * 2, 4);
            _alpha = .5f;
        }

        drawSetColor(c_white, _alpha);
        drawImage(pageManager.Page == "_Specialize" ? Sprite("elements", "meta_spec_selected") : Sprite("elements", "meta_spec"),
            btxc - _ui(24 / 2), bty + 4, _ui(24), _ui(24));
        drawSetColor(c_white);
        btx += _btsw;

        var btw = (windowWidth - padd - btx) / pageManager.SkillGroups.Count;

        foreach (var groupName in pageManager.SkillGroups.Keys)
        {
            btxc = btx + btw / 2;
            btww = btw * .5f / 2;
            var alpha = 1f;
            var _fTitle = fTitle;

            var points = 0;
            foreach (var skill in pageManager.SkillGroups[groupName]) points += skill.AbilityPoints;

            if (groupName == pageManager.Page)
            {
                drawImage(Sprite("elements", "tab_sep_selected"), btxc - btww, bty + bth - 4, btww * 2, 4);
                _fTitle = fTitleGold;
            }
            else if (mouseHover(btx, bty, btx + btw, bty + bth))
            {
                hoveringID = groupName;
                drawImage(Sprite("elements", "tab_sep_hover"), btxc - btww, bty + bth - 4, btww * 2, 4);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    pageManager.SetPage(groupName);
                    api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/page.ogg"), false, .3f);
                }
            }
            else
            {
                drawImage(Sprite("elements", "tab_sep"), btxc - btww, bty + bth - 4, btww * 2, 4);
                alpha = .5f;
            }

            drawSetColor(c_white, alpha);
            var skillName_size = drawTextFont(_fTitle, groupName, btx + btw / 2, bty + bth / 2, HALIGN.Center,
                VALIGN.Center);
            drawSetColor(c_white);

            if (points > 0)
            {
                var _pax = btx + btw / 2 + skillName_size.X / 2 + _ui(20);
                var _pay = bty + bth / 2;

                var pointsText = points.ToString();
                var pointsText_size = fSubtitle.CalcTextSize(pointsText);
                drawSetColor(c_lime, .3f);
                drawImage9patch(Sprite("elements", "glow"), _pax - 16, _pay - pointsText_size.Y / 2 - 12,
                    pointsText_size.X + 32, pointsText_size.Y + 24, 15);
                drawSetColor(c_white);
                drawTextFont(fSubtitle, pointsText, _pax, _pay, HALIGN.Left, VALIGN.Center);
            }

            btx += btw;
        }

        return bty + bth;
    }
}
