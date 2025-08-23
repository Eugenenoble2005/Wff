namespace Wff.Utils;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class Ffmpeg
{
    public async static Task<List<string>> AudioDevicesAsync()
    {
        var process = new Process(
        );
        process.StartInfo =
            new ProcessStartInfo()
            {
                FileName = "/bin/bash",
                Arguments = "-c \"pactl list sources | grep Name\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

        List<string> audio_devices = new() { "None" };
        if (process is null) return audio_devices;
        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data is null) return;
            if (e.Data.TrimStart().StartsWith("Name:"))
            {
                var device = e.Data.Split("Name:")[1].Trim();
                audio_devices.Add(device);
            }
        };
        process.Start();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();
        return audio_devices;
    }
    public async static Task<List<string>> CodecsAsync(FfmpegCodecTarget target)
    {
        var codecFilter = target switch
        {
            FfmpegCodecTarget.Audio => "A",
            FfmpegCodecTarget.Video => "V",
            _ => "",
        };
        var process = new Process();
        process.StartInfo = new()
        {
            FileName = "ffmpeg",
            Arguments = "-encoders",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        List<string> codecs = new() { "Default" };
        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data is null) return;
            if (e.Data.TrimStart().StartsWith(codecFilter) && e.Data.Split(" ", System.StringSplitOptions.RemoveEmptyEntries).Length > 2)
            {
                var codec = e.Data.Split(" ", System.StringSplitOptions.RemoveEmptyEntries)[1];
                if (codec != "=") codecs.Add(codec);
            }
        };
        if (process is null) return codecs;
        process.Start();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();
        return codecs;
    }
    public async static Task<List<string>> OutputsAsync()
    {
        var command = "wlr-randr";
        var process = new Process();
        process.StartInfo = new()
        {
            FileName = command,
            Arguments = "--json",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
        };
        var outputs = new List<string>() { };
        var tlock = new System.Threading.ManualResetEventSlim(false);
        var buffer = new System.Text.StringBuilder();
        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data is null) return;
            var data = e.Data;
            buffer.AppendLine(data);
        };
        process.Exited += (s, e) =>
        {
            var jsonDoc = JsonDocument.Parse(buffer.ToString());
            foreach (var element in jsonDoc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("name", out var nameProperty))
                {
                    outputs.Add(nameProperty.GetString() ?? string.Empty);
                }
            }
            tlock.Set();
        };
        if (process is null) return outputs;
        process.EnableRaisingEvents = true;
        process.Start();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();
        tlock.Wait();
        return outputs;
    }
}

public enum FfmpegCodecTarget
{
    Video,
    Audio
};

public record RandrOutput
{
    public required string name { get; set; }
};


