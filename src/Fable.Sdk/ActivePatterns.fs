namespace Fable.Sdk.Tasks

open System

module internal Patterns =
    let (|StringEquals|_|) (compareTo: string) (comparison: StringComparison) (input: string) =
        if String.Equals (input, compareTo, comparison) then
            Some input
        else
            None
    
    let (|OrdinalIgnoreCase|_|) (compareTo: string) (input: string) =
        if StringComparer.OrdinalIgnoreCase.Equals (input, compareTo) then
            Some input
        else None
        