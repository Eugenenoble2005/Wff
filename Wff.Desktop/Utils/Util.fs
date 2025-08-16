[<AutoOpen>]
module Utils.Common

type MaybeBuilder() =
    member self.Bind(m,f) = m |> Option.ofObj |> Option.bind f 
    member self.Return(m) = Some m
    member self.Zero() = Some()
    member self.Combine m f = f
let maybe = MaybeBuilder()
