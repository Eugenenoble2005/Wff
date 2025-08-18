namespace Wff.Utils;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System;

public static class Libc
{
    [DllImport("libc")]
    public static extern int kill(int pid, int signal);
}

public static class ProcessExtension
{
    public static int Signal(this Process p, int signal)
    {
        return Libc.kill(p.Id, signal);
    }
}

public record WfrecorderDataModel
{
    public required string Filename { get; set; }
    public required string Framerate { get; set; }
    public required string Output { get; set; }
    public required string VideoCodec { get; set; }
    public required string Region { get; set; }
    public required string AudioCodec { get; set; }
    public required bool Dmabuf { get; set; }
    public required bool Damage { get; set; }
    public required string AudioBackend { get; set; }
    public required string AudioDevice { get; set; }
}

public static class Wfrecorder
{
    private static string RecorderCommand(WfrecorderDataModel dataModel)
    {
        List<string> parts = new() {
            "--overwrite",
            $"--output={dataModel.Output}",
            $"-f {dataModel.Filename}",
        };
        if (dataModel.Framerate != "Default")
        {
            parts.Add($"--framerate={dataModel.Framerate}");
        }
        if (dataModel.VideoCodec != "Default")
        {
            parts.Add($"--codec={dataModel.VideoCodec}");
        }
        if (dataModel.AudioCodec != "Default")
        {
            parts.Add($"--audio-codec={dataModel.AudioCodec}");
        }
        if (dataModel.Dmabuf != true)
        {
            parts.Add("--no-dmabuf");
        }
        if (dataModel.Damage != true)
        {
            parts.Add("--no-damage");
        }
        if (dataModel.Region != "Screen")
        {
            parts.Add($"--geometry=\"{dataModel.Region}\"");
        }
        if (dataModel.AudioBackend != "Default")
        {
            parts.Add($"--audio-backend={dataModel.AudioBackend}");
        }
        if (dataModel.AudioDevice != "None")
        {
            parts.Add($"--audio={dataModel.AudioDevice}");
        }
        return string.Join(" ", parts);
    }
    public static RecorderProcess CreateRecorder(WfrecorderDataModel dataModel)
    {
        var cmd = RecorderCommand(dataModel);
        var recorderProcess = new Process();
        recorderProcess.StartInfo = new()
        {
            FileName = "wf-recorder",
            Arguments = cmd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Console.WriteLine($"wf-recorder {cmd}");
        recorderProcess.EnableRaisingEvents = true;
        return new RecorderProcess(recorderProcess);
    }
}

//TODO: should probably just create a new instance of this directly
public class RecorderProcess(Process process)
{
    bool processRunning = false;
    public Stopwatch stopwatch { get; } = new Stopwatch();

    public void Start()
    {
        if (processRunning) { return; } //TODO:handle
        process.Start();
        stopwatch.Start();
        processRunning = true;
    }

    public void Stop()
    {
        if (!processRunning) { return; } //TODO:handle
        process.Signal(2);
    }
}


