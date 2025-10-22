using System;
using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;
using VSImGui;
using VSImGui.API;
using XLib.XLeveling;
using xSkillGilded.Managers;
using xSkillGilded.UI;
using static xSkillGilded.ImGuiUtil;

namespace xSkillGilded;

public class xSkillGraphicalUI : ModSystem
{
    public const string configFileName = "xskillsgilded.json";

    private const int checkAPIInterval = 1000;
    private const int checkLevelInterval = 100;
    public static ModConfig config;

    private ImGuiViewportPtr _viewPort;

    private ICoreClientAPI api;
    private long checkAPIID, checkLevelID;
    private EffectBox effectBox;

    private ImGuiModSystem imguiModSystem;
    public bool isOpen;
    private bool isReady;

    private SkillPageManager pageManager;
    private SkillUIRenderer uiRenderer;
    private Stopwatch stopwatch;

    private readonly int windowBaseHeight = 1060;
    private readonly int windowBaseWidth = 1800;

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

        pageManager = new SkillPageManager();
        stopwatch = Stopwatch.StartNew();
        checkAPIID = api.Event.RegisterGameTickListener(OnCheckAPI, checkAPIInterval);
        checkLevelID = api.Event.RegisterGameTickListener(OnCheckLevel, checkLevelInterval);

        effectBox = new EffectBox(api);
    }

    public void OnCheckAPI(float dt)
    {
        if (pageManager.GetSkillData(api))
        {
            isReady = true;
            effectBox.xLeveling = pageManager.XLeveling;
            effectBox.xLevelingClient = pageManager.XLevelingClient;
            uiRenderer = new SkillUIRenderer(api, pageManager, _viewPort);
        }

        if (isReady) api.Event.UnregisterGameTickListener(checkAPIID);
    }

    public void OnCheckLevel(float dt)
    {
        if (pageManager.PreviousLevels == null) return;
        if (!config.lvPopupEnabled) return;

        foreach (var skill in pageManager.PreviousLevels.Keys)
        {
            var currentLevel = skill.Level;

            if (currentLevel > pageManager.PreviousLevels[skill])
            {
                LevelPopup levelPopup = new(api, skill);
                api.Gui.PlaySound(new AssetLocation("xskillgilded", "sounds/levelup.ogg"), false, .3f);
                api.Logger.Debug($"{skill.Skill.Name}, {skill.Skill.Id} Level up");
            }

            pageManager.PreviousLevels[skill] = currentLevel;
        }
    }

    private CallbackGUIStatus Draw(float deltaSeconds)
    {
        if (!isOpen) return CallbackGUIStatus.Closed;

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

        var windowWidth = Math.Min(windowBaseWidth, (int)window.OuterWidth - 128);
        var windowHeight = Math.Min(windowBaseHeight, (int)window.OuterHeight - 128);

        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight));
        ImGui.SetNextWindowPos(new Vector2(
            _viewPort.Pos.X + (_viewPort.Size.X - windowWidth) / 2,
            _viewPort.Pos.Y + (_viewPort.Size.Y - windowHeight) / 2
        ));
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground;

        ImGui.Begin("xSkill Gilded", flags);

        Vector2 windowPos = ImGui.GetWindowPos();
        windowPosX = windowPos.X;
        windowPosY = windowPos.Y;

        drawImage(Sprite("elements", "bg"), 0, 0, windowWidth, windowHeight);

        var deltaTime = stopwatch.ElapsedMilliseconds / 1000f;
        stopwatch.Restart();

        uiRenderer.Draw(windowWidth, windowHeight, deltaTime, flags, OnSparringToggle);

        drawImage9patch(Sprite("elements", "frame"), 0, 0, windowWidth, windowHeight, 60);

        ImGui.End();

        return CallbackGUIStatus.GrabMouse;
    }

    private void OnSparringToggle(bool toggle)
    {
        pageManager.XLevelingClient.LocalPlayerSkillSet.Sparring = toggle;
        var package = new CommandPackage(EnumXLevelingCommand.SparringMode, toggle ? 1 : 0);
        pageManager.XLevelingClient.SendPackage(package);
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
    }
}
