using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using VSImGui;
using VSImGui.API;
using XLib.XLeveling;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded {
    public class xSkillGraphicalUI : ModSystem {
        public static ModConfig config;
        public const string configFileName = "xskillsgilded.json";

        private ICoreClientAPI api;
        private ImGuiModSystem imguiModSystem;

        ImFontPtr FTitle;

        XLeveling xLeveling;
        XLevelingClient xLevelingClient;
        Dictionary<string, List<PlayerSkill>> skillGroups;
        List<PlayerSkill> allSkills;
        List<PlayerSkill> currentSkills;
        List<PlayerAbility> specializeGroups;
        PlayerSkill currentPlayerSkill;

        Dictionary<PlayerSkill, int> previousLevels;
        
        const int checkAPIInterval   = 1000;
        const int checkLevelInterval = 100;
        private long checkAPIID, checkLevelID;
        bool isReady = false;

        bool metaPage = false;
        public bool isOpen = false;
        int windowX      = 0;
        int windowY      = 0;
        int windowBaseWidth  = 1800;
        int windowBaseHeight = 1060;
        Stopwatch stopwatch;
        
        Dictionary<string, AbilityButton> abilityButtons;
        List<float> levelRequirementBars;
        List<DecorationLine> decorationLines;

        private float _abilityPageWidth  = 0;
        private float _abilityPageHeight = 0;
        private float buttonWidth      = 128;
        private float buttonHeight     = 100;
        private float buttonPad        =  16;

        private float tooltipWidth   = 400;
        private float contentPadding = 16;

        string page = "";
        int skillPage = 0;

        string currentTooltip = "";
        List<VTMLblock> tooltipVTML;
        AbilityButton hoveringButton;
        TooltipObject hoveringTooltip = null;
        string hoveringID = null;

        EffectBox effectBox;
        private ImGuiViewportPtr _viewPort;

        public override bool ShouldLoad(EnumAppSide forSide) { return forSide == EnumAppSide.Client; }
        public override double ExecuteOrder() { return 1; }
        
        public override void StartClientSide(ICoreClientAPI api) {
            this.api = api;
            resourceLoader.setApi(api);
            var mainViewport = ImGui.GetMainViewport();
            this._viewPort = mainViewport;
            
            try {
                config = api.LoadModConfig<ModConfig>(configFileName);
                if (config == null)
                    config = new ModConfig();

                api.StoreModConfig<ModConfig>(config, configFileName);

            } catch (Exception e) {
                config = new ModConfig();
            }

            //api.Logger.Debug("CONFIG: " + config.ToString());

            api.Input.RegisterHotKey("xSkillGilded", "Show/Hide Skill Dialog - Gilded", GlKeys.O, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("xSkillGilded", Toggle);

            imguiModSystem = api.ModLoader.GetModSystem<ImGuiModSystem>();
            imguiModSystem.Draw   += Draw;
            imguiModSystem.Closed += Close;

            fTitle        = new Font().LoadedTexture(api, Sprite("fonts", "scarab"), FontData.SCARAB).setLetterSpacing(2);
            fTitleGold    = new Font().LoadedTexture(api, Sprite("fonts", "scarab_gold"), FontData.SCARAB).setLetterSpacing(2).setFallbackColor(c_gold);
            fSubtitle     = new Font().LoadedTexture(api, Sprite("fonts", "scarab_small"), FontData.SCARAB_SMALL).setLetterSpacing(1);
            fSubtitleGold = new Font().LoadedTexture(api, Sprite("fonts", "scarab_small_gold"), FontData.SCARAB_SMALL).setLetterSpacing(1).setFallbackColor(c_gold);

            useInternalTextDrawer = Lang.UsesNonLatinCharacters(Lang.CurrentLocale);
            if(!useInternalTextDrawer) {
                fTitle.baseLineHeight        = ImGui.GetTextLineHeight();
                fTitleGold.baseLineHeight    = ImGui.GetTextLineHeight();
                fSubtitle.baseLineHeight     = ImGui.GetTextLineHeight();
                fSubtitleGold.baseLineHeight = ImGui.GetTextLineHeight();
            }

            tooltipVTML   = new List<VTMLblock>();

            // probably the corecct way to load font
            // FontManager.BeforeFontsLoaded += initFonts;
            // FTitle = FontManager.Fonts["scarab"];

            stopwatch    = Stopwatch.StartNew();
            checkAPIID   = api.Event.RegisterGameTickListener(onCheckAPI,   checkAPIInterval);
            checkLevelID = api.Event.RegisterGameTickListener(onCheckLevel, checkLevelInterval);

            effectBox = new(api);
        }

        public void initFonts(HashSet<string> fonts, HashSet<int> sizes) {
            fonts.Add(Path.Combine(GamePaths.AssetsPath, "xskillgilded", "fonts", "scarab.ttf"));
        }

        public void onCheckAPI(float dt) {
            if(getSkillData()) isReady = true;
            if(isReady) api.Event.UnregisterGameTickListener(checkAPIID);
        }

        public void onCheckLevel(float dt) {
            if(previousLevels == null) return;
            if(!config.lvPopupEnabled) return;

            foreach(PlayerSkill skill in previousLevels.Keys) {
                int currentLevel = skill.Level;

                if(currentLevel > previousLevels[skill]) {
                    LevelPopup levelPopup = new(api, skill);
                    api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/levelup.ogg"), false, .3f);
                    api.Logger.Debug($"{skill.Skill.Name}, {skill.Skill.Id} Level up");
                }

                previousLevels[skill] = currentLevel;
            }
        }

        private bool getSkillData() {
            xLeveling        = api.ModLoader.GetModSystem<XLeveling>();
            if(xLeveling == null) return false;

            xLevelingClient  = xLeveling.IXLevelingAPI as XLevelingClient;
            if(xLevelingClient == null) return false;

            effectBox.xLeveling = xLeveling;
            effectBox.xLevelingClient = xLevelingClient;

            PlayerSkillSet playerSkillSet = xLevelingClient.LocalPlayerSkillSet;
            if(playerSkillSet == null) return false;

            skillGroups      = new Dictionary<string, List<PlayerSkill>>();
            previousLevels   = new Dictionary<PlayerSkill, int>();
            allSkills        = new List<PlayerSkill>();
            specializeGroups = new List<PlayerAbility>();

            bool firstGroup = true;
            foreach (PlayerSkill skill in playerSkillSet.PlayerSkills) {
                if (skill.Skill.Enabled && !skill.Hidden && skill.PlayerAbilities.Count > 0) {
                    string groupName = skill.Skill.Group;

                    if (!skillGroups.ContainsKey(groupName))
                        skillGroups[groupName] = new List<PlayerSkill>();
                    
                    List<PlayerSkill> groupList = skillGroups[groupName];
                    groupList.Add(skill);
                    allSkills.Add(skill);
                    previousLevels[skill] = skill.Level;

                    if (firstGroup) {
                        setPage(groupName);
                        firstGroup = false;
                    }

                    foreach(PlayerAbility playerAbility in skill.PlayerAbilities) {
                        Ability ability = playerAbility.Ability;
                        foreach(Requirement req in ability.Requirements) {
                            if(IsAbilityLimited(req)) {
                                specializeGroups.Add(playerAbility);
                                break;
                            }
                        }
                            
                    }
                }
            }

            return true;
        }

        private void setPage(string page) {
            if(page == "_Specialize") {
                this.page = "_Specialize";
                metaPage  = true;

                setPageContentList(specializeGroups);
                return;
            }

            if (!skillGroups.ContainsKey(page)) return;

            metaPage  = false;
            this.page = page;
            currentSkills = skillGroups[page];
            setSkillPage(0);
        }

        private void setSkillPage(int page) {
            if (page < 0 || page >= currentSkills.Count) return;
            skillPage = page;
            currentPlayerSkill = currentSkills[page];

            setPageContent();
        }

        private void setPageContent() {
            abilityButtons = new Dictionary<string, AbilityButton>();

            float pad = buttonPad;

            List<int> levelTiers  = new List<int>();
            List<int> buttonTiers = new List<int>();

            foreach (PlayerAbility ability in currentPlayerSkill.PlayerAbilities) {
                if(!ability.IsVisible()) continue;
                int lv = ability.Ability.RequiredLevel(1);

                while(levelTiers.Count <= lv) levelTiers.Add(0);
                levelTiers[lv]++;
            }

            Dictionary<int, int> levelTierMap = new Dictionary<int, int>();
            for(int i = 0, j = 0; i < levelTiers.Count; i++) {
                levelTierMap[i] = j;
                if (levelTiers[i] > 0) j++;
            }

            foreach (PlayerAbility ability in currentPlayerSkill.PlayerAbilities) {
                if(!ability.IsVisible()) continue;
                string name = ability.Ability.Name;
                
                int lv   = ability.Ability.RequiredLevel(1);
                int tier = levelTierMap[lv];

                while(buttonTiers.Count <= tier) buttonTiers.Add(0);
                buttonTiers[tier]++;

                AbilityButton button = new AbilityButton(ability);

                button.tier = tier;
                abilityButtons[name] = button;
            }
            
            Dictionary<int, int> buttonTierMap = new Dictionary<int, int>();
            List<float> tierX = new List<float>();

            for(int i = 0, j = 0; i < buttonTiers.Count; i++) {
                buttonTierMap[i] = j;
                if (buttonTiers[i] > 0) j++;
                tierX.Add(0);
            }

            foreach (AbilityButton button in abilityButtons.Values) {
                int tier = buttonTierMap[button.tier];
                int roww = buttonTiers[button.tier];

                float _x = tierX[tier] - (roww - 1) / 2 * (buttonWidth + pad);
                float _y = -tier * (buttonHeight + pad);
                tierX[tier] += buttonWidth + pad;

                button.x = _x;
                button.y = _y;
            }

            float minx =  99999;
            float miny =  99999;
            float maxx = -99999;
            float maxy = -99999;

            foreach (AbilityButton button in abilityButtons.Values) {
                minx = Math.Min(minx, button.x);
                miny = Math.Min(miny, button.y);

                maxx = Math.Max(maxx, button.x + buttonWidth);
                maxy = Math.Max(maxy, button.y + buttonHeight);
            }

            float cx = (minx + maxx) / 2;
            float cy = (miny + maxy) / 2;

            foreach (AbilityButton button in abilityButtons.Values) {
                button.x -= cx;
                button.y -= cy;
            }

            _abilityPageWidth  = maxx - minx;
            _abilityPageHeight = maxy - miny;

            levelRequirementBars = new List<float> ();
            for(int i = 0; i < levelTiers.Count; i++) {
                if (levelTiers[i] > 0) 
                    levelRequirementBars.Add(i);
            }

            decorationLines = new List<DecorationLine>();

            foreach (AbilityButton button in abilityButtons.Values) {
                float x0 = button.x;
                float y0 = button.y;

                foreach(Requirement req in button.Ability.Ability.Requirements) {
                    ExclusiveAbilityRequirement req2 = req as ExclusiveAbilityRequirement;
                    if(req2 != null) {
                        string name = req2.Ability.Name;
                        if(abilityButtons.ContainsKey(name)) {
                            AbilityButton _button = abilityButtons[name];
                            float x1 = _button.x;
                            float y1 = _button.y;

                            decorationLines.Add(new(x0, y0, x1, y1, new(165/255f, 98/255f, 67/255f, .5f)));
                        }
                    }
                }
            }
        }

        private void setPageContentList(List<PlayerAbility> abilityList) {
            abilityButtons = new Dictionary<string, AbilityButton>();
            levelRequirementBars.Clear();
            decorationLines.Clear();

            float pad  = buttonPad;

            int amo  = abilityList.Count;
            int col  = (int)Math.Floor(Math.Sqrt((double)amo));
            int indx = 0;

            for(int i = 0; i < amo; i++) {
                PlayerAbility ability = abilityList[i];
                if(!ability.IsVisible()) continue;

                int c = indx % col;
                int r = indx / col;
                indx++;

                string name = ability.Ability.Name;
                int lv   = ability.Ability.RequiredLevel(0);
                
                AbilityButton button = new AbilityButton(ability);

                button.x = c * (buttonWidth  + pad);
                button.y = r * (buttonHeight + pad);

                abilityButtons[name] = button;
            }

            float minx =  99999;
            float miny =  99999;
            float maxx = -99999;
            float maxy = -99999;

            foreach (AbilityButton button in abilityButtons.Values) {
                minx = Math.Min(minx, button.x);
                miny = Math.Min(miny, button.y);

                maxx = Math.Max(maxx, button.x + buttonWidth);
                maxy = Math.Max(maxy, button.y + buttonHeight);
            }

            float cx = (minx + maxx) / 2;
            float cy = (miny + maxy) / 2;

            foreach (AbilityButton button in abilityButtons.Values) {
                button.x -= cx;
                button.y -= cy;
            }
            
            _abilityPageWidth  = maxx - minx;
            _abilityPageHeight = maxy - miny;
        }

        private CallbackGUIStatus Draw(float deltaSeconds) {
            if(!isOpen) return CallbackGUIStatus.Closed;

            ElementBounds window = api.Gui.WindowBounds;
            IXPlatformInterface xPlatform = api.Forms;
            Size2i size = xPlatform.GetScreenSize();

            uiScale = ClientSettings.GUIScale;

            if(!useInternalTextDrawer) {
                fTitle.baseScale        = _ui(1);
                fTitleGold.baseScale    = _ui(1);
                fSubtitle.baseScale     = _ui(1);
                fSubtitleGold.baseScale = _ui(1);
            }

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,   0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,    0);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,    0);

            int windowWidth  = Math.Min(windowBaseWidth,  (int)window.OuterWidth  - 128); // 160
            int windowHeight = Math.Min(windowBaseHeight, (int)window.OuterHeight - 128); // 160

            ImGui.SetNextWindowSize(new (windowWidth, windowHeight));
            // ImGui.SetNextWindowPos(new (windowX, windowY));
            ImGui.SetNextWindowPos(new Vector2(
                _viewPort.Pos.X + (_viewPort.Size.X - windowWidth) / 2,
                _viewPort.Pos.Y + (_viewPort.Size.Y - windowHeight) / 2
            ));
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar
                                                    | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground;

            ImGui.Begin("xSkill Gilded", flags);
            
            drawImage(Sprite("elements", "bg"), 0, 0, windowWidth, windowHeight);
            float padd = _ui(contentPadding);
            float contentWidth = windowWidth - _ui(tooltipWidth) - padd * 2;
            float deltaTime    = stopwatch.ElapsedMilliseconds / 1000f;
            stopwatch.Restart();

            string _hoveringID = null;
            
            #region Skill Group Tab
                float btx = padd;
                float bty = padd;
                float bth = _ui(32);
                
                float _btsw  = _ui(96);
                float btxc   = btx + _btsw / 2;
                float btww   = _btsw * .5f / 2;
                float _alpha = 1f;
                
                if(page == "_Specialize") {
                    drawImage(Sprite("elements", "tab_sep_selected"), btxc - btww, bty + bth - 4, btww * 2, 4);
                    _alpha = 1f;

                } else if (mouseHover(btx, bty, btx + _btsw, bty + bth)) {
                    _hoveringID = "_Specialize";
                    drawImage(Sprite("elements", "tab_sep_hover"), btxc - btww, bty + bth - 4, btww * 2, 4);
                    _alpha = 1f;
                    if(ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                        setPage("_Specialize");
                        api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/page.ogg"), false, .3f);
                    }

                } else {
                    drawImage(Sprite("elements", "tab_sep"), btxc - btww, bty + bth - 4, btww * 2, 4);
                    _alpha = .5f;
                }

                drawSetColor(c_white, _alpha);
                drawImage(page == "_Specialize"? Sprite("elements", "meta_spec_selected") : Sprite("elements", "meta_spec"), btxc - _ui(24 / 2), bty + 4, _ui(24), _ui(24));
                drawSetColor(c_white);
                btx += _btsw;
                
                float btw = (windowWidth - padd - btx) / skillGroups.Count;
                
                foreach(string groupName in skillGroups.Keys) {
                    btxc = btx + btw / 2;
                    btww = btw * .5f / 2;
                    float alpha = 1f;
                    Font _fTitle = fTitle;

                    int points = 0;
                    foreach(PlayerSkill skill in skillGroups[groupName]) {
                        points += skill.AbilityPoints;
                    }

                    if (groupName == page) {
                        drawImage(Sprite("elements", "tab_sep_selected"), btxc - btww, bty + bth - 4, btww * 2, 4);
                        _fTitle = fTitleGold;

                    } else if (mouseHover(btx, bty, btx + btw, bty + bth)) {
                        _hoveringID = groupName;
                        drawImage(Sprite("elements", "tab_sep_hover"), btxc - btww, bty + bth - 4, btww * 2, 4);
                        if(ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                            setPage(groupName);
                            api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/page.ogg"), false, .3f);
                        }

                    } else {
                        drawImage(Sprite("elements", "tab_sep"), btxc - btww, bty + bth - 4, btww*2, 4);
                        alpha = .5f;
                    }

                    drawSetColor(c_white, alpha);
                    Vector2 skillName_size = drawTextFont(_fTitle, groupName, btx + btw / 2, bty + bth / 2, HALIGN.Center, VALIGN.Center);
                    drawSetColor(c_white);
                
                    if(points > 0) {
                        float _pax = btx + btw / 2 + skillName_size.X / 2 + _ui(20);
                        float _pay = bty + bth / 2;

                        string pointsText = points.ToString();
                        Vector2 pointsText_size = fSubtitle.CalcTextSize(pointsText);
                        drawSetColor(c_lime, .3f);
                        drawImage9patch(Sprite("elements", "glow"), _pax - 16, _pay - pointsText_size.Y / 2 - 12, pointsText_size.X + 32, pointsText_size.Y + 24, 15);
                        drawSetColor(c_white);
                        drawTextFont(fSubtitle, pointsText, _pax, _pay, HALIGN.Left, VALIGN.Center);
                    }

                    btx += btw;
                }
            #endregion

            #region Skills Tab
            float skx = padd;
            float sky = bty + bth + _ui(4);
            float skw = (windowWidth - padd * 2) / currentSkills.Count;
            float skh = _ui(32);

            if(!metaPage) {
                for(int i = 0; i < currentSkills.Count; i++) {
                    PlayerSkill skill = currentSkills[i];
                    string skillName = skill.Skill.DisplayName;
                    float skxc = skx + skw / 2;
                    float skww = skw * .5f / 2;
                    Vector4 color = new Vector4(1,1,1,1);
                    Font _fTitle = fSubtitle;

                    if(i != skillPage) {
                        if (mouseHover(skx, sky, skx + skw, sky + skh)) {
                            _hoveringID = skillName;
                            drawImage(Sprite("elements", "tab_sep_hover"), skxc - skww, sky + skh - 4, skww * 2, 4);  
                            if(ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                                setSkillPage(i);
                                api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/pagesub.ogg"), false, .3f);
                            }

                        } else {
                            drawImage(Sprite("elements", "tab_sep"), skxc - skww, sky + skh - 4, skww * 2, 4);
                            color.W = .5f;
                        }
                    
                    } else {
                        drawImage(Sprite("elements", "tab_sep_selected"), skxc - skww, sky + skh - 4, skww * 2, 4);
                        _fTitle = fSubtitleGold;
                    }
                
                    drawSetColor(color);
                    Vector2 skillName_size = drawTextFont(_fTitle, skillName, skx + skw / 2, sky + skh / 2, HALIGN.Center, VALIGN.Center);
                    drawSetColor(c_white);

                    float points = skill.AbilityPoints;
                    if(points > 0) {
                        float _pax = skxc + skillName_size.X / 2 + _ui(20);
                        float _pay = sky + skh / 2;

                        string pointsText = points.ToString();
                        Vector2 pointsText_size = fSubtitle.CalcTextSize(pointsText);
                        drawSetColor(c_lime, .3f);
                        drawImage9patch(Sprite("elements", "glow"), _pax - 16, _pay - pointsText_size.Y / 2 - 12, pointsText_size.X + 32, pointsText_size.Y + 24, 15);
                        drawSetColor(c_white);
                        drawTextFont(fSubtitle, pointsText, _pax, _pay, HALIGN.Left, VALIGN.Center);
                    }

                    skx += skw;
                }
            }
            #endregion

            #region Ability
            float abx = padd;
            float aby = sky + skh + _ui(8);
            float abw = contentWidth - abx - _ui(8);
            float abh = windowHeight - aby - _ui(8);
            float bw  = _ui(buttonWidth);
            float bh  = _ui(buttonHeight);

            float padX = Math.Max(0, _ui(_abilityPageWidth) - abw  + _ui(128));
            float padY = Math.Max(0, _ui(_abilityPageHeight) - abh + _ui(128));
            
            float mx = ImGui.GetMousePos().X;
            float my = ImGui.GetMousePos().Y;

            float mrx = (mx - (this._viewPort.Pos.X + abx)) / abw - .5f;
            float mry = (my - (this._viewPort.Pos.Y + aby)) / abh - .5f;

            float ofmx = (float)Math.Round(-padX * mrx);
            float ofmy = (float)Math.Round(-padY * mry);

            windowPosX = windowX + abx;
            windowPosY = windowY + aby;
            ImGui.SetCursorPos(new(abx, aby));
            ImGui.BeginChild("Ability", new(abw, abh), false, flags);
                float offx = ofmx + abw / 2;
                float offy = ofmy + abh / 2;
                AbilityButton _hoveringButton = null;
                
                float lvx = _ui(64);
                
                for(int i = 1; i < levelRequirementBars.Count; i++) {
                    float lv = levelRequirementBars[i];
                    float _y = offy + _ui(_abilityPageHeight / 2 - i * (buttonHeight + buttonPad) + buttonPad / 2);

                    if (mouseHover(lvx, _y - buttonHeight - buttonPad, lvx + abw, _y))
                        drawSetColor(new(239/255f, 183/255f, 117/255f, 1));
                    else 
                        drawSetColor(new(104/255f, 76/255f, 60/255f, 1));

                    string lvReqText = $"Level {lv}";
                    drawImage(Sprite("elements", "level_sep"), lvx, _y - _ui(64), abw - _ui(128), _ui(64));
                    drawTextFont(fSubtitle, lvReqText, lvx + _ui(32), _y - _ui(2), HALIGN.Left, VALIGN.Bottom);
                }
                drawSetColor(c_white);

                foreach (DecorationLine line in decorationLines) {
                    drawSetColor(line.color);
                    
                    if(line.y0 == line.y1) {
                        float _x0 = offx + _ui(Math.Min(line.x0, line.x1)) + bw;
                        float _x1 = offx + _ui(Math.Max(line.x0, line.x1));
                        
                        drawImage(Sprite("elements", "pixel"), _x0, offy + _ui(line.y0 + bh / 2 - 10), _x1 - _x0, _ui(20));
                    }
                }
                drawSetColor(c_white);
            
                foreach (AbilityButton button in abilityButtons.Values) {
                    float bx = _ui(button.x) + offx;
                    float by = _ui(button.y) + offy;
                    string buttonSpr = "abilitybox_frame_inactive";
                    Vector4 color = c_grey;
                
                    PlayerAbility ability = button.Ability;
                    LoadedTexture texture = button.Texture;
                    int tier = ability.Tier;

                    if(tier > 0) color = c_white;
                    
                    string abilityName = button.Ability.Ability.DisplayName;
                    bool   reqFulfiled = ability.RequirementsFulfilled(tier + 1);

                    if(reqFulfiled) {
                        color = c_lime;
                        buttonSpr = "abilitybox_frame_active";
                    }

                    if(tier == ability.Ability.MaxTier) {
                        color = c_gold;
                        buttonSpr = "abilitybox_frame_max";
                    }

                    if (mouseHover(bx, by, bx + bw, by + bh)) {
                        _hoveringButton = button;
                        
                        if(ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                            ability.SetTier(ability.Tier + 1);
                            if(ability.Tier > tier) {
                                button.glowAlpha = 1;
                                
                                if(ability.Tier == ability.Ability.MaxTier)
                                    api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/upgradedmax.ogg"), false, .3f);
                                else 
                                    api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/upgraded.ogg"), false, .3f);
                            }
                        }
                        
                        if(ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
                            ability.SetTier(ability.Tier - 1);

                            if(ability.Tier < tier)
                                api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/downgraded.ogg"), false, .3f);
                        }
                    }

                    if(button.glowAlpha > 0) {
                        float glow_size = _ui(256);
                        drawSetColor(tier == ability.Ability.MaxTier? c_gold : c_lime, button.glowAlpha);
                        drawImage(Sprite("elements", "ability_glow"), bx + bw / 2 - glow_size / 2, by + bh / 2 - glow_size / 2, glow_size, glow_size);
                        drawSetColor(c_white);
                    }

                    button.glowAlpha = lerpTo(button.glowAlpha, 0, .2f, deltaTime);
                    button.drawColor = color;
                    drawImage(Sprite("elements", "abilitybox_bg"), bx, by, bw, bh);
                    if(ability.Tier == 0 && !reqFulfiled)
                        drawSetColor(new(1,1,1,.25f));
                    if(texture != null) drawImageFitOverflow(texture, bx, by, bw, bh, .75f);
                    drawSetColor(c_white);
                    drawImage9patch(Sprite("elements", "ability_shadow"), bx, by, bw, bh, 30);
                    
                    Vector2 _nameSize = fSubtitle.CalcTextSize(abilityName);
                    float   bgh = _nameSize.X > bw - _ui(8)? bh : _ui(48);
                    drawImage(Sprite("elements", "abilitybox_name_under"), bx, by + bh - bgh, bw, bgh);
                    drawSetColor(color);
                    if(_nameSize.X > bw - _ui(8))
                        drawTextFontWrap(fSubtitle, abilityName, bx + bw / 2, by + bh - _ui(12), HALIGN.Center, VALIGN.Bottom, bw - _ui(8));
                    else 
                        drawTextFont(fSubtitle, abilityName, bx + bw / 2, by + bh - _ui(12), HALIGN.Center, VALIGN.Bottom);
                    drawSetColor(c_white);

                    float progress = ability.Tier / (float)ability.Ability.MaxTier;
                    float prh = _ui(6);
                    float prw = bw / (float)ability.Ability.MaxTier;
                    float prx = bx;
                    float pry = by + bh - _ui(2) - prh;

                    for(int i = 0; i < ability.Ability.MaxTier; i++)
                        drawImage9patch(Sprite("elements", "abilitybox_progerss_bg"), prx + i * prw, pry, prw, prh, 2);
                    
                    float tierWidth = ability.Tier * prw;
                    button.drawTierWidth = lerpTo(button.drawTierWidth , tierWidth, .85f, deltaTime);
                    if(button.drawTierWidth > 0) 
                        drawImage9patch(Sprite("elements", "abilitybox_progerss_content"), prx, pry, button.drawTierWidth, prh, 2);
                    
                    for(int i = 0; i < ability.Ability.MaxTier - 1; i++)
                        drawImage9patch(Sprite("elements", "abilitybox_progerss_overlay"), prx + i * prw, pry, prw + 1, prh, 2);
                    
                    drawImage9patch(Sprite("elements", buttonSpr), bx, by, bw, bh, 15);
                }

                if(_hoveringButton != null && hoveringButton != _hoveringButton)
                    api.Gui.PlaySound("tick", false, .5f);
                hoveringButton = _hoveringButton;
                if(hoveringButton != null) {
                    PlayerAbility ability = hoveringButton.Ability;
                    float bx  = _ui(hoveringButton.x) + offx;
                    float by  = _ui(hoveringButton.y) + offy;
                    Vector4 c = hoveringButton.drawColor;

                    drawSetColor(new(c.X, c.Y, c.Z, .5f));
                    drawImage9patch(Sprite("elements", "abilitybox_frame_selected"), bx - 16, by - 16, bw + 32, bh + 32, 30);
                    drawSetColor(c_white);

                    List<Requirement> requirements = ability.Ability.Requirements;
                    foreach (Requirement req in requirements)
                        drawRequirementHighlight(hoveringButton, req, offx, offy);

                }

            ImGui.EndChild();            
            windowPosX = windowX;
            windowPosY = windowY;
            #endregion

            #region Skills Description
            float sdx = padd + _ui(16);
            float sdy = sky + skh + _ui(16);
            float sdw = _ui(200);

            if(page == "_Specialize") {
                string skillTitle = Lang.GetUnformatted("xlib:specialisations");
                Vector2 skillTitle_size = drawTextFont(fTitleGold, skillTitle, sdx, sdy);
                sdy += fTitleGold.getLineHeight() + _ui(8);

                foreach(PlayerSkill skill in allSkills) {
                    float hh = drawSkillLevelDetail(skill, sdx, sdy, sdw, false);
                    sdy += hh;
                }


            } else {
                float hh = drawSkillLevelDetail(currentPlayerSkill, sdx, sdy, sdw, true);
                sdy += hh;
                
                float unlearnPoint    = currentPlayerSkill.PlayerSkillSet.UnlearnPoints;
                float unlearnPointReq = xLevelingClient.GetPointsForUnlearn();
                float unlearnAmount   = (float)Math.Floor(unlearnPoint / unlearnPointReq);
                float unlearnProgress = unlearnPoint / unlearnPointReq - unlearnAmount;
                float unx = sdx + sdw - _ui(8);
                float uny = sdy;
                
                drawSetColor(c_red);
                drawTextFont(fSubtitle, Lang.GetUnformatted("xlib:unlearnpoints"), sdx, sdy);

                if(unlearnAmount > 0) {
                    Vector2 unlearnPoint_size = fSubtitle.CalcTextSize(unlearnAmount.ToString());
                    drawSetColor(c_red, .3f);
                    drawImage9patch(Sprite("elements", "glow"), unx - unlearnPoint_size.X - 16, sdy - 12, unlearnPoint_size.X + 32, unlearnPoint_size.Y + 24, 15);
                    drawSetColor(c_white);
                }
                drawTextFont(fSubtitle, unlearnAmount.ToString(), unx, sdy, HALIGN.Right);
                
                sdy += fSubtitle.getLineHeight();
                drawProgressBar(unlearnProgress, sdx, sdy, sdw, _ui(4), c_dkgrey, c_red);
                sdy += _ui(4);
                
                float unlearnCooldown    = currentPlayerSkill.PlayerSkillSet.UnlearnCooldown;
                float unlearnCooldownMax = xLevelingClient.Config.unlearnCooldown;
                if(unlearnCooldown > 0) {
                    drawSetColor(c_grey);
                    drawTextFont(fSubtitle, "Cooldown", sdx, sdy);
                    drawTextFont(fSubtitle, FormatTime((float)Math.Round(unlearnCooldown)), unx, sdy, HALIGN.Right);
                    drawSetColor(c_white);
                }
                
                if(mouseHover(sdx, uny - 4, sdx + sdw, sdy + 4)) {
                    string desc = string.Format(Lang.GetUnformatted("xskillgilded:unlearnDesc"), FormatTime(unlearnCooldownMax * 60f));
                    hoveringTooltip = new(Lang.GetUnformatted("xskillgilded:unlearnTitle"), desc);
                }
            }
            #endregion

            #region Skills actions
                float actx = padd + _ui(8);
                float acty = windowHeight - padd - _ui(8);

                float actbw = _ui(96);
                float actbh = _ui(96);
                float actbx = actx;
                float actby = acty - actbh;
                float actLh = _ui(24);
                bool isSparing = xLevelingClient.LocalPlayerSkillSet.Sparring;

                drawSetColor(new Vector4(1,1,1,isSparing? 1 : .5f));
                drawImage(Sprite("elements", isSparing? "sparring_enabled" : "sparring_disabled"), actbx + actbw / 2 - _ui(96) / 2, actby + actbh - _ui(96), _ui(96), _ui(96));
                drawSetColor(c_white);
                
                drawImage9patch(Sprite("elements", "button_idle"), actbx, actby + actbh - actLh, actbw, actLh, 2);
                if (mouseHover(actbx, actby, actbx + actbw, actby + actbh)) {
                    _hoveringID = "Sparring";
                    drawImage9patch(Sprite("elements", "button_idle_hovering"), actbx-1, actby + actbh - actLh-1, actbw+2, actLh+2, 2);
                    if(ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                        OnSparringToggle(!isSparing);
                        api.Gui.PlaySound(new AssetLocation("xskillgilded", isSparing? "sounds/sparringoff.ogg" : "sounds/sparringon.ogg"), false, .6f);
                    }

                    if(ImGui.IsMouseDown(ImGuiMouseButton.Left)) {
                        drawImage9patch(Sprite("elements", "button_pressing"), actbx, actby + actbh - actLh, actbw, actLh, 2);
                    }
                    
                    hoveringTooltip = new(Lang.GetUnformatted("xlib:sparringmode"), Lang.GetUnformatted("xlib:sparring-desc"));
                } 

                drawTextFont(fSubtitle, "Spar", actbx + actbw / 2, actby + actbh - _ui(4), HALIGN.Center, VALIGN.Bottom);
            #endregion

            #region Tooltip
                float tooltipX = windowWidth - tooltipWidth - padd;
                float tooltipY = sky + skh + _ui(32);
                float tooltipW = tooltipWidth - padd;
                float tooltipH = windowHeight - tooltipY - padd;
                
                drawImage(Sprite("elements", "tooltip_sep_v"), tooltipX - _ui(16), tooltipY, 2, tooltipH);
                
                if(hoveringTooltip != null) {
                    tooltipY += fTitleGold.getLineHeight();
                    drawTextFont(fTitleGold, hoveringTooltip.Title, tooltipX + _ui(8), tooltipY, HALIGN.Left, VALIGN.Bottom);
                    
                    tooltipY += _ui(2);
                    drawProgressBar(0, tooltipX, tooltipY, tooltipW, _ui(4), c_dkgrey, c_lime);
                    tooltipY += _ui(12);
                    
                    // float h = drawTextWrap(hoveringTooltip.Description, tooltipX + 8, tooltipY, HALIGN.Left, VALIGN.Top, tooltipW - 16);
                    if(currentTooltip != hoveringTooltip.Description) {
                        tooltipVTML = VTML.parseVTML(hoveringTooltip.Description);
                        currentTooltip = hoveringTooltip.Description; 
                    }

                    float h = drawTextVTML(tooltipVTML, tooltipX + _ui(8), tooltipY, tooltipW - _ui(16));

                } else if(_hoveringButton != null) {
                    PlayerAbility ability = _hoveringButton.Ability;
                    
                    string name      = ability.Ability.DisplayName;
                    string skillName = ability.Ability.Skill.DisplayName;
                    int    tier      = ability.Tier;
                    int    tierMax   = ability.Ability.MaxTier;
                    string tierText  = "Lv. " + tier + "/" + tierMax;
                    
                    tooltipY += fTitleGold.getLineHeight();
                    drawTextFont(fTitleGold, name, tooltipX + _ui(8), tooltipY, HALIGN.Left, VALIGN.Bottom);
                    drawTextFont(fSubtitle, tierText, tooltipX + tooltipW - _ui(8), tooltipY, HALIGN.Right, VALIGN.Bottom);

                    tooltipY += _ui(2);
                    drawProgressBar((float)tier / tierMax, tooltipX, tooltipY, tooltipW, _ui(4), c_dkgrey, tier == tierMax? c_gold : c_lime);
                    tooltipY += _ui(12);

                    string descCurrTier = formatAbilityDescription(ability.Ability, tier);
                    // float h = drawTextWrap(descCurrTier, tooltipX + 8, tooltipY, HALIGN.Left, VALIGN.Top, tooltipW - 16);
                    if(currentTooltip != descCurrTier) {
                        tooltipVTML = VTML.parseVTML(descCurrTier);
                        currentTooltip = descCurrTier; 
                    }

                    float h = drawTextVTML(tooltipVTML, tooltipX + _ui(8), tooltipY, tooltipW - _ui(16));
                    tooltipY += Math.Max(h + _ui(16), _ui(160));

                    drawSetColor(new(104/255f, 76/255f, 60/255f, 1));
                    drawImage(Sprite("elements", "tooltip_sep"), tooltipX + _ui(8), tooltipY, tooltipW - _ui(16), 1);
                    drawSetColor(c_white);
                    tooltipY += _ui(16);

                    if (tier < tierMax) {
                        int requiredLevel = ability.Ability.RequiredLevel(tier + 1);
                        string reqText    = string.Format(Lang.GetUnformatted("xskillgilded:abilityLevelRequired"), skillName, requiredLevel);

                        drawSetColor(currentPlayerSkill.Level >= requiredLevel? c_lime : c_red);
                        drawTextFont(fSubtitle, reqText, tooltipX + _ui(8), tooltipY);
                        drawSetColor(c_white);
                        tooltipY += fSubtitle.getLineHeight() + _ui(4);

                        List<Requirement> requirements = ability.Ability.Requirements;
                        foreach (Requirement req in requirements) {
                            if(req.MinimumTier > tier + 1) continue;
                            reqText = req.ShortDescription(ability);

                            if (reqText == null || reqText.Length == 0) continue;
                            string[] reqLines = reqText.Split('\n');

                            bool isFulfilled = req.IsFulfilled(ability, ability.Tier + 1);
                            drawSetColor(isFulfilled? c_lime : c_red);

                            ExclusiveAbilityRequirement exReq = req as ExclusiveAbilityRequirement;
                            if (exReq != null)
                                drawSetColor(isFulfilled? c_grey : c_red);
                        
                            foreach (string reqLine in reqLines) {
                                if (reqLine.Length == 0) continue;
                                drawTextFont(fSubtitle, reqLine, tooltipX + _ui(8), tooltipY);
                                tooltipY += fSubtitle.getLineHeight() + _ui(2);
                            }

                            drawSetColor(c_white);

                            tooltipY += _ui(4);
                        }
                    }

                    float actX = windowWidth  - padd - _ui(16);
                    float actY = windowHeight - padd - _ui( 8);
                    
                    drawSetColor(c_grey);
                    Vector2 _mouseRsize = drawTextFont(fSubtitle, Lang.GetUnformatted("xskillgilded:actionUnlearn"), actX, actY, HALIGN.Right, VALIGN.Bottom);    
                    drawImage(Sprite("elements", "mouse_right"), actX - _mouseRsize.X / 2 - _ui(64 / 2), actY - _ui(32 + 16), _ui(64), _ui(32));
                    actX -= _mouseRsize.X + _ui(16);
                    
                    Vector2 _mouseLsize = drawTextFont(fSubtitle, Lang.GetUnformatted("xskillgilded:actionLearn"), actX, actY, HALIGN.Right, VALIGN.Bottom);
                    drawImage(Sprite("elements", "mouse_left"),  actX - _mouseLsize.X / 2 - _ui(64 / 2), actY - _ui(32 + 16), _ui(64), _ui(32));
                    actX -= _mouseLsize.X + _ui(16);
                    drawSetColor(c_white);
                }
                
                hoveringTooltip = null;
                    
            #endregion

            // if(_hoveringID != null && _hoveringID != hoveringID) api.Gui.PlaySound("tick", false, .5f); // too annoying
            hoveringID = _hoveringID;

            drawImage9patch(Sprite("elements", "frame"), 0, 0, windowWidth, windowHeight, 60);

            ImGui.End();

            return CallbackGUIStatus.GrabMouse;
        }

        private string formatAbilityDescription(Ability ability, int currTier) {
            string descBase = ability.Description.Replace("%", "%%");
                   descBase = descBase.Replace("\n", "<br>");
            HashSet<int> percentageValues = new HashSet<int>();

            Regex percentRx = new(@"{(\d)}%%", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var matches = percentRx.Matches(descBase);
            foreach( Match match in matches ) {
                int index = int.Parse(match.Groups[1].Value);
                percentageValues.Add(index);
                descBase = descBase.Replace(match.Value, match.Value.Replace("%", ""));
            }

            int[]  values = ability.Values;
            int    valueCount = values.Length;
            
            int vpt   = ability.ValuesPerTier;
            int begin = vpt * (currTier - 1);
            int next  = begin + vpt;

            string[] v = new string[vpt];
            for (int i = 0; i < vpt; i++) {
                string str = "";
                
                if (begin + i >= 0 && begin + i < valueCount) {
                    string _v = values[begin + i].ToString();
                    if(percentageValues.Contains(i)) _v += "%%";

                    str += $"<font color=\"#feae34\">{_v}</font>"; 
                }

                if (next + i < valueCount) {
                    if(str.Length > 0) str += " > ";

                    string _v = values[next + i].ToString();
                    if(percentageValues.Contains(i)) _v += "%%";

                    str += $"<font color=\"#7ac62f\">{_v}</font>";
                }

                v[i] = str;
            }

            try {
                switch (vpt) {
                    case 1: return String.Format(descBase, v[0]);
                    case 2: return String.Format(descBase, v[0], v[1]);
                    case 3: return String.Format(descBase, v[0], v[1], v[2]);
                    case 4: return String.Format(descBase, v[0], v[1], v[2], v[3]);
                    case 5: return String.Format(descBase, v[0], v[1], v[2], v[3], v[4]);
                    case 6: return String.Format(descBase, v[0], v[1], v[2], v[3], v[4], v[5]);
                    case 7: return String.Format(descBase, v[0], v[1], v[2], v[3], v[4], v[5], v[6]);
                    case 8: return String.Format(descBase, v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7]);
                }
            } catch {
                return descBase;
            }

            return descBase;
        }

        private float drawSkillLevelDetail(PlayerSkill skill, float x, float y, float w, bool title) {
            float ys = y;
            float sx = x;

            string skillTitle = skill.Skill.DisplayName;
            //LoadedTexture skillIcon = Sprite("skillicon", skill.Skill.Name);
            //if(skillIcon.TextureId != 0) {
            //    drawSetColor(c_grey, .1f);
            //    drawImage(skillIcon, sx - _ui(8), y - _ui(16), _ui(64), _ui(64));
            //    drawSetColor(c_white);
            //}

            Vector2 skillTitle_size = drawTextFont(title? fTitleGold : fSubtitleGold, skillTitle, sx, y);

            if(!title) {
                int abilityPoint = skill.AbilityPoints;
                string skillPointTitle = abilityPoint.ToString();

                float unlearnPoint    = currentPlayerSkill.PlayerSkillSet.UnlearnPoints;
                float unlearnPointReq = xLevelingClient.GetPointsForUnlearn();
                float unlearnAmount   = (float)Math.Floor(unlearnPoint / unlearnPointReq);
                string unlearnPointTitle = unlearnAmount.ToString();

                float _sx = x + w - _ui(8);
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

            y += skillTitle_size.Y + _ui(title? 4 : 0);

            string skillLvTitle = "Lv." + skill.Level;
            Vector2 skillLvTitle_size = drawTextFont(fSubtitle, skillLvTitle, x, y);

            float currXp = (float)Math.Round(skill.Experience);
            float nextXp = (float)Math.Round(skill.RequiredExperience);
            float xpProgress = currXp / nextXp;

            drawSetColor(c_grey);
            drawTextFont(fSubtitle, $"{currXp}/{nextXp} xp", x + w - _ui(8), y, HALIGN.Right);
            drawSetColor(c_white);

            float expBonus = skill.Skill.GetExperienceMultiplier(skill.PlayerSkillSet, false) - 1f;
            if(expBonus != 0f) {
                string bonusText = (expBonus > 0? "+" : "-") + Math.Round(expBonus * 100f) + "%";
                drawSetColor(expBonus > 0? c_lime : c_red);
                Vector2 bonusTextSize = drawTextFont(fSubtitle, bonusText, x + w, y, HALIGN.Left);

                if(mouseHover(x + w - 4, y - 4, x + w + bonusTextSize.X + 4, y + bonusTextSize.Y + 4)) {
                    float totalBonus = skill.Skill.GetExperienceMultiplier(skill.PlayerSkillSet, true) - 1f;

                    string desc = Lang.GetUnformatted("xskillgilded:expBonusDesc");
                    string _bonusText     = (expBonus > 0? "+" : "-") + Math.Round(expBonus * 100f) + "%%";
                    string totalBonusText = (totalBonus > 0? "+" : "-") + Math.Round(totalBonus * 100f) + "%%";

                    desc = string.Format(desc, VTML.WrapFont(_bonusText, expBonus > 0? "#7ac62f" : "#bf663f"), VTML.WrapFont(totalBonusText, totalBonus > 0? "#7ac62f" : "#bf663f"));
                        
                    hoveringTooltip = new(Lang.GetUnformatted("xskillgilded:expBonusTitle"), desc);
                }
            }
            
            y += skillLvTitle_size.Y;
            drawProgressBar(xpProgress, x, y, w, _ui(4), c_dkgrey, c_lime);
            y += _ui(6);
            
            if(title) {
                int abilityPoint = skill.AbilityPoints;
                string skillPointTitle = string.Format(Lang.GetUnformatted("xskillgilded:pointsAvailable"), abilityPoint.ToString());
                if(abilityPoint > 0) {
                    Vector2 skillPoint_size = fSubtitle.CalcTextSize(abilityPoint.ToString());
                    drawSetColor(c_lime, .3f);
                    drawImage9patch(Sprite("elements", "glow"), x - 16, y - 12, skillPoint_size.X + 32, skillPoint_size.Y + 24, 15);
                    drawSetColor(c_white);
                }
                drawTextFont(fSubtitle, skillPointTitle, x, y);
                y += fSubtitle.getLineHeight();
            }
            
            y += _ui(8);
            return y - ys;
        }

        private void drawRequirementHighlight(AbilityButton button, Requirement requirement, float offx, float offy) {
            PlayerAbility ability = button.Ability;
            bool isFulfilled = requirement.IsFulfilled(ability, ability.Tier + 1);
            
            float bx  = _ui(button.x) + offx;
            float by  = _ui(button.y) + offy;
            float bw  = _ui(buttonWidth);
            float bh  = _ui(buttonHeight);
                        
            AbilityRequirement abilityRequirement = requirement as AbilityRequirement;
            if(abilityRequirement != null) {
                string name = abilityRequirement.Ability.Name;
                if(abilityButtons.ContainsKey(name)) {
                    AbilityButton _button = abilityButtons[name];

                    float _bx  = _ui(_button.x) + offx;
                    float _by  = _ui(_button.y) + offy;
                    Vector4 _c = isFulfilled? new(c_lime.X, c_lime.Y, c_lime.Z, .5f) : new(c_red.X, c_red.Y, c_red.Z, .9f);
                                
                    drawSetColor(_c);
                    drawImage9patch(Sprite("elements", "abilitybox_frame_selected"), _bx - 16, _by - 16, bw + 32, bh + 32, 30);
                    drawSetColor(c_white);
                }
            }

            AndRequirement andRequirement = requirement as AndRequirement;
            if(andRequirement != null) {
                foreach(Requirement _req in andRequirement.Requirements)
                    drawRequirementHighlight(button, _req, offx, offy);
            }

            OrRequirement orRequirement = requirement as OrRequirement;
            if(orRequirement != null) {
                foreach(Requirement _req in orRequirement.Requirements)
                    drawRequirementHighlight(button, _req, offx, offy);
            }
            
            ExclusiveAbilityRequirement exclusiveAbilityRequirement = requirement as ExclusiveAbilityRequirement;
            if(exclusiveAbilityRequirement != null) {
                string name = exclusiveAbilityRequirement.Ability.Name;
                if(abilityButtons.ContainsKey(name)) {
                    AbilityButton _button = abilityButtons[name];

                    float _bx  = _ui(_button.x) + offx;
                    float _by  = _ui(_button.y) + offy;

                    drawSetColor(new(c_red.X, c_red.Y, c_red.Z, .9f));
                    drawImage9patch(Sprite("elements", "abilitybox_frame_selected"), _bx - 16, _by - 16, bw + 32, bh + 32, 30);
                    drawSetColor(c_white);
                }
            }

        }

        private bool IsAbilityLimited(Requirement Requirement) {
            LimitationRequirement limitation = Requirement as LimitationRequirement;
            if (limitation != null) return true;

            AndRequirement and = Requirement as AndRequirement;
            if (and != null) {
                foreach(Requirement req in and.Requirements) {
                    if(IsAbilityLimited(req))
                        return true;
                }
            }
                
            NotRequirement not = Requirement as NotRequirement;
            if (not != null) {
                if(IsAbilityLimited(not.Requirement))
                    return true;
            }
            
            return false;
        }

        private void OnSparringToggle(bool toggle) {
            xLevelingClient.LocalPlayerSkillSet.Sparring = toggle;
            CommandPackage package = new CommandPackage(EnumXLevelingCommand.SparringMode, toggle ? 1 : 0);
            xLevelingClient.SendPackage(package);
        }

        private void Open() {
            if(isOpen) return;

            if(!isReady) {
                onCheckAPI(0);
                if(!isReady) return;
            }

            isOpen = true;
            imguiModSystem.Show();
            api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/open.ogg"), false, .3f);
        }

        private void Close() { 
            if(!isOpen) return;
            isOpen = false;
            api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/close.ogg"), false, .3f);
        }

        private bool Toggle(KeyCombination _) {
            if(isOpen) Close();
            else       Open();
            return true;
        }
        
        public override void Dispose() {
            base.Dispose();

            api.Event.UnregisterGameTickListener(checkLevelID);
            // imguiModSystem.Draw   -= Draw;
            // imguiModSystem.Closed -= Close;
        }
    }

    class AbilityButton {
        public string RawName        { get; set; }
        public string Name           { get; set; }
        public LoadedTexture Texture { get; set; }
        public PlayerAbility Ability { get; set; }
        public List<VTMLblock> Description { get; set; }

        public float x { get; set; }
        public float y { get; set; }

        public int tier = -1;
        public Vector4 drawColor;

        public float glowAlpha     = 0;
        public float drawTierWidth = 0;

        public AbilityButton(PlayerAbility ability) {
            Ability = ability;
            RawName = ability.Ability.Name;
            Name    = ability.Ability.DisplayName;

            string _icoPath = $"xskillgilded:textures/gui/skilltree/abilityicon/{RawName}.png";
            Texture = resourceLoader.Sprite(_icoPath);
        }
    }

    class DecorationLine {
        public float x0 { get; set; }
        public float y0 { get; set; }
        public float x1 { get; set; }
        public float y1 { get; set; }

        public Vector4 color;

        public DecorationLine(float x0, float y0, float x1, float y1, Vector4 color) {
            this.x0 = x0;
            this.y0 = y0;
            this.x1 = x1;
            this.y1 = y1;
            this.color = color;
        }
    }

    class TooltipObject {
        public string Title { get; set; }
        public string Description { get; set; }

        public TooltipObject(string title, string description) {
            Title = title;
            Description = description;
        }
    }
}
