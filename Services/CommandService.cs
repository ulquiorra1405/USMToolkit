using System.Diagnostics;
using System.IO;
using System.Text;
using Toolkit.ViewModels;

namespace Toolkit.Services;

public static class VariableHelper
{
    public static string ReplaceVariables(string text, Dictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(text) || values == null || values.Count == 0)
            return text;
        var sb = new StringBuilder(text);
        foreach (var kvp in values)
            sb.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        return sb.ToString();
    }
}



public class CommandService
{
    private static string ResolveToolkitRoot(string command)
    {
        if (!command.Contains("%TOOLKIT_ROOT%"))
            return command;
        var root = MainViewModel.Shared?.ResolvedToolkitRoot() ?? AppContext.BaseDirectory;
        return command.Replace("%TOOLKIT_ROOT%", root);
    }

    public async Task<string> RunCommandStreamingAsync(
        string command,
        Action<string>? onAppendLine = null,
        Action<string>? onReplaceLine = null,
        CancellationToken ct = default,
        string shell = "cmd")
    {
        command = ResolveToolkitRoot(command);
        var psi = new ProcessStartInfo
        {
            FileName = shell == "powershell" ? "powershell.exe" : "cmd.exe",
            Arguments = shell == "powershell" ? $"-NoProfile -Command \"{command.Replace("\"", "\\\"")}\"" : $"/c {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var lineBuffer = new List<char>();
        char? lastChar = null;

        process.Start();

        ct.Register(() =>
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        });

        try
        {
            var buf = new char[4096];
            int charsRead;
            while ((charsRead = await process.StandardOutput.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false)) > 0)
            {
                ct.ThrowIfCancellationRequested();

                for (int i = 0; i < charsRead; i++)
                {
                    char c = buf[i];

                    if (c == '\n')
                    {
                        var line = new string(lineBuffer.ToArray());
                        lineBuffer.Clear();
                        output.AppendLine(line);
                        onAppendLine?.Invoke(line);
                    }
                    else if (c == '\r')
                    {
                        // wait for next char
                    }
                    else
                    {
                        if (lastChar == '\r')
                        {
                            // \r followed by non-\n → overwrite current line
                            var line = new string(lineBuffer.ToArray());
                            lineBuffer.Clear();
                            ReplaceLastLine(output, line);
                            onReplaceLine?.Invoke(line);
                        }
                        lineBuffer.Add(c);
                    }
                    lastChar = c;
                }
            }

            // Flush remaining buffer (last char was \r)
            if (lastChar == '\r' && lineBuffer.Count > 0)
            {
                var line = new string(lineBuffer.ToArray());
                lineBuffer.Clear();
                ReplaceLastLine(output, line);
                onReplaceLine?.Invoke(line);
            }

            string error = await process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrEmpty(error))
            {
                var errLine = $"\n[STDERR]\n{error}";
                output.Append(errLine);
                onAppendLine?.Invoke(errLine);
            }

            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        ct.ThrowIfCancellationRequested();
        return output.ToString();
    }

    private static void ReplaceLastLine(StringBuilder sb, string newLine)
    {
        int lastNl = sb.ToString().LastIndexOf('\n', sb.Length - 2);
        if (lastNl >= 0)
            sb.Length = lastNl + 1;
        else
            sb.Length = 0;
        sb.Append(newLine);
    }

    public async Task<(string Output, bool IsSuccess)> RunCommandWithStatusAsync(string command, CancellationToken ct = default, string shell = "cmd")
    {
        try
        {
            var result = await RunCommandAsync(command, ct, shell);
            return (result, true);
        }
        catch (OperationCanceledException)
        {
            return ("Proceso interrumpido por el usuario.", false);
        }
        catch (Exception ex)
        {
            return ($"Error: {ex.Message}", false);
        }
    }

    public async Task<string> RunCommandAsync(string command, CancellationToken ct = default, string shell = "cmd")
    {
        command = ResolveToolkitRoot(command);
        var psi = new ProcessStartInfo
        {
            FileName = shell == "powershell" ? "powershell.exe" : "cmd.exe",
            Arguments = shell == "powershell" ? $"-NoProfile -Command \"{command.Replace("\"", "\\\"")}\"" : $"/c {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.Start();

        ct.Register(() =>
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        });

        try
        {
            await Task.WhenAll(
                process.StandardOutput.ReadToEndAsync().ContinueWith(t => output.Append(t.Result)),
                process.StandardError.ReadToEndAsync().ContinueWith(t => error.Append(t.Result))
            );

            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        ct.ThrowIfCancellationRequested();

        var result = output.ToString();
        if (error.Length > 0)
            result += $"\n[STDERR]\n{error}";

        return result;
    }
}
