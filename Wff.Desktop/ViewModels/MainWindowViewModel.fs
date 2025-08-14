namespace Wff.ViewModels

open CommunityToolkit.Mvvm
open System.Threading.Tasks
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
    let mutable audioCodec = audioCodecs.[0]
    let mutable videoCodec = videoCodecs.[0]
    let mutable framerate = framerates.[0]
    let mutable region = "Screen"
    let mutable dmabuf = true
    let mutable recordingDuration = ""
    let mutable damage = true
    let mutable audioBackend = audioBackends.[0]
    let mutable audioDevice = audioDevices.[0]
    let mutable RecorderProcess: Utils.Recorder.RecorderProcess option = None
    let mutable recording = false

    let mutable filename =
        let wffFolder =
            [ Environment.GetFolderPath Environment.SpecialFolder.MyVideos
              "wff-recordings" ]
            |> List.toArray
            |> System.IO.Path.Join

        if System.IO.Directory.Exists wffFolder <> true then
            System.IO.Directory.CreateDirectory wffFolder |> ignore

        [ wffFolder; "output.mkv" ] |> List.toArray |> System.IO.Path.Join

    let timerElapsed (e: Timers.ElapsedEventArgs) =
        if RecorderProcess.IsSome then
            self.RecordingDuration <- RecorderProcess.Value.Stopwatch.Elapsed.ToString @"hh\:mm\:ss\:ff"

    let StartRecording (model: Utils.Recorder.WfrecorderDataModel) =
        let recorderProcess = Utils.Recorder.CreateRecorder model
        RecorderProcess <- Some recorderProcess

        match recorderProcess.Start with
        | Error e -> printfn "%s" e
        | Ok _ ->
            self.IsRecording <- true
            let timer = new System.Timers.Timer 100 //ms
            timer.Start()
            timer.Elapsed.Add timerElapsed
            printfn "Starting recording..."


    let StopRecording () =
        if self.IsRecording then
            RecorderProcess
            |> Option.iter (fun rp ->
                match rp.Stop with
                | Error e -> printfn "%s" e
                | Ok _ ->
                    self.IsRecording <- false
                    rp.Dispose()
                    RecorderProcess <- None
                    printfn "Recording saved ...")

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
            data.Data
            |> Option.ofObj
            |> Option.iter (fun d ->
                if d.Length > 0 then
                    self.Region <- d))

        _process.WaitForExit()


    member self.AudioDevices = Utils.ffmpeg.audioDevices

    member self.AudioCodecs = audioCodecs

    member self.VideoCodecs = videoCodecs

    member self.Framerates = framerates

    member self.AudioBackends = audioBackends
    member self.Outputs = Utils.ffmpeg.outputs

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
        CommunityToolkit.Mvvm.Input.RelayCommand(fun _ -> StartRecording self.DataModel)

    member self.TryStopRecordingCommand =
        CommunityToolkit.Mvvm.Input.RelayCommand(fun _ -> StopRecording())

    member self.TrySlurpSelectRegionCommand =
        CommunityToolkit.Mvvm.Input.RelayCommand(fun _ -> TrySlurpSelectRegion())

    member self.TrySelectScreenRegionCommand =
        CommunityToolkit.Mvvm.Input.RelayCommand(fun _ -> self.Region <- "Screen")

    member self.RecordingDuration
        with get () = recordingDuration
        and set v = self.SetProperty(&recordingDuration, v) |> ignore

    member self.Region
        with get () = region
        and set v = self.SetProperty(&region, v) |> ignore

    member private self.DataModel: Utils.Recorder.WfrecorderDataModel =
        { Framerate = self.Framerate
          Filename = self.Filename
          VideoCodec = self.VideoCodec
          AudioCodec = self.AudioCodec
          Dmabuf = self.Dmabuf
          Damage = self.Damage
          Region = self.Region
          AudioBackend = self.AudioBackend
          AudioDevice = self.AudioDevice }
