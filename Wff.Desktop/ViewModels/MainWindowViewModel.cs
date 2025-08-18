﻿namespace Wff.Desktop.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Wff.Utils;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        Init();
    }
    List<string> Framerates => new() { "Default", "30", "60", "120" };
    List<string> audioBackends => new() { "Default", "Pipewire", "Pulseaudio", "ALSA" };
    RecorderProcess? recorderProcess = null;
    Process? countdownProcess = null;
    bool cancelledDuringCountdown = false;
    string initialFilename()
    {
        string wffFolder = Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "wff-recordings"
        );
        if (!Directory.Exists(wffFolder))
        {
            Directory.CreateDirectory(wffFolder);
        }
        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Join(wffFolder, $"{ts}output.mkv");
    }

    void TimerElapsed(object? s, System.Timers.ElapsedEventArgs e)
    {
        if (recorderProcess is not null)
        {
            RecordingDuration = recorderProcess.stopwatch.Elapsed.ToString(@"hh\:mm\ss\:ff");
        }
    }

    async Task StartCountdownAsync()
    {
        var process = new Process();
        countdownProcess = process;
        var countdownExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "countdown");
        Console.WriteLine($"{countdownExe} {Output} {Delay}");
        process.StartInfo = new()
        {
            FileName = countdownExe,
            Arguments = $"{Output} {Delay}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        if (process is null) return;
        Console.WriteLine("triggeting the countdown");
        process.Start();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();
        Console.WriteLine("countdown ended");
    }

    public async void StartRecordingAsync()
    {
        IsRecording = true;
        cancelledDuringCountdown = false;
        if (Delay > 0)
        {
            Console.WriteLine("has a dley");
            await StartCountdownAsync();
            Console.WriteLine("this should not show till after the counrdown");
        } //will block till countdown is complete

        if (cancelledDuringCountdown) return;
        recorderProcess = Wfrecorder.CreateRecorder(DataModel);
        recorderProcess.Start();
        var timer = new System.Timers.Timer(100);
        timer.Start();
        timer.Elapsed += TimerElapsed;
        Console.WriteLine("Starting recording...");
    }
    public async void SaveFileDialogAsync()
    {
        var wayland_display = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        var toplevel = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var options = new FilePickerSaveOptions();
        options.SuggestedFileName = Path.GetFileName(Filename);
        options.ShowOverwritePrompt = true;
        options.Title = "Save Recording";
        if (toplevel is null) return;
        var file = await toplevel.StorageProvider.SaveFilePickerAsync(options);
        if (file is not null) Filename = file.Path.AbsolutePath;
        //restore environment
        if (wayland_display is not null)
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", wayland_display);
        }
    }

    public void TrySelectScreenRegion() => Region = "Screen";

    public void StopRecording()
    {
        //stop a recorder process along with a countdown process if they exist
        if (!IsRecording) return;
        if (countdownProcess is not null)
        {
            cancelledDuringCountdown = true;
            countdownProcess.Signal(2);
            countdownProcess = null;
        }
        if (recorderProcess is not null)
        {
            recorderProcess.Stop();
            recorderProcess = null;
            Console.WriteLine("Recording saved");
        }
        IsRecording = false;
    }

    public void TrySlurpSelectRegion()
    {
        var process = new Process();
        process.StartInfo = new()
        {
            FileName = "slurp",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        if (process is null) return;
        process.Start();
        process.BeginOutputReadLine();
        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data is null) return;
            var data = e.Data;
            if (data.Length > 0)
            {
                Region = data;
            }
        };
        process.WaitForExit();
    }
    [ObservableProperty]
    public string filename = "";

    [ObservableProperty]
    public bool dmabuf = true;

    [ObservableProperty]
    public bool damage = true;

    [ObservableProperty]
    public string audioBackend = "";

    [ObservableProperty]
    public string audioCodec = "";

    [ObservableProperty]
    public string videoCodec = "";

    [ObservableProperty]
    public string audioDevice = "";

    [ObservableProperty]
    public string framerate = "";

    [ObservableProperty]
    public bool isRecording = false;

    [ObservableProperty]
    public string recordingDuration = "";

    [ObservableProperty]
    public string region = "Screen";

    [ObservableProperty]
    public int delay = 0;

    [ObservableProperty]
    public string output = "";

    public void Init()
    {
        AudioDevices = new(Utils.Ffmpeg.AudioDevices());
        AudioCodecs = new(Utils.Ffmpeg.Codecs(Utils.FfmpegCodecTarget.Audio));
        VideoCodecs = new(Utils.Ffmpeg.Codecs(Utils.FfmpegCodecTarget.Video));
        Outputs = new(Utils.Ffmpeg.Outputs());
        Filename = initialFilename();
        Output = Outputs[0];
        AudioDevice = AudioDevices[0];
        AudioCodec = AudioCodecs[0];
        VideoCodec = VideoCodecs[0];
        Framerate = Framerates[0];
        AudioBackend = AudioBackends[0];

    }
    public ObservableCollection<string> AudioDevices { get; set; } = new();
    public ObservableCollection<string> AudioCodecs { get; set; } = new();
    public ObservableCollection<string> VideoCodecs { get; set; } = new();
    public ObservableCollection<string> AudioBackends { get; set; } = new() { "Default", "Pipewire", "Pulseaudio", "ALSA" };
    public ObservableCollection<string> Outputs { get; set; } = new();
    WfrecorderDataModel DataModel => new()
    {
        Framerate = Framerate,
        Filename = Filename,
        Output = Output,
        VideoCodec = VideoCodec,
        AudioCodec = AudioCodec,
        Dmabuf = Dmabuf,
        Damage = Damage,
        Region = Region,
        AudioBackend = AudioBackend,
        AudioDevice = AudioDevice,
    };
}
