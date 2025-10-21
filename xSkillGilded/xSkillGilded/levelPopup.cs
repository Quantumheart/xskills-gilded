using Cairo;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using VSImGui;
using VSImGui.API;
using XLib.XEffects;
using XLib.XLeveling;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded {

    public class LevelPopup {
        ICoreClientAPI api;
        private ImGuiModSystem imguiModSystem;
        PlayerSkill skill;
        
        float timer  = 0;
        bool showing = true;
        
        float windowWidth  = _ui(560);
        float windowHeight = _ui(160);

        public LevelPopup(ICoreClientAPI api, PlayerSkill skill) {
            this.api = api;
            this.skill = skill;

            imguiModSystem = api.ModLoader.GetModSystem<ImGuiModSystem>();
            imguiModSystem.Draw += Draw;
            imguiModSystem.Closed += Close;
        }

        public CallbackGUIStatus Draw(float deltaSecnds) {
            if(!showing) return CallbackGUIStatus.DontGrabMouse;

            var viewport = ImGui.GetMainViewport();

            float wx = viewport.Pos.X + (viewport.Size.X - windowWidth) / 2;
            float wy = viewport.Pos.Y + _ui(8);

            ImGui.SetNextWindowSize(new (windowWidth, windowHeight));
            ImGui.SetNextWindowPos(new (wx, wy));
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar
                 | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

            ImGui.Begin("effectBox", flags);
            drawSetColor(c_dkgrey, invLerp2(timer, 0f, 1f, 3.5f, 4f));
            drawImage(Sprite("elements", "level_up_glow"), 0, 0, windowWidth, windowHeight);
            drawSetColor(c_white, invLerp2(timer, 0f, .5f, 3.5f, 4f));
            float ww = invLerp(timer, 0f, .75f) * (windowWidth - _ui(80));
            drawImage(Sprite("elements", "level_sep"), windowWidth / 2 - ww / 2, windowHeight / 2 - _ui(64), ww, _ui(64));
            drawSetColor(c_white);

            LoadedTexture skillIcon = Sprite("skillicon", skill.Skill.Name);
            if(skillIcon.TextureId != 0) {
                drawSetColor(c_dkgrey, invLerp2(timer, 0f, 1f, 3.5f, 4f));
                drawImage(Sprite("elements", "level_up_glow"), windowWidth / 2 -_ui(40), windowHeight / 2 - _ui(40), _ui(80), _ui(80));
                drawSetColor(c_gold, invLerp2(timer, 0f, 1f, 3.5f, 4f));
                drawImage(skillIcon, windowWidth / 2 -_ui(16), windowHeight / 2 - _ui(16), _ui(32), _ui(32));
                drawSetColor(c_white);
            }

            string lvUpText = $"{skill.Skill.DisplayName} Level up";
            drawSetColor(c_white, invLerp2(timer, 0f, .3f, 3.5f, 4f));
            Vector2 lvUpText_s = drawTextFont(fTitleGold, lvUpText, windowWidth / 2, windowHeight / 2 - _ui(16), HALIGN.Center, VALIGN.Bottom);
            drawSetColor(c_white);

            var hk = api.Input.GetHotKeyByCode("xSkillGilded");
            string hotkeyText = $"Press {hk.CurrentMapping.ToString()} to open skill tree.";
            drawSetColor(c_white, invLerp2(timer, .3f, .6f, 3.5f, 4f) * .8f);
            Vector2 hotkeyText_s = drawTextFont(fSubtitle, hotkeyText, windowWidth / 2, windowHeight / 2 + _ui(16), HALIGN.Center, VALIGN.Top);
            drawSetColor(c_white);

            ImGui.End();
            
            timer += deltaSecnds;
            if(timer >= 4f) showing = false; // imguiModSystem.Draw -= Draw;
            
            return CallbackGUIStatus.DontGrabMouse;
        }

        float smoothstep(float t) {
            return t * t * (3f - 2f * t);
        }

        float invLerp(float time, float from, float to) {
            float a = Math.Clamp((time - from) / (to - from), 0f, 1f);
            return smoothstep(a);
        }

        float invLerp2(float time, float from0, float to0, float from1, float to1) {
            float a = Math.Min(Math.Clamp((time - from0) / (to0 - from0), 0f, 1f),
                          1f - Math.Clamp((time - from1) / (to1 - from1), 0f, 1f));
            return smoothstep(a);
        }

        private void Close() { }

    }
}
