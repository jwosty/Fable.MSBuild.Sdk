namespace Fable.Sdk.Tasks
open System
open Microsoft.Build.Framework
open Microsoft.Build.Utilities

type Greet() =
    inherit Task()

    [<Required>]
    member val Name: string = null with get, set
    
    override this.Execute () =
        this.Log.LogMessage (MessageImportance.High, $"Hello, %s{this.Name}!")
        not this.Log.HasLoggedErrors
