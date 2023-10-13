namespace Fable.Sdk.Tasks
open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Threading
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Fable.Sdk.Tasks.Patterns

type FableCompile() =
    inherit Task()
    
    let mkDummyDisposable () = { new IDisposable with member this.Dispose () = () }
    let mutable cts = None
    
    [<Required>]
    member val InputFsproj: string = "" with get, set
    
    [<Required>]
    member val FableToolDll = "" with get, set
    
    [<Output>]
    member val OutputFiles: ITaskItem[] = Array.empty with get
    
    member private this.StartProcess (startInfo: ProcessStartInfo) =
        // let startInfo = ProcessStartInfo(FileName = fileName)
        this.Log.LogMessage (MessageImportance.High, "Running: {0}", startInfo.FileName, startInfo.Arguments)
        Process.Start startInfo
        
    // member private this.RunProcessAsync (startInfo: ProcessStartInfo, ?cancellationToken: CancellationToken) = task {
    //     use proc = Process.Start startInfo
    //     do! proc.WaitForExitAsync (?cancellationToken = cancellationToken)
    // }
    
    member private this.RunProcessAsync (fileName: string, ?arguments: string, ?useShellExecute: bool, ?onStdOutLineRecieved: string -> unit, ?onStdErrLineRecieved: string -> unit, ?cancellationToken: CancellationToken) = task {
        let startInfo = ProcessStartInfo(FileName = fileName)
        arguments |> Option.iter (fun arguments -> startInfo.Arguments <- arguments)
        useShellExecute |> Option.iter (fun useShellExecute -> startInfo.UseShellExecute <- useShellExecute)
        startInfo.RedirectStandardOutput <- Option.isSome onStdOutLineRecieved
        startInfo.RedirectStandardError <- Option.isSome onStdErrLineRecieved
        
        this.Log.LogMessage (MessageImportance.High, "Running process: {0} {0}", fileName, defaultArg arguments "")
        use proc = Process.Start startInfo
        use _ =
            match onStdOutLineRecieved with
            | Some f -> proc.OutputDataReceived.Subscribe (fun x -> f x.Data)
            | None -> mkDummyDisposable ()
        use _ =
            match onStdErrLineRecieved with
            | Some f -> proc.ErrorDataReceived.Subscribe (fun x -> f x.Data)
            | None -> mkDummyDisposable ()
        
        do! proc.WaitForExitAsync (?cancellationToken = cancellationToken)
        return proc.ExitCode
    }
    
    member this.ExecuteAsync (?cancellationToken: CancellationToken) = task {
        if not (File.Exists this.FableToolDll) then
            this.Log.LogError ("Fable tool not found at {0}", this.FableToolDll)
        else
            let dotnetEntry = Process.GetCurrentProcess().MainModule.FileName
            let! exitCode =
                this.RunProcessAsync (
                    dotnetEntry, $"%s{this.FableToolDll} %s{this.InputFsproj}",
                    onStdOutLineRecieved = (fun msg -> this.Log.LogMessage (MessageImportance.Normal, msg)),
                    onStdErrLineRecieved = this.Log.LogError,
                    ?cancellationToken = cancellationToken)
            if exitCode = 0 then
                this.Log.LogMessage (MessageImportance.Normal, "Fable compilation succeeded")
            else
                this.Log.LogError $"Fable compilation failed (exit code %d{exitCode})"
        return not this.Log.HasLoggedErrors
    }
    
    override this.Execute () =
        use _cts = new CancellationTokenSource()
        cts <- Some _cts
        (this.ExecuteAsync _cts.Token).Result
        
    interface ICancelableTask with
        override this.Cancel () = cts |> Option.iter (fun cts -> cts.Cancel ())
