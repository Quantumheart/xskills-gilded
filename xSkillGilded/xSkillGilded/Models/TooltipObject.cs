namespace xSkillGilded.Models;

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
