using System.IO;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace xSkillGilded;

internal static class resourceLoader
{
    public static ICoreClientAPI api;

    public static void setApi(ICoreClientAPI _api)
    {
        api = _api;
    }

    public static LoadedTexture Sprite(string name)
    {
        LoadedTexture tex = new(api);
        api.Render.GetOrLoadTexture(name, ref tex);
        return tex;
    }

    public static ImFontPtr loadFont(string path)
    {
        var _fpath = Path.Combine(GamePaths.AssetsPath, "xskillgilded", "fonts", "scarab.ttf");
        var io = ImGui.GetIO();
        var f = io.Fonts.AddFontFromFileTTF(_fpath, 24);
        return f;
    }
}