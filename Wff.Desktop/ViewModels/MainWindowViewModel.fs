namespace Wff.ViewModels
open Utils

open CommunityToolkit.Mvvm
open Avalonia
open Avalonia.Platform.Storage
open System.Threading.Tasks
open Utils.Recorder
open System.Collections.Generic
open System

type MainWindowViewModel() as self =
    inherit ViewModelBase()
    //Backing fields
    let framerates = [ "Default"; "30"; "60"; "120" ]
    let audioBackends = [ "Default"; "Pipewire"; "PulseAudio"; "ALSA" ]
    let videoCodecs = Utils.ffmpeg.codecs Utils.ffmpeg.FfmpegCodecTarget.Video
    let audioCodecs = Utils.ffmpeg.codecs Utils.ffmpeg.FfmpegCodecTarget.Audio
    let audioDevices = Utils.ffmpeg.audioDevices
    let outputs = Utils.ffmpeg.outputs
    let mutable output = outputs.[0];
    let mutable WAYLAND_DISPLAY = None
    let mutable audioCodec = audioCodecs.[0]
    let mutable videoCodec = videoCodecs.[0]
    let mutable framerate = framerates.[0]
    let mutable cancelledDuringCountdown = false
    let mutable region = "Screen"
    let mutable delay = 0
    let mutable dmabuf = true
    let mutable recordingDuration = ""
    let mutable damage = true
    let mutable audioBackend = audioBackends.[0]
    let mutable audioDevice = audioDevices.[0]
    let mutable RecorderProcess: Utils.Recorder.RecorderProcess option = None
    let mutable CountdownProcess = None
    let mutable recording = false

    let mutable filename :string  =
        let wffFolder =
            [ Environment.GetFolderPath Environment.SpecialFolder.MyVideos
              "wff-recordings" ]
            |> List.toArray
            |> System.IO.Path.Join

        if System.IO.Directory.Exists wffFolder <> true then
            System.IO.Directory.CreateDirectory wffFolder |> ignore
        let ts = DateTime.Now.ToString("yyyyMMdd_HHmmss")
        [ wffFolder; $"{ts}output.mkv" ] |> List.toArray |> System.IO.Path.Join

    let timerElapsed (e: Timers.ElapsedEventArgs) =
        if RecorderProcess.IsSome then
            self.RecordingDuration <- RecorderProcess.Value.Stopwatch.Elapsed.ToString @"hh\:mm\:ss\:ff"

    let StartCountdownAsync() = async {      
            let _process = new System.Diagnostics.Process()
            CountdownProcess <- Some _process;
            let countdownExe = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"countdown")
            _process.StartInfo <-
                System.Diagnostics.ProcessStartInfo(
                    FileName = countdownExe, 
                    Arguments = $"{self.Output} {self.Delay}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                )

            _process.Start() |> ignore
            _process.BeginOutputReadLine()
            do! _process.WaitForExitAsync() |> Async.AwaitTask
        }
    let StartRecording (model: Utils.Recorder.WfrecorderDataModel) = async {
        self.IsRecording <- true
        cancelledDuringCountdown <- false
        if self.Delay > 0 then
            do! StartCountdownAsync() //will block till countdown is complete
        if cancelledDuringCountdown <> true then
            let recorderProcess = Utils.Recorder.CreateRecorder model
            CountdownProcess <- None
            RecorderProcess <- Some recorderProcess
            match recorderProcess.Start with
            | Error e -> printfn "%s" e
            | Ok _ ->
                let timer = new System.Timers.Timer 100 //ms
                timer.Start()
                timer.Elapsed.Add timerElapsed
                printfn "Starting recording..."
        } 
    let SaveFileDialog() =
        //capture the wayland display before it is overwritten by the portal
        let wayland_display = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")
        match Application.Current with
        | null -> ()
        | app ->
            match app.ApplicationLifetime with
            | :? Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime as desktop ->
                match desktop.MainWindow with
                | null -> ()
                | mainWindow ->
                    let saveFileAsync = async {
                        let options = FilePickerSaveOptions()
                        options.SuggestedFileName <- System.IO.Path.GetFileName(self.Filename:string)                  
                        options.ShowOverwritePrompt <- true
                        options.Title <- "Save Recording"
                        let! file = mainWindow.StorageProvider.SaveFilePickerAsync options |> Async.AwaitTask
                        match file with
                        | null -> ()
                        | f -> self.Filename <- f.Path.AbsolutePath
                        //restore Environment
                        maybe {
                            let! var = wayland_display
                            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", var)
                        } |> ignore
                    }
                    Async.StartImmediate saveFileAsync
            | _ -> ()        

    let StopRecording () =
        //stop a recorder process along with a countdown process if they exist
        if self.IsRecording then
            match CountdownProcess with
                | None -> ()
                | Some p ->
                    cancelledDuringCountdown <- true
                    p.Signal 2 |> ignore
                    CountdownProcess <- None
            match RecorderProcess with
                | None -> ()
                | Some rp ->                
                    match rp.Stop with
                    | Error e -> printfn "%s" e
                    | Ok _ ->
                        rp.Dispose()
                        RecorderProcess <- None
                        printfn "Recording saved ..."
            self.IsRecording <- false

    let TrySlurpSelectRegion () =
        //Use slurp to capture a region of the screen
        let _process = new System.Diagnostics.Process()
        _process.StartInfo <-
            System.Diagnostics.ProcessStartInfo(
                FileName = "slurp", //TODO, need to check if slurp is installed before this is run
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            )

        _process.Start() |> ignore
        _process.BeginOutputReadLine()

        _process.OutputDataReceived.Add(fun data ->
            printfn "trying to run command"
            data.Data
            |> Option.ofObj
            |> Option.iter (fun d ->
                printfn "%s" d
                if d.Length > 0 then
                    self.Region <- d))
        _process.ErrorDataReceived.Add(fun e ->
            printfn "recieved error %s" e.Data
        )

        _process.WaitForExit()

    member self.AudioDevices = Utils.ffmpeg.audioDevices

    member self.AudioCodecs = audioCodecs

    member self.VideoCodecs = videoCodecs

    member self.Framerates = framerates

    member self.AudioBackends = audioBackends
    member self.Outputs = outputs

    member self.Filename
        with get () = filename
        and set v = self.SetProperty(&filename, v) |> ignore

    member self.Dmabuf
        with get () = dmabuf
        and set v = self.SetProperty(&dmabuf, v) |> ignore

    member self.Damage
        with get () = damage
        and set v = self.SetProperty(&damage, v) |> ignore

    member self.AudioBackend
        with get () = audioBackend
        and set v = self.SetProperty(&audioBackend, v) |> ignore

    member self.AudioCodec
        with get () = audioCodec
        and set v = self.SetProperty(&audioCodec, v) |> ignore

    member self.VideoCodec
        with get () = videoCodec
        and set v = self.SetProperty(&videoCodec, v) |> ignore

    member self.AudioDevice
        with get () = audioDevice
        and set v = self.SetProperty(&audioDevice, v) |> ignore

    member self.Framerate
        with get () = framerate
        and set v = self.SetProperty(&framerate, v) |> ignore

    member self.IsRecording
        with get () = recording
        and set v = self.SetProperty(&recording, v) |> ignore

    member self.TryStartRecordingCommand =
        CommunityToolkit.Mvvm.Input.RelayCommand(fun _ -> StartRecording self.DataModel |> Async.Start)

    member self.TryStopRecordingCommand =
        CommunityToolkit.Mvvm.Input.RelayCommand(fun _ -> StopRecording())

    member self.TrySlurpSelectRegionCommand =
        CommunityToolkit.Mvvm.Input.RelayCommand(fun _ -> TrySlurpSelectRegion())

    member self.TrySelectScreenRegionCommand =
        CommunityToolkit.Mvvm.Input.RelayCommand(fun _ -> self.Region <- "Screen")

    member self.SelectFile =
        CommunityToolkit.Mvvm.Input.RelayCommand(fun _ -> SaveFileDialog())

    member self.RecordingDuration
        with get () = recordingDuration
        and set v = self.SetProperty(&recordingDuration, v) |> ignore

    member self.Region
        with get () = region
        and set v = self.SetProperty(&region, v) |> ignore

    member self.Delay
        with get() = delay
        and set v = self.SetProperty(&delay, v) |> ignore

    member self.Output
        with get() = output
        and set v = self.SetProperty(&output, v) |> ignore

    member private self.DataModel: Utils.Recorder.WfrecorderDataModel =
        { Framerate = self.Framerate
          Filename = self.Filename
          Output = self.Output  
          VideoCodec = self.VideoCodec
          AudioCodec = self.AudioCodec
          Dmabuf = self.Dmabuf
          Damage = self.Damage
          Region = self.Region
          AudioBackend = self.AudioBackend
          AudioDevice = self.AudioDevice }

