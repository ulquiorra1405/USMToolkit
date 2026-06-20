using Microsoft.Win32;

namespace Toolkit.Services;

public class RegistryService
{
    private RegistryKey? OpenBaseKey(string path)
    {
        var parts = path.Split('\\');
        if (parts.Length < 2) return null;

        var hive = parts[0].ToUpperInvariant() switch
        {
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_USERS" or "HKU" => Registry.Users,
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
            _ => null
        };

        if (hive == null) return null;

        var subKey = string.Join("\\", parts.Skip(1));
        try { return hive.OpenSubKey(subKey, true); }
        catch { return null; }
    }

    public bool TryReadValue(string path, string valueName, out object? value)
    {
        value = null;
        try
        {
            using var key = OpenBaseKey(path);
            if (key == null) return false;
            value = key.GetValue(valueName);
            return true;
        }
        catch { return false; }
    }

    public bool TryWriteValue(string path, string valueName, object value)
    {
        try
        {
            using var key = OpenBaseKey(path);
            if (key == null) return false;
            key.SetValue(valueName, value);
            return true;
        }
        catch { return false; }
    }
}
