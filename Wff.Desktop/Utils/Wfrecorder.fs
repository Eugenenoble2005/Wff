module Utils.Recorder

open System.Diagnostics

type WfrecorderDataModel =
    { Filename: string
      Framerate: string
      VideoCodec: string
      AudioCodec: string
      Dmabuf: bool
      Damage: bool
      AudioBackend: string
      AudioDevice: string }

let private RecorderCommand (dataModel: WfrecorderDataModel) =
    let parts =
        [ " --overwrite"
          $" -f {dataModel.Filename}"
          if dataModel.Framerate <> "Default" then
              $" --framerate {dataModel.Framerate}"
          if dataModel.VideoCodec <> "Default" then
              $" --codec {dataModel.VideoCodec}"
          if dataModel.AudioCodec <> "Default" then
              $" --audio-codec {dataModel.AudioCodec}"
          if dataModel.Dmabuf <> true then
              $" --no-dmabuf"
          if dataModel.Damage <> true then
              $" --no-damage"
          if dataModel.AudioBackend <> "Default" then
              $" --audio-backend {dataModel.AudioBackend}"
          if dataModel.AudioDevice <> "None" then
              $" --audio {dataModel.AudioDevice}" ]

    parts |> String.concat ""

type RecorderProcess(_process: Process) =
    let mutable processRunning = false

    member self.Start =
        match processRunning with
        | true -> Error "Process already running."
        | _ ->
            _process.Start() |> ignore
            processRunning <- true
            Ok()

    member self.Stop =
        match processRunning with
        | false -> Error "No Running process we manage to stop."
        | true ->
            try
                _process.Kill() //Have reason to believe this is abrupt and does not send the correct signal.
                Ok()
            with _ ->
                Error "Failed to stop process."

    member self.Dispose() = _process.Dispose()

let CreateRecorder (dataModel: WfrecorderDataModel) =
    let cmd = RecorderCommand dataModel

    let recorderProcess = new Process()

    recorderProcess.StartInfo <-
        new ProcessStartInfo(
            FileName = "wf-recorder",
            Arguments = cmd,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        )

    recorderProcess.EnableRaisingEvents <- true
    new RecorderProcess(recorderProcess)
