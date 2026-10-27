using System.Text.RegularExpressions;

namespace CtrNfSave.Updates;

public sealed record UpdateVersion(int Major, int Minor, int Patch, string? Suffix) : IComparable<UpdateVersion>
{
    public static bool TryParse(string? text, out UpdateVersion version)
    {
        version = new(0, 0, 0, null);
        var match = Regex.Match(text ?? "", @"^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*))?\z", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int major) || !int.TryParse(match.Groups[2].Value, out int minor) || !int.TryParse(match.Groups[3].Value, out int patch)) return false;
        version = new(major, minor, patch, match.Groups[4].Success ? match.Groups[4].Value : null); return true;
    }
    public int CompareTo(UpdateVersion? other)
    {
        if (other is null) return 1;
        int result = Major.CompareTo(other.Major);
        if (result == 0) result = Minor.CompareTo(other.Minor);
        if (result == 0) result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;
        if (Suffix is null || other.Suffix is null) return Suffix is null ? other.Suffix is null ? 0 : 1 : -1;
        var left = Suffix.Split('.'); var right = other.Suffix.Split('.');
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            bool a = ulong.TryParse(left[i], out ulong x), b = ulong.TryParse(right[i], out ulong y);
            result = a && b ? x.CompareTo(y) : a != b ? a ? -1 : 1 : string.CompareOrdinal(left[i], right[i]);
            if (result != 0) return result;
        }
        return left.Length.CompareTo(right.Length);
    }
    public override string ToString() => $"{Major}.{Minor}.{Patch}" + (Suffix is null ? "" : "-" + Suffix);
}
