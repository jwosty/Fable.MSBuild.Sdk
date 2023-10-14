namespace Fable.Sdk.Tasks
open System
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Fable.Sdk.Tasks.Patterns

type RedirectFablePackageTargets() =
    inherit Task()
    
    [<Required>]
    member val RestoreGraphItems: ITaskItem[] = [||] with get, set
    
    [<Required>]
    member val ReplacementTargetFramework: string = null with get, set
    
    [<Output>]
    member val AdjustedRestoreGraphItems: ITaskItem[] = [||] with get, set
    
    override this.Execute () =
        this.Log.LogMessage (MessageImportance.High, "Restoring ({0}) packages using netstandard instead of fable target framework", this.RestoreGraphItems.Length)
        
        let restoreItems' =
            this.RestoreGraphItems
            |> Seq.map TaskItem
            |> Seq.map<_, ITaskItem> (fun restoreItem ->
                // let x = package.MetadataNames |> Seq.cast<string> |> String.concat System.Environment.NewLine
                match restoreItem.GetMetadata "Type" with
                | OrdinalIgnoreCase "Dependency" _ ->
                    let pkgId = restoreItem.GetMetadata "Id"
                    let oldTfmsStr = restoreItem.GetMetadata "TargetFrameworks"
                    
                    // let newTfmsStr =
                    //     match pkgId, oldTfmsStr with
                    //     | OrdinalIgnoreCase "FSharp.Core" _, _ -> this.ReplacementTargetFramework
                    //     | _, _ -> oldTfmsStr
                    
                    let newTfmsStr =
                        match pkgId, oldTfmsStr with
                        // | OrdinalIgnoreCase "FSharp.Core" _, _ -> this.ReplacementTargetFramework
                        | _, OrdinalIgnoreCase "fable4" _ -> "netstandard2.1"
                        | _, _ -> oldTfmsStr
                    
                    this.Log.LogMessage (MessageImportance.High, "Adjusting TargetFrameworks for {0} (old: {1} -> new: {2})", pkgId, oldTfmsStr, newTfmsStr)
                    restoreItem.SetMetadata ("TargetFrameworks", newTfmsStr)
                    restoreItem
                | _ ->
                    restoreItem
                
            )
            |> Seq.toArray
        this.AdjustedRestoreGraphItems <- restoreItems'
        this.Log.LogMessage (MessageImportance.High, "Done")
        not this.Log.HasLoggedErrors
