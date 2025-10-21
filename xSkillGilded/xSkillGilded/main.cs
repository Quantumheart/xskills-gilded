using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text.RegularExpressions;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;
using VSImGui;
using VSImGui.API;
using XLib.XLeveling;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded;

public class xSkillGraphicalUI : ModSystem
{
    public const string configFileName = "xskillsgilded.json";

    private const int checkAPIInterval = 1000;
    private const int checkLevelInterval = 100;
    public static ModConfig config;
    private float _abilityPageHeight;

    private float _abilityPageWidth;
    private ImGuiViewportPtr _viewPort;

    private Dictionary<string, AbilityButton> abilityButtons;
    private List<PlayerSkill> allSkills;

    private ICoreClientAPI api;
    private readonly float buttonHeight = 100;
    private readonly float buttonPad = 16;
    private readonly float buttonWidth = 128;
    private long checkAPIID, checkLevelID;
    private readonly float contentPadding = 16;
    private PlayerSkill currentPlayerSkill;
    private List<PlayerSkill> currentSkills;

    private string currentTooltip = "";
    private List<DecorationLine> decorationLines;

    private EffectBox effectBox;

    private ImFontPtr FTitle;
    private AbilityButton hoveringButton;
    private string hoveringID;
    private TooltipObject hoveringTooltip;
    private ImGuiModSystem imguiModSystem;
    public bool isOpen;
    private bool isReady;
    private List<float> levelRequirementBars;

    private bool metaPage;

    private string page = "";

    private Dictionary<PlayerSkill, int> previousLevels;
    private Dictionary<string, List<PlayerSkill>> skillGroups;
    private int skillPage;
    private List<PlayerAbility> specializeGroups;
    private Stopwatch stopwatch;
    private List<VTMLblock> tooltipVTML;

    private readonly float tooltipWidth = 400;
    private readonly int windowBaseHeight = 1060;
    private readonly int windowBaseWidth = 1800;
    private readonly int windowX = 0;
    private readonly int windowY = 0;

    private XLeveling xLeveling;
    private XLevelingClient xLevelingClient;

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Client;
    }

    public override double ExecuteOrder()
    {
        return 1;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        this.api = api;
        resourceLoader.setApi(api);
        var mainViewport = ImGui.GetMainViewport();
        _viewPort = mainViewport;

        try
        {
            config = api.LoadModConfig<ModConfig>(configFileName);
            if (config == null)
                config = new ModConfig();

            api.StoreModConfig(config, configFileName);
        }
        catch (Exception e)
        {
            config = new ModConfig();
        }

        api.Input.RegisterHotKey("xSkillGilded", "Show/Hide Skill Dialog - Gilded", GlKeys.O,
            HotkeyType.GUIOrOtherControls);
        api.Input.SetHotKeyHandler("xSkillGilded", Toggle);

        imguiModSystem = api.ModLoader.GetModSystem<ImGuiModSystem>();
        imguiModSystem.Draw += Draw;
        imguiModSystem.Closed += Close;

        fTitle = new Font().LoadedTexture(api, Sprite("fonts", "scarab"), FontData.SCARAB).setLetterSpacing(2);
        fTitleGold = new Font().LoadedTexture(api, Sprite("fonts", "scarab_gold"), FontData.SCARAB).setLetterSpacing(2)
            .setFallbackColor(c_gold);
        fSubtitle = new Font().LoadedTexture(api, Sprite("fonts", "scarab_small"), FontData.SCARAB_SMALL)
            .setLetterSpacing(1);
        fSubtitleGold = new Font().LoadedTexture(api, Sprite("fonts", "scarab_small_gold"), FontData.SCARAB_SMALL)
            .setLetterSpacing(1).setFallbackColor(c_gold);

        useInternalTextDrawer = Lang.UsesNonLatinCharacters(Lang.CurrentLocale);
        if (!useInternalTextDrawer)
        {
            fTitle.baseLineHeight = ImGui.GetTextLineHeight();
            fTitleGold.baseLineHeight = ImGui.GetTextLineHeight();
            fSubtitle.baseLineHeight = ImGui.GetTextLineHeight();
            fSubtitleGold.baseLineHeight = ImGui.GetTextLineHeight();
        }

        tooltipVTML = new List<VTMLblock>();

        stopwatch = Stopwatch.StartNew();
        checkAPIID = api.Event.RegisterGameTickListener(OnCheckAPI, checkAPIInterval);
        checkLevelID = api.Event.RegisterGameTickListener(OnCheckLevel, checkLevelInterval);

        effectBox = new EffectBox(api);
    }

    public void OnCheckAPI(float dt)
    {
        if (GetSkillData()) isReady = true;
        if (isReady) api.Event.UnregisterGameTickListener(checkAPIID);
    }

    public void OnCheckLevel(float dt)
    {
        if (previousLevels == null) return;
        if (!config.lvPopupEnabled) return;

        foreach (var skill in previousLevels.Keys)
        {
            var currentLevel = skill.Level;

            if (currentLevel > previousLevels[skill])
            {
                LevelPopup levelPopup = new(api, skill);
                api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/levelup.ogg"), false, .3f);
                api.Logger.Debug($"{skill.Skill.Name}, {skill.Skill.Id} Level up");
            }

            previousLevels[skill] = currentLevel;
        }
    }

    private bool GetSkillData()
    {
        xLeveling = api.ModLoader.GetModSystem<XLeveling>();
        if (xLeveling == null) return false;

        xLevelingClient = xLeveling.IXLevelingAPI as XLevelingClient;
        if (xLevelingClient == null) return false;

        effectBox.xLeveling = xLeveling;
        effectBox.xLevelingClient = xLevelingClient;

        var playerSkillSet = xLevelingClient.LocalPlayerSkillSet;
        if (playerSkillSet == null) return false;

        skillGroups = new Dictionary<string, List<PlayerSkill>>();
        previousLevels = new Dictionary<PlayerSkill, int>();
        allSkills = new List<PlayerSkill>();
        specializeGroups = new List<PlayerAbility>();

        var firstGroup = true;
        foreach (var skill in playerSkillSet.PlayerSkills)
            if (skill.Skill.Enabled && !skill.Hidden && skill.PlayerAbilities.Count > 0)
            {
                var groupName = skill.Skill.Group;

                if (!skillGroups.ContainsKey(groupName))
                    skillGroups[groupName] = new List<PlayerSkill>();

                var groupList = skillGroups[groupName];
                groupList.Add(skill);
                allSkills.Add(skill);
                previousLevels[skill] = skill.Level;

                if (firstGroup)
                {
                    SetPage(groupName);
                    firstGroup = false;
                }

                foreach (var playerAbility in skill.PlayerAbilities)
                {
                    var ability = playerAbility.Ability;
                    foreach (var req in ability.Requirements)
                        if (IsAbilityLimited(req))
                        {
                            specializeGroups.Add(playerAbility);
                            break;
                        }
                }
            }

        return true;
    }

    private void SetPage(string page)
    {
        if (page == "_Specialize")
        {
            this.page = "_Specialize";
            metaPage = true;

            SetPageContentList(specializeGroups);
            return;
        }

        if (!skillGroups.ContainsKey(page)) return;

        metaPage = false;
        this.page = page;
        currentSkills = skillGroups[page];
        SetSkillPage(0);
    }

    private void SetSkillPage(int page)
    {
        if (page < 0 || page >= currentSkills.Count) return;
        skillPage = page;
        currentPlayerSkill = currentSkills[page];

        SetPageContent();
    }

    private void SetPageContent()
    {
        abilityButtons = new Dictionary<string, AbilityButton>();

        var pad = buttonPad;

        var levelTiers = new List<int>();
        var buttonTiers = new List<int>();

        foreach (var ability in currentPlayerSkill.PlayerAbilities)
        {
            if (!ability.IsVisible()) continue;
            var lv = ability.Ability.RequiredLevel(1);

            while (levelTiers.Count <= lv) levelTiers.Add(0);
            levelTiers[lv]++;
        }

        var levelTierMap = new Dictionary<int, int>();
        for (int i = 0, j = 0; i < levelTiers.Count; i++)
        {
            levelTierMap[i] = j;
            if (levelTiers[i] > 0) j++;
        }

        foreach (var ability in currentPlayerSkill.PlayerAbilities)
        {
            if (!ability.IsVisible()) continue;
            var name = ability.Ability.Name;

            var lv = ability.Ability.RequiredLevel(1);
            var tier = levelTierMap[lv];

            while (buttonTiers.Count <= tier) buttonTiers.Add(0);
            buttonTiers[tier]++;

            var button = new AbilityButton(ability);

            button.tier = tier;
            abilityButtons[name] = button;
        }

        var buttonTierMap = new Dictionary<int, int>();
        var tierX = new List<float>();

        for (int i = 0, j = 0; i < buttonTiers.Count; i++)
        {
            buttonTierMap[i] = j;
            if (buttonTiers[i] > 0) j++;
            tierX.Add(0);
        }

        foreach (var button in abilityButtons.Values)
        {
            var tier = buttonTierMap[button.tier];
            var roww = buttonTiers[button.tier];

            var _x = tierX[tier] - (roww - 1) / 2 * (buttonWidth + pad);
            var _y = -tier * (buttonHeight + pad);
            tierX[tier] += buttonWidth + pad;

            button.x = _x;
            button.y = _y;
        }

        float minx = 99999;
        float miny = 99999;
        float maxx = -99999;
        float maxy = -99999;

        foreach (var button in abilityButtons.Values)
        {
            minx = Math.Min(minx, button.x);
            miny = Math.Min(miny, button.y);

            maxx = Math.Max(maxx, button.x + buttonWidth);
            maxy = Math.Max(maxy, button.y + buttonHeight);
        }

        var cx = (minx + maxx) / 2;
        var cy = (miny + maxy) / 2;

        foreach (var button in abilityButtons.Values)
        {
            button.x -= cx;
            button.y -= cy;
        }

        _abilityPageWidth = maxx - minx;
        _abilityPageHeight = maxy - miny;

        levelRequirementBars = new List<float>();
        for (var i = 0; i < levelTiers.Count; i++)
            if (levelTiers[i] > 0)
                levelRequirementBars.Add(i);

        decorationLines = new List<DecorationLine>();

        foreach (var button in abilityButtons.Values)
        {
            var x0 = button.x;
            var y0 = button.y;

            foreach (var req in button.Ability.Ability.Requirements)
            {
                var req2 = req as ExclusiveAbilityRequirement;
                if (req2 != null)
                {
                    var name = req2.Ability.Name;
                    if (abilityButtons.ContainsKey(name))
                    {
                        var _button = abilityButtons[name];
                        var x1 = _button.x;
                        var y1 = _button.y;

                        decorationLines.Add(new DecorationLine(x0, y0, x1, y1,
                            new Vector4(165 / 255f, 98 / 255f, 67 / 255f, .5f)));
                    }
                }
            }
        }
    }

    private void SetPageContentList(List<PlayerAbility> abilityList)
    {
        abilityButtons = new Dictionary<string, AbilityButton>();
        levelRequirementBars.Clear();
        decorationLines.Clear();

        var pad = buttonPad;

        var amo = abilityList.Count;
        var col = (int)Math.Floor(Math.Sqrt(amo));
        var indx = 0;

        for (var i = 0; i < amo; i++)
        {
            var ability = abilityList[i];
            if (!ability.IsVisible()) continue;

            var c = indx % col;
            var r = indx / col;
            indx++;

            var name = ability.Ability.Name;
            var lv = ability.Ability.RequiredLevel(0);

            var button = new AbilityButton(ability);

            button.x = c * (buttonWidth + pad);
            button.y = r * (buttonHeight + pad);

            abilityButtons[name] = button;
        }

        float minx = 99999;
        float miny = 99999;
        float maxx = -99999;
        float maxy = -99999;

        foreach (var button in abilityButtons.Values)
        {
            minx = Math.Min(minx, button.x);
            miny = Math.Min(miny, button.y);

            maxx = Math.Max(maxx, button.x + buttonWidth);
            maxy = Math.Max(maxy, button.y + buttonHeight);
        }

        var cx = (minx + maxx) / 2;
        var cy = (miny + maxy) / 2;

        foreach (var button in abilityButtons.Values)
        {
            button.x -= cx;
            button.y -= cy;
        }

        _abilityPageWidth = maxx - minx;
        _abilityPageHeight = maxy - miny;
    }

    private CallbackGUIStatus Draw(float deltaSeconds)
    {
        if (!isOpen) return CallbackGUIStatus.Closed;

        // NEW: Check for Escape key press to close the window
        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Close();
            return CallbackGUIStatus.Closed;
        }
        
        var window = api.Gui.WindowBounds;
        var xPlatform = api.Forms;
        var size = xPlatform.GetScreenSize();

        uiScale = ClientSettings.GUIScale;

        if (!useInternalTextDrawer)
        {
            fTitle.baseScale = _ui(1);
            fTitleGold.baseScale = _ui(1);
            fSubtitle.baseScale = _ui(1);
            fSubtitleGold.baseScale = _ui(1);
        }

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);

        var windowWidth = Math.Min(windowBaseWidth, (int)window.OuterWidth - 128); // 160
        var windowHeight = Math.Min(windowBaseHeight, (int)window.OuterHeight - 128); // 160

        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight));
        // ImGui.SetNextWindowPos(new (windowX, windowY));
        ImGui.SetNextWindowPos(new Vector2(
            _viewPort.Pos.X + (_viewPort.Size.X - windowWidth) / 2,
            _viewPort.Pos.Y + (_viewPort.Size.Y - windowHeight) / 2
        ));
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground;

        ImGui.Begin("xSkill Gilded", flags);

        drawImage(Sprite("elements", "bg"), 0, 0, windowWidth, windowHeight);
        var padd = _ui(contentPadding);
        var contentWidth = windowWidth - _ui(tooltipWidth) - padd * 2;
        var deltaTime = stopwatch.ElapsedMilliseconds / 1000f;
        stopwatch.Restart();

        string _hoveringID = null;

        var bty = DrawSkillGroupTab(padd, windowWidth, ref _hoveringID);
        var bth = _ui(32);

        var sky = DrawSkillsTab(padd, bty, bth, windowWidth, ref _hoveringID);
        var skh = _ui(32);

        DrawAbility(padd, sky, skh, contentWidth, windowHeight, deltaTime, flags);

        DrawSkillsDescription(padd, sky, skh);

        DrawSkillsActions(padd, windowHeight, ref _hoveringID);

        DrawTooltip(padd, sky, skh, windowWidth, windowHeight);

        // if(_hoveringID != null && _hoveringID != hoveringID) api.Gui.PlaySound("tick", false, .5f); // too annoying
        hoveringID = _hoveringID;

        drawImage9patch(Sprite("elements", "frame"), 0, 0, windowWidth, windowHeight, 60);

        ImGui.End();

        return CallbackGUIStatus.GrabMouse;
    }

    private float DrawSkillGroupTab(float padd, int windowWidth, ref string _hoveringID)
    {
        var btx = padd;
        var bty = padd;
        var bth = _ui(32);

        var _btsw = _ui(96);
        var btxc = btx + _btsw / 2;
        var btww = _btsw * .5f / 2;
        var _alpha = 1f;

        if (page == "_Specialize")
        {
            drawImage(Sprite("elements", "tab_sep_selected"), btxc - btww, bty + bth - 4, btww * 2, 4);
            _alpha = 1f;
        }
        else if (mouseHover(btx, bty, btx + _btsw, bty + bth))
        {
            _hoveringID = "_Specialize";
            drawImage(Sprite("elements", "tab_sep_hover"), btxc - btww, bty + bth - 4, btww * 2, 4);
            _alpha = 1f;
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                SetPage("_Specialize");
                api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/page.ogg"), false, .3f);
            }
        }
        else
        {
            drawImage(Sprite("elements", "tab_sep"), btxc - btww, bty + bth - 4, btww * 2, 4);
            _alpha = .5f;
        }

        drawSetColor(c_white, _alpha);
        drawImage(page == "_Specialize" ? Sprite("elements", "meta_spec_selected") : Sprite("elements", "meta_spec"),
            btxc - _ui(24 / 2), bty + 4, _ui(24), _ui(24));
        drawSetColor(c_white);
        btx += _btsw;

        var btw = (windowWidth - padd - btx) / skillGroups.Count;

        foreach (var groupName in skillGroups.Keys)
        {
            btxc = btx + btw / 2;
            btww = btw * .5f / 2;
            var alpha = 1f;
            var _fTitle = fTitle;

            var points = 0;
            foreach (var skill in skillGroups[groupName]) points += skill.AbilityPoints;

            if (groupName == page)
            {
                drawImage(Sprite("elements", "tab_sep_selected"), btxc - btww, bty + bth - 4, btww * 2, 4);
                _fTitle = fTitleGold;
            }
            else if (mouseHover(btx, bty, btx + btw, bty + bth))
            {
                _hoveringID = groupName;
                drawImage(Sprite("elements", "tab_sep_hover"), btxc - btww, bty + bth - 4, btww * 2, 4);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    SetPage(groupName);
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

    private float DrawSkillsTab(float padd, float bty, float bth, int windowWidth, ref string _hoveringID)
    {
        var skx = padd;
        var sky = bty + bth + _ui(4);
        var skw = (windowWidth - padd * 2) / currentSkills.Count;
        var skh = _ui(32);

        if (!metaPage)
            for (var i = 0; i < currentSkills.Count; i++)
            {
                var skill = currentSkills[i];
                var skillName = skill.Skill.DisplayName;
                var skxc = skx + skw / 2;
                var skww = skw * .5f / 2;
                var color = new Vector4(1, 1, 1, 1);
                var _fTitle = fSubtitle;

                if (i != skillPage)
                {
                    if (mouseHover(skx, sky, skx + skw, sky + skh))
                    {
                        _hoveringID = skillName;
                        drawImage(Sprite("elements", "tab_sep_hover"), skxc - skww, sky + skh - 4, skww * 2, 4);
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            SetSkillPage(i);
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

    private void DrawAbility(float padd, float sky, float skh, float contentWidth, int windowHeight, float deltaTime,
        ImGuiWindowFlags flags)
    {
        var abx = padd;
        var aby = sky + skh + _ui(8);
        var abw = contentWidth - abx - _ui(8);
        var abh = windowHeight - aby - _ui(8);
        var bw = _ui(buttonWidth);
        var bh = _ui(buttonHeight);

        var padX = Math.Max(0, _ui(_abilityPageWidth) - abw + _ui(128));
        var padY = Math.Max(0, _ui(_abilityPageHeight) - abh + _ui(128));

        var mx = ImGui.GetMousePos().X;
        var my = ImGui.GetMousePos().Y;

        var mrx = (mx - (_viewPort.Pos.X + abx)) / abw - .5f;
        var mry = (my - (_viewPort.Pos.Y + aby)) / abh - .5f;

        var ofmx = (float)Math.Round(-padX * mrx);
        var ofmy = (float)Math.Round(-padY * mry);

        windowPosX = windowX + abx;
        windowPosY = windowY + aby;
        ImGui.SetCursorPos(new Vector2(abx, aby));
        ImGui.BeginChild("Ability", new Vector2(abw, abh), false, flags);
        var offx = ofmx + abw / 2;
        var offy = ofmy + abh / 2;
        AbilityButton _hoveringButton = null;

        var lvx = _ui(64);

        for (var i = 1; i < levelRequirementBars.Count; i++)
        {
            var lv = levelRequirementBars[i];
            var _y = offy + _ui(_abilityPageHeight / 2 - i * (buttonHeight + buttonPad) + buttonPad / 2);

            if (mouseHover(lvx, _y - buttonHeight - buttonPad, lvx + abw, _y))
                drawSetColor(new Vector4(239 / 255f, 183 / 255f, 117 / 255f, 1));
            else
                drawSetColor(new Vector4(104 / 255f, 76 / 255f, 60 / 255f, 1));

            var lvReqText = $"Level {lv}";
            drawImage(Sprite("elements", "level_sep"), lvx, _y - _ui(64), abw - _ui(128), _ui(64));
            drawTextFont(fSubtitle, lvReqText, lvx + _ui(32), _y - _ui(2), HALIGN.Left, VALIGN.Bottom);
        }

        drawSetColor(c_white);

        foreach (var line in decorationLines)
        {
            drawSetColor(line.color);

            if (line.y0 == line.y1)
            {
                var _x0 = offx + _ui(Math.Min(line.x0, line.x1)) + bw;
                var _x1 = offx + _ui(Math.Max(line.x0, line.x1));

                drawImage(Sprite("elements", "pixel"), _x0, offy + _ui(line.y0 + bh / 2 - 10), _x1 - _x0, _ui(20));
            }
        }

        drawSetColor(c_white);

        foreach (var button in abilityButtons.Values)
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

        if (_hoveringButton != null && hoveringButton != _hoveringButton)
            api.Gui.PlaySound("tick", false, .5f);
        hoveringButton = _hoveringButton;
        if (hoveringButton != null)
        {
            var ability = hoveringButton.Ability;
            var bx = _ui(hoveringButton.x) + offx;
            var by = _ui(hoveringButton.y) + offy;
            var c = hoveringButton.drawColor;

            drawSetColor(new Vector4(c.X, c.Y, c.Z, .5f));
            drawImage9patch(Sprite("elements", "abilitybox_frame_selected"), bx - 16, by - 16, bw + 32, bh + 32, 30);
            drawSetColor(c_white);

            var requirements = ability.Ability.Requirements;
            foreach (var req in requirements)
                drawRequirementHighlight(hoveringButton, req, offx, offy);
        }

        ImGui.EndChild();
        windowPosX = windowX;
        windowPosY = windowY;
    }

    private void DrawSkillsDescription(float padd, float sky, float skh)
    {
        var sdx = padd + _ui(16);
        var sdy = sky + skh + _ui(16);
        var sdw = _ui(200);

        if (page == "_Specialize")
        {
            var skillTitle = Lang.GetUnformatted("xlib:specialisations");
            var skillTitle_size = drawTextFont(fTitleGold, skillTitle, sdx, sdy);
            sdy += fTitleGold.getLineHeight() + _ui(8);

            foreach (var skill in allSkills)
            {
                var hh = drawSkillLevelDetail(skill, sdx, sdy, sdw, false);
                sdy += hh;
            }
        }
        else
        {
            var hh = drawSkillLevelDetail(currentPlayerSkill, sdx, sdy, sdw, true);
            sdy += hh;

            var unlearnPoint = currentPlayerSkill.PlayerSkillSet.UnlearnPoints;
            float unlearnPointReq = xLevelingClient.GetPointsForUnlearn();
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

            var unlearnCooldown = currentPlayerSkill.PlayerSkillSet.UnlearnCooldown;
            var unlearnCooldownMax = xLevelingClient.Config.unlearnCooldown;
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

    private void DrawSkillsActions(float padd, int windowHeight, ref string _hoveringID)
    {
        var actx = padd + _ui(8);
        var acty = windowHeight - padd - _ui(8);

        var actbw = _ui(96);
        var actbh = _ui(96);
        var actbx = actx;
        var actby = acty - actbh;
        var actLh = _ui(24);
        var isSparing = xLevelingClient.LocalPlayerSkillSet.Sparring;

        drawSetColor(new Vector4(1, 1, 1, isSparing ? 1 : .5f));
        drawImage(Sprite("elements", isSparing ? "sparring_enabled" : "sparring_disabled"),
            actbx + actbw / 2 - _ui(96) / 2, actby + actbh - _ui(96), _ui(96), _ui(96));
        drawSetColor(c_white);

        drawImage9patch(Sprite("elements", "button_idle"), actbx, actby + actbh - actLh, actbw, actLh, 2);
        if (mouseHover(actbx, actby, actbx + actbw, actby + actbh))
        {
            _hoveringID = "Sparring";
            drawImage9patch(Sprite("elements", "button_idle_hovering"), actbx - 1, actby + actbh - actLh - 1, actbw + 2,
                actLh + 2, 2);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                OnSparringToggle(!isSparing);
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

    private void DrawTooltip(float padd, float sky, float skh, int windowWidth, int windowHeight)
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

            // float h = drawTextWrap(hoveringTooltip.Description, tooltipX + 8, tooltipY, HALIGN.Left, VALIGN.Top, tooltipW - 16);
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

            var descCurrTier = formatAbilityDescription(ability.Ability, tier);
            // float h = drawTextWrap(descCurrTier, tooltipX + 8, tooltipY, HALIGN.Left, VALIGN.Top, tooltipW - 16);
            if (currentTooltip != descCurrTier)
            {
                tooltipVTML = VTML.parseVTML(descCurrTier);
                currentTooltip = descCurrTier;
            }

            var h = drawTextVTML(tooltipVTML, tooltipX + _ui(8), tooltipY, tooltipW - _ui(16));
            tooltipY += Math.Max(h + _ui(16), _ui(160));

            drawSetColor(new Vector4(104 / 255f, 76 / 255f, 60 / 255f, 1));
            drawImage(Sprite("elements", "tooltip_sep"), tooltipX + _ui(8), tooltipY, tooltipW - _ui(16), 1);
            drawSetColor(c_white);
            tooltipY += _ui(16);

            if (tier < tierMax)
            {
                var requiredLevel = ability.Ability.RequiredLevel(tier + 1);
                var reqText = string.Format(Lang.GetUnformatted("xskillgilded:abilityLevelRequired"), skillName,
                    requiredLevel);

                drawSetColor(currentPlayerSkill.Level >= requiredLevel ? c_lime : c_red);
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

        hoveringTooltip = null;
    }

    private string formatAbilityDescription(Ability ability, int currTier)
    {
        var descBase = ability.Description.Replace("%", "%%");
        descBase = descBase.Replace("\n", "<br>");
        var percentageValues = new HashSet<int>();

        Regex percentRx = new(@"{(\d)}%%", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var matches = percentRx.Matches(descBase);
        foreach (Match match in matches)
        {
            var index = int.Parse(match.Groups[1].Value);
            percentageValues.Add(index);
            descBase = descBase.Replace(match.Value, match.Value.Replace("%", ""));
        }

        var values = ability.Values;
        var valueCount = values.Length;

        var vpt = ability.ValuesPerTier;
        var begin = vpt * (currTier - 1);
        var next = begin + vpt;

        var v = new string[vpt];
        for (var i = 0; i < vpt; i++)
        {
            var str = "";

            if (begin + i >= 0 && begin + i < valueCount)
            {
                var _v = values[begin + i].ToString();
                if (percentageValues.Contains(i)) _v += "%%";

                str += $"<font color=\"#feae34\">{_v}</font>";
            }

            if (next + i < valueCount)
            {
                if (str.Length > 0) str += " > ";

                var _v = values[next + i].ToString();
                if (percentageValues.Contains(i)) _v += "%%";

                str += $"<font color=\"#7ac62f\">{_v}</font>";
            }

            v[i] = str;
        }

        try
        {
            switch (vpt)
            {
                case 1: return string.Format(descBase, v[0]);
                case 2: return string.Format(descBase, v[0], v[1]);
                case 3: return string.Format(descBase, v[0], v[1], v[2]);
                case 4: return string.Format(descBase, v[0], v[1], v[2], v[3]);
                case 5: return string.Format(descBase, v[0], v[1], v[2], v[3], v[4]);
                case 6: return string.Format(descBase, v[0], v[1], v[2], v[3], v[4], v[5]);
                case 7: return string.Format(descBase, v[0], v[1], v[2], v[3], v[4], v[5], v[6]);
                case 8: return string.Format(descBase, v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7]);
            }
        }
        catch
        {
            return descBase;
        }

        return descBase;
    }

    private float drawSkillLevelDetail(PlayerSkill skill, float x, float y, float w, bool title)
    {
        var ys = y;
        var sx = x;

        var skillTitle = skill.Skill.DisplayName;
        //LoadedTexture skillIcon = Sprite("skillicon", skill.Skill.Name);
        //if(skillIcon.TextureId != 0) {
        //    drawSetColor(c_grey, .1f);
        //    drawImage(skillIcon, sx - _ui(8), y - _ui(16), _ui(64), _ui(64));
        //    drawSetColor(c_white);
        //}

        var skillTitle_size = drawTextFont(title ? fTitleGold : fSubtitleGold, skillTitle, sx, y);

        if (!title)
        {
            var abilityPoint = skill.AbilityPoints;
            var skillPointTitle = abilityPoint.ToString();

            var unlearnPoint = currentPlayerSkill.PlayerSkillSet.UnlearnPoints;
            float unlearnPointReq = xLevelingClient.GetPointsForUnlearn();
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

    private void drawRequirementHighlight(AbilityButton button, Requirement requirement, float offx, float offy)
    {
        var ability = button.Ability;
        var isFulfilled = requirement.IsFulfilled(ability, ability.Tier + 1);

        var bx = _ui(button.x) + offx;
        var by = _ui(button.y) + offy;
        var bw = _ui(buttonWidth);
        var bh = _ui(buttonHeight);

        var abilityRequirement = requirement as AbilityRequirement;
        if (abilityRequirement != null)
        {
            var name = abilityRequirement.Ability.Name;
            if (abilityButtons.ContainsKey(name))
            {
                var _button = abilityButtons[name];

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
                drawRequirementHighlight(button, _req, offx, offy);

        var orRequirement = requirement as OrRequirement;
        if (orRequirement != null)
            foreach (var _req in orRequirement.Requirements)
                drawRequirementHighlight(button, _req, offx, offy);

        var exclusiveAbilityRequirement = requirement as ExclusiveAbilityRequirement;
        if (exclusiveAbilityRequirement != null)
        {
            var name = exclusiveAbilityRequirement.Ability.Name;
            if (abilityButtons.ContainsKey(name))
            {
                var _button = abilityButtons[name];

                var _bx = _ui(_button.x) + offx;
                var _by = _ui(_button.y) + offy;

                drawSetColor(new Vector4(c_red.X, c_red.Y, c_red.Z, .9f));
                drawImage9patch(Sprite("elements", "abilitybox_frame_selected"), _bx - 16, _by - 16, bw + 32, bh + 32,
                    30);
                drawSetColor(c_white);
            }
        }
    }

    private bool IsAbilityLimited(Requirement Requirement)
    {
        var limitation = Requirement as LimitationRequirement;
        if (limitation != null) return true;

        var and = Requirement as AndRequirement;
        if (and != null)
            foreach (var req in and.Requirements)
                if (IsAbilityLimited(req))
                    return true;

        var not = Requirement as NotRequirement;
        if (not != null)
            if (IsAbilityLimited(not.Requirement))
                return true;

        return false;
    }

    private void OnSparringToggle(bool toggle)
    {
        xLevelingClient.LocalPlayerSkillSet.Sparring = toggle;
        var package = new CommandPackage(EnumXLevelingCommand.SparringMode, toggle ? 1 : 0);
        xLevelingClient.SendPackage(package);
    }

    private void Open()
    {
        if (isOpen) return;

        if (!isReady)
        {
            OnCheckAPI(0);
            if (!isReady) return;
        }

        isOpen = true;
        imguiModSystem.Show();
        api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/open.ogg"), false, .3f);
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/close.ogg"), false, .3f);
    }

    private bool Toggle(KeyCombination _)
    {
        if (isOpen) Close();
        else Open();
        return true;
    }

    public override void Dispose()
    {
        base.Dispose();

        api.Event.UnregisterGameTickListener(checkLevelID);
        // imguiModSystem.Draw   -= Draw;
        // imguiModSystem.Closed -= Close;
    }
}

internal class AbilityButton
{
    public Vector4 drawColor;
    public float drawTierWidth;

    public float glowAlpha;

    public int tier = -1;

    public AbilityButton(PlayerAbility ability)
    {
        Ability = ability;
        RawName = ability.Ability.Name;
        Name = ability.Ability.DisplayName;

        var _icoPath = $"xskillgilded:textures/gui/skilltree/abilityicon/{RawName}.png";
        Texture = resourceLoader.Sprite(_icoPath);
    }

    public string RawName { get; set; }
    public string Name { get; set; }
    public LoadedTexture Texture { get; set; }
    public PlayerAbility Ability { get; set; }
    public List<VTMLblock> Description { get; set; }

    public float x { get; set; }
    public float y { get; set; }
}

internal class DecorationLine
{
    public Vector4 color;

    public DecorationLine(float x0, float y0, float x1, float y1, Vector4 color)
    {
        this.x0 = x0;
        this.y0 = y0;
        this.x1 = x1;
        this.y1 = y1;
        this.color = color;
    }

    public float x0 { get; set; }
    public float y0 { get; set; }
    public float x1 { get; set; }
    public float y1 { get; set; }
}

internal class TooltipObject
{
    public TooltipObject(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; set; }
    public string Description { get; set; }
}