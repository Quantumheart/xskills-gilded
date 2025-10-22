using System;
using System.Collections.Generic;
using System.Numerics;
using Vintagestory.API.Client;
using XLib.XLeveling;
using xSkillGilded.Models;
using xSkillGilded.Utilities;
using AbilityButton = xSkillGilded.Models.AbilityButton;

namespace xSkillGilded.Managers;

internal class SkillPageManager
{
    private readonly float buttonHeight = 100;
    private readonly float buttonPad = 16;
    private readonly float buttonWidth = 128;

    public float AbilityPageHeight { get; private set; }
    public float AbilityPageWidth { get; private set; }

    public Dictionary<string, AbilityButton> AbilityButtons { get; private set; }
    public List<PlayerSkill> AllSkills { get; private set; }
    public PlayerSkill CurrentPlayerSkill { get; private set; }
    public List<PlayerSkill> CurrentSkills { get; private set; }
    public List<DecorationLine> DecorationLines { get; private set; }
    public List<float> LevelRequirementBars { get; private set; }
    public bool MetaPage { get; private set; }
    public string Page { get; private set; } = "";
    public Dictionary<PlayerSkill, int> PreviousLevels { get; private set; }
    public Dictionary<string, List<PlayerSkill>> SkillGroups { get; private set; }
    public int SkillPage { get; private set; }
    public List<PlayerAbility> SpecializeGroups { get; private set; }

    public XLeveling XLeveling { get; private set; }
    public XLevelingClient XLevelingClient { get; private set; }

    public bool GetSkillData(ICoreClientAPI api)
    {
        XLeveling = api.ModLoader.GetModSystem<XLeveling>();
        if (XLeveling == null) return false;

        XLevelingClient = XLeveling.IXLevelingAPI as XLevelingClient;
        if (XLevelingClient == null) return false;

        var playerSkillSet = XLevelingClient.LocalPlayerSkillSet;
        if (playerSkillSet == null) return false;

        SkillGroups = new Dictionary<string, List<PlayerSkill>>();
        PreviousLevels = new Dictionary<PlayerSkill, int>();
        AllSkills = new List<PlayerSkill>();
        SpecializeGroups = new List<PlayerAbility>();

        var firstGroup = true;
        foreach (var skill in playerSkillSet.PlayerSkills)
            if (skill.Skill.Enabled && !skill.Hidden && skill.PlayerAbilities.Count > 0)
            {
                var groupName = skill.Skill.Group;

                if (!SkillGroups.ContainsKey(groupName))
                    SkillGroups[groupName] = new List<PlayerSkill>();

                var groupList = SkillGroups[groupName];
                groupList.Add(skill);
                AllSkills.Add(skill);
                PreviousLevels[skill] = skill.Level;

                if (firstGroup)
                {
                    SetPage(groupName);
                    firstGroup = false;
                }

                foreach (var playerAbility in skill.PlayerAbilities)
                {
                    var ability = playerAbility.Ability;
                    foreach (var req in ability.Requirements)
                        if (RequirementHelper.IsAbilityLimited(req))
                        {
                            SpecializeGroups.Add(playerAbility);
                            break;
                        }
                }
            }

        return true;
    }

    public void SetPage(string page)
    {
        if (page == "_Specialize")
        {
            Page = "_Specialize";
            MetaPage = true;

            SetPageContentList(SpecializeGroups);
            return;
        }

        if (!SkillGroups.ContainsKey(page)) return;

        MetaPage = false;
        Page = page;
        CurrentSkills = SkillGroups[page];
        SetSkillPage(0);
    }

    public void SetSkillPage(int page)
    {
        if (page < 0 || page >= CurrentSkills.Count) return;
        SkillPage = page;
        CurrentPlayerSkill = CurrentSkills[page];

        SetPageContent();
    }

    private void SetPageContent()
    {
        AbilityButtons = new Dictionary<string, AbilityButton>();

        var pad = buttonPad;

        var levelTiers = new List<int>();
        var buttonTiers = new List<int>();

        foreach (var ability in CurrentPlayerSkill.PlayerAbilities)
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

        foreach (var ability in CurrentPlayerSkill.PlayerAbilities)
        {
            if (!ability.IsVisible()) continue;
            var name = ability.Ability.Name;

            var lv = ability.Ability.RequiredLevel(1);
            var tier = levelTierMap[lv];

            while (buttonTiers.Count <= tier) buttonTiers.Add(0);
            buttonTiers[tier]++;

            var button = new AbilityButton(ability);

            button.tier = tier;
            AbilityButtons[name] = button;
        }

        var buttonTierMap = new Dictionary<int, int>();
        var tierX = new List<float>();

        for (int i = 0, j = 0; i < buttonTiers.Count; i++)
        {
            buttonTierMap[i] = j;
            if (buttonTiers[i] > 0) j++;
            tierX.Add(0);
        }

        foreach (var button in AbilityButtons.Values)
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

        foreach (var button in AbilityButtons.Values)
        {
            minx = Math.Min(minx, button.x);
            miny = Math.Min(miny, button.y);

            maxx = Math.Max(maxx, button.x + buttonWidth);
            maxy = Math.Max(maxy, button.y + buttonHeight);
        }

        var cx = (minx + maxx) / 2;
        var cy = (miny + maxy) / 2;

        foreach (var button in AbilityButtons.Values)
        {
            button.x -= cx;
            button.y -= cy;
        }

        AbilityPageWidth = maxx - minx;
        AbilityPageHeight = maxy - miny;

        LevelRequirementBars = new List<float>();
        for (var i = 0; i < levelTiers.Count; i++)
            if (levelTiers[i] > 0)
                LevelRequirementBars.Add(i);

        DecorationLines = new List<DecorationLine>();

        foreach (var button in AbilityButtons.Values)
        {
            var x0 = button.x;
            var y0 = button.y;

            foreach (var req in button.Ability.Ability.Requirements)
            {
                var req2 = req as ExclusiveAbilityRequirement;
                if (req2 != null)
                {
                    var name = req2.Ability.Name;
                    if (AbilityButtons.ContainsKey(name))
                    {
                        var _button = AbilityButtons[name];
                        var x1 = _button.x;
                        var y1 = _button.y;

                        DecorationLines.Add(new DecorationLine(x0, y0, x1, y1,
                            new Vector4(165 / 255f, 98 / 255f, 67 / 255f, .5f)));
                    }
                }
            }
        }
    }

    private void SetPageContentList(List<PlayerAbility> abilityList)
    {
        AbilityButtons = new Dictionary<string, AbilityButton>();
        LevelRequirementBars = new List<float>();
        DecorationLines = new List<DecorationLine>();

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

            AbilityButtons[name] = button;
        }

        float minx = 99999;
        float miny = 99999;
        float maxx = -99999;
        float maxy = -99999;

        foreach (var button in AbilityButtons.Values)
        {
            minx = Math.Min(minx, button.x);
            miny = Math.Min(miny, button.y);

            maxx = Math.Max(maxx, button.x + buttonWidth);
            maxy = Math.Max(maxy, button.y + buttonHeight);
        }

        var cx = (minx + maxx) / 2;
        var cy = (miny + maxy) / 2;

        foreach (var button in AbilityButtons.Values)
        {
            button.x -= cx;
            button.y -= cy;
        }

        AbilityPageWidth = maxx - minx;
        AbilityPageHeight = maxy - miny;
    }
}
