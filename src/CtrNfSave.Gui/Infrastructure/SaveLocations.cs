namespace CtrNfSave.Gui;

/// <summary>Read-only discovery of launcher save folders; never selects between multiple profiles.</summary>
internal static class SaveLocations
{
    public static string RelativeProfilePath(string? profileRoot)
    {
        var region = profileRoot is null ? null : new DirectoryInfo(profileRoot);
        if (region?.Parent?.Parent?.Name.Equals("savedata", StringComparison.OrdinalIgnoreCase) == true &&
            region.Parent.Parent.Parent?.Name.Equals("user", StringComparison.OrdinalIgnoreCase) == true)
            return Path.Combine("user", "savedata", region.Parent.Name, region.Name);
        return @"user\savedata\<user>\<CUSA…>";
    }

    public static string? FindProfileRoot(string? currentFile)
    {
        if (currentFile is not null)
        {
            var folder = new FileInfo(currentFile).Directory;
            if (folder?.Name.StartsWith("savedata", StringComparison.OrdinalIgnoreCase) == true &&
                folder.Parent?.Name.StartsWith("CUSA", StringComparison.OrdinalIgnoreCase) == true)
                return folder.Parent.FullName;
        }
        var profiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var examined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (DirectoryInfo? parent = new(AppContext.BaseDirectory); parent is not null; parent = parent.Parent)
            foreach (string root in new[] { Path.Combine(parent.FullName, "user", "savedata"), Path.Combine(parent.FullName, "CTR Nitro Fueled", "user", "savedata") })
            {
                if (!examined.Add(root) || !Directory.Exists(root)) continue;
                try
                {
                    foreach (string user in Directory.EnumerateDirectories(root))
                        foreach (string region in Directory.EnumerateDirectories(user, "CUSA*"))
                            profiles.Add(region);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        return profiles.Count == 1 ? profiles.Single() : null;
    }
    public static string? InitialFolder(string? profileRoot, bool global)
    {
        if (profileRoot is null) return null;
        string folder = global ? Path.Combine(profileRoot, "savedata0") : profileRoot;
        return Directory.Exists(folder) ? folder : Directory.Exists(profileRoot) ? profileRoot : null;
    }
}
