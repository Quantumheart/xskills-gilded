using System.Collections.Generic;
using System.Numerics;
using Vintagestory.API.Client;
using XLib.XLeveling;

namespace xSkillGilded.Models;

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
