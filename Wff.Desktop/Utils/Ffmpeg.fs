//ffmpeg, pactl and wl-randr commands
module Utils.ffmpeg

open System.Text.Json

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
    | null -> audio_devices 
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

//only the output name is needed
type RandrOutput = {name:string}

let outputs =
    let command = "wlr-randr"

    let _process =
        Process.Start(
            ProcessStartInfo(
                FileName = command,
                Arguments = "--json",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            )
        )

    let mutable outputs = []
    let lock = new System.Threading.ManualResetEventSlim false
    let buffer = new System.Text.StringBuilder()
    _process.OutputDataReceived.Add(fun sender ->
        sender.Data |> Option.ofObj |>Option.iter (fun data ->
            buffer.AppendLine data |> ignore
        )
    )
    _process.Exited.Add(fun _ ->
        let randrObj = JsonSerializer.Deserialize<List<RandrOutput>> (buffer.ToString())
        match randrObj with
            |null -> ()
            | o ->
                o |> Seq.iter (fun output ->
                    printfn "%s" output.name
                    outputs <- outputs @ [output.name]    
                )
        lock.Set() |> ignore
    )
    match _process with
        |null -> ["eDP-1"]
        | p ->
            p.EnableRaisingEvents <- true
            p.Start() |> ignore
            p.BeginOutputReadLine()
            p.WaitForExit()
            lock.Wait()
            outputs

