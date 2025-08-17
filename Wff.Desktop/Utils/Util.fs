[<AutoOpen>]
module Utils.Common

type MaybeBuilder() =
    member self.Bind(m,f) = m |> Option.ofObj |> Option.bind f 
    member self.Return(m) = Some m
    member self.Zero() = Some()
let maybe = MaybeBuilder()
