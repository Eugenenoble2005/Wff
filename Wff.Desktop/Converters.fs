namespace Wff.Converters

open Avalonia.Data.Converters

type StringEqualConverter() =
    interface IValueConverter with
        member this.Convert
            (
                value: obj | null,
                targetType: System.Type,
                parameter: obj | null,
                culture: System.Globalization.CultureInfo
            ) : obj | null =
            match value, parameter with
            | (:? string as sval), (:? string as param) -> box (sval = param)
            | _ -> box false

        member self.ConvertBack
            (
                value: obj | null,
                targetType: System.Type,
                parameter: obj | null,
                culture: System.Globalization.CultureInfo
            ) : obj | null =
            raise (System.NotImplementedException())


type StringNotEqualConverter() =
    interface IValueConverter with
        member this.Convert
            (
                value: obj | null,
                targetType: System.Type,
                parameter: obj | null,
                culture: System.Globalization.CultureInfo
            ) : obj | null =
            match value, parameter with
            | (:? string as sval), (:? string as param) -> box (sval <> param)
            | _ -> box false

        member self.ConvertBack
            (
                value: obj | null,
                targetType: System.Type,
                parameter: obj | null,
                culture: System.Globalization.CultureInfo
            ) : obj | null =
            raise (System.NotImplementedException())


open System
open System.Globalization
open Avalonia.Data.Converters
open System.Collections.Generic

type RegionContentConverter() =
    interface IMultiValueConverter with
        member _.Convert
            (values: IList<obj | null>, targetType: Type, parameter: obj | null, culture: CultureInfo)
            : obj | null =

            let region =
                match values.[0] with
                | :? string as v -> v
                | _ -> ""

            let notEqual =
                match values.[1] with
                | :? bool as b -> b
                | _ -> false

            if notEqual then
                box (sprintf "Select Region %s" region)
            else
                box "Select Region"
