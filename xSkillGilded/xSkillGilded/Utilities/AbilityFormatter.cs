using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using XLib.XLeveling;

namespace xSkillGilded.Utilities;

internal static class AbilityFormatter
{
    public static string FormatAbilityDescription(Ability ability, int currTier)
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
}
