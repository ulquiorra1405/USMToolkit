namespace Toolkit.Models;

public class DiagnosticTest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Command { get; set; } = "";
    public bool IsRunning { get; set; }
    public bool HasResult { get; set; }
    public string RawOutput { get; set; } = "";
    public string ParsedSummary { get; set; } = "";
    public bool IsSuccess { get; set; }
}
