using System.Collections.ObjectModel;

namespace Toolkit.Models;

public class SettingItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsRegistry { get; set; }
    public string RegistryPath { get; set; } = "";
    public string RegistryValue { get; set; } = "";
    public object? RegistryDataOn { get; set; }
    public object? RegistryDataOff { get; set; }
    public string CmdCommand { get; set; } = "";
    public bool IsActive { get; set; }
}
