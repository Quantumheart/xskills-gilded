using XLib.XLeveling;

namespace xSkillGilded.Utilities;

internal static class RequirementHelper
{
    public static bool IsAbilityLimited(Requirement requirement)
    {
        var limitation = requirement as LimitationRequirement;
        if (limitation != null) return true;

        var and = requirement as AndRequirement;
        if (and != null)
            foreach (var req in and.Requirements)
                if (IsAbilityLimited(req))
                    return true;

        var not = requirement as NotRequirement;
        if (not != null)
            if (IsAbilityLimited(not.Requirement))
                return true;

        return false;
    }
}
