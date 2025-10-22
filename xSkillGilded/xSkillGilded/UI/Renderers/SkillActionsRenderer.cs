using System.Numerics;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using xSkillGilded.Managers;
using xSkillGilded.Models;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded.UI.Renderers;

internal static class SkillActionsRenderer
{
    public static void Draw(SkillPageManager pageManager, ICoreClientAPI api, float padd, int windowHeight, ref string hoveringID, ref TooltipObject hoveringTooltip, System.Action<bool> onSparringToggle)
    {
        var actx = padd + _ui(8);
        var acty = windowHeight - padd - _ui(8);

        var actbw = _ui(96);
        var actbh = _ui(96);
        var actbx = actx;
        var actby = acty - actbh;
        var actLh = _ui(24);
        var isSparing = pageManager.XLevelingClient.LocalPlayerSkillSet.Sparring;

        drawSetColor(new Vector4(1, 1, 1, isSparing ? 1 : .5f));
        drawImage(Sprite("elements", isSparing ? "sparring_enabled" : "sparring_disabled"),
            actbx + actbw / 2 - _ui(96) / 2, actby + actbh - _ui(96), _ui(96), _ui(96));
        drawSetColor(c_white);

        drawImage9patch(Sprite("elements", "button_idle"), actbx, actby + actbh - actLh, actbw, actLh, 2);
        if (mouseHover(actbx, actby, actbx + actbw, actby + actbh))
        {
            hoveringID = "Sparring";
            drawImage9patch(Sprite("elements", "button_idle_hovering"), actbx - 1, actby + actbh - actLh - 1, actbw + 2,
                actLh + 2, 2);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                onSparringToggle(!isSparing);
                api.Gui.PlaySound(
                    new AssetLocation("xskillgilded", isSparing ? "sounds/sparringoff.ogg" : "sounds/sparringon.ogg"),
                    false, .6f);
            }

            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                drawImage9patch(Sprite("elements", "button_pressing"), actbx, actby + actbh - actLh, actbw, actLh, 2);

            hoveringTooltip = new TooltipObject(Lang.GetUnformatted("xlib:sparringmode"),
                Lang.GetUnformatted("xlib:sparring-desc"));
        }

        drawTextFont(fSubtitle, "Spar", actbx + actbw / 2, actby + actbh - _ui(4), HALIGN.Center, VALIGN.Bottom);
    }
}
