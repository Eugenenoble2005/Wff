namespace Wff.ViewModels

open CommunityToolkit.Mvvm
open System.Threading.Tasks
open System.Collections.Generic
open System

type MainWindowViewModel() as self =
    inherit ViewModelBase()
    let framerates = [ "Default"; "30"; "60"; "120" ]
    let audioBackends = [ "Default"; "Pipewire"; "PulseAudio"; "ALSA" ]
    let videoCodecs = Utils.ffmpeg.codecs Utils.ffmpeg.FfmpegCodecTarget.Video
    let audioCodecs = Utils.ffmpeg.codecs Utils.ffmpeg.FfmpegCodecTarget.Audio
    let audioDevices = Utils.ffmpeg.audioDevices
    let mutable audioCodec = audioCodecs.[0]
    let mutable videoCodec = videoCodecs.[0]
    let mutable framerate = framerates.[0]
    let mutable dmabuf = true
    let mutable damage = true
    let mutable audioBackend = audioBackends.[0]
    let mutable audioDevice = audioDevices.[0]
    let mutable RecorderProcess: Utils.Recorder.RecorderProcess option = None
    let mutable recording = false

    let mutable filename =
        let wffFolder =
            System.IO.Path.Join(
                [ Environment.GetFolderPath Environment.SpecialFolder.MyVideos
                  "wff-recordings" ]
                |> List.toArray
            )

        if System.IO.Directory.Exists wffFolder <> true then
            System.IO.Directory.CreateDirectory wffFolder |> ignore

        System.IO.Path.Join([ wffFolder; "output.mkv" ] |> List.toArray)

    let StartRecording (model: Utils.Recorder.WfrecorderDataModel) =
        let recorderProcess = Utils.Recorder.CreateRecorder model
        RecorderProcess <- Some recorderProcess

        match recorderProcess.Start with
        | Error e -> printfn "%s" e
        | Ok _ ->
            self.IsRecording <- true
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

    member self.AudioDevices = Utils.ffmpeg.audioDevices

    member self.AudioCodecs = audioCodecs

    member self.VideoCodecs = videoCodecs

    member self.Framerates = framerates

    member self.AudioBackends = audioBackends

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
        CommunityToolkit.Mvvm.Input.RelayCommand(fun _ -> StartRecording(self.DataModel))

    member self.TryStopRecordingCommand =
        CommunityToolkit.Mvvm.Input.RelayCommand(fun _ -> StopRecording())

    member private self.DataModel: Utils.Recorder.WfrecorderDataModel =
        { Framerate = self.Framerate
          Filename = self.Filename
          VideoCodec = self.VideoCodec
          AudioCodec = self.AudioCodec
          Dmabuf = self.Dmabuf
          Damage = self.Damage
          AudioBackend = self.AudioBackend
          AudioDevice = self.AudioDevice }
