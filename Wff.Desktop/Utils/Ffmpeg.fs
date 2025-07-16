module Utils.ffmpeg

open System.Diagnostics

let audioDevices =
    let _process =
        Process.Start(
            ProcessStartInfo(
                FileName = "/bin/bash",
                Arguments = "-c \"pactl list sources | grep Name\"", //TODO: Don't use grep for this
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            )
        )

    let mutable audio_devices = [ "None" ]
    let mutable has_error = false

    _process.OutputDataReceived.Add(fun sender ->
        sender.Data
        |> Option.ofObj
        |> Option.iter (fun data ->
            if data.TrimStart().StartsWith "Name:" then
                let device = data.Split("Name:").[1].Trim()
                audio_devices <- audio_devices @ [ data.Split("Name:").[1] ]))

    match _process with
    | null -> audio_devices //Should never happen , i don't even know why it's a thing
    | p ->
        p.Start() |> ignore
        p.BeginOutputReadLine()
        p.WaitForExit()
        audio_devices

type FfmpegCodecTarget =
    | Video
    | Audio

let codecs target =
    let codecFilter =
        match target with
        | FfmpegCodecTarget.Audio -> "A"
        | FfmpegCodecTarget.Video -> "V"

    let _process =
        Process.Start(
            ProcessStartInfo(
                FileName = "ffmpeg",
                Arguments = "-encoders",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            )
        )

    let mutable codecs = [ "Default" ]

    _process.OutputDataReceived.Add(fun sender ->
        sender.Data
        |> Option.ofObj
        |> Option.iter (fun data ->
            if
                data.TrimStart().StartsWith codecFilter
                && data.Split(" ", System.StringSplitOptions.RemoveEmptyEntries).Length > 2
            then
                let codec = data.Split(" ", System.StringSplitOptions.RemoveEmptyEntries).[1]

                if codec <> "=" then
                    codecs <- codecs @ [ codec ]))

    match _process with
    | null -> codecs
    | p ->
        p.Start() |> ignore
        p.BeginOutputReadLine()
        p.WaitForExit()
        codecs
