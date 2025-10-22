using System.Numerics;

namespace xSkillGilded.Models;

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
