namespace Fable.Sdk.Tasks
open System
open System.Buffers
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.ExceptionServices
open System.Text
open System.Threading
open System.Threading.Tasks
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Fable.Sdk.Tasks.Patterns
open Microsoft.FSharp.Control

type SysTask = System.Threading.Tasks.Task
type SysTask<'T> = System.Threading.Tasks.Task<'T>
type MsbTask = Microsoft.Build.Utilities.Task

type FableCompile() =
    inherit Task()
    
    let mutable cts = None
    let mutable _FableLogFile = None
    
    [<Required>]
    member val InputFsproj: string = "" with get, set
    
    [<Required>]
    member val FableToolDll = "" with get, set
    
    [<Output>]
    member val OutputFiles: ITaskItem[] = Array.empty with get
    
    member val CompilerLogFile: string = null with get, set
    
    member val Run: string = null with get, set
    
    member val RunFast: string = null with get, set
    
    member val RunWatch: string = null with get, set
    
    member val OtherFlags: string = null with get, set
    
    member private this.StartProcess (startInfo: ProcessStartInfo) =
        this.Log.LogMessage (MessageImportance.High, "Running: {0}", startInfo.FileName, startInfo.Arguments)
        Process.Start startInfo
    
    member private this.RunProcessAsync (fileName: string, ?arguments: Choice<string,seq<string>>, ?useShellExecute: bool, ?onStdOutLineRecieved: string -> unit, ?stdoutAltOutput: TextWriter, ?onStdErrLineRecieved: string -> unit, ?stderrAltOutput: TextWriter, ?cancellationToken: CancellationToken) = task {
        let startInfo = ProcessStartInfo(FileName = fileName)
        let argsStr =
            match arguments with
            | Some (Choice1Of2 args) ->
                startInfo.Arguments <- args
                args
            | Some (Choice2Of2 args) ->
                args |> Seq.iter startInfo.ArgumentList.Add
                String.Join (' ', args)
            | None -> ""
        useShellExecute |> Option.iter (fun useShellExecute -> startInfo.UseShellExecute <- useShellExecute)
        startInfo.RedirectStandardOutput <- Option.isSome onStdOutLineRecieved || Option.isSome stdoutAltOutput
        startInfo.RedirectStandardError <- Option.isSome onStdErrLineRecieved || Option.isSome stderrAltOutput
        
        this.Log.LogMessage (MessageImportance.High, "Running process: {0} {1}", fileName, argsStr)
        use proc = Process.Start startInfo
        use stdOut =
            match stdoutAltOutput with
            | Some stdoutAltOutput -> new TeeTextReader(proc.StandardOutput, stdoutAltOutput, leaveOutputOpen = true) : TextReader
            | None -> proc.StandardOutput
        let stdOutWorker =
            match onStdOutLineRecieved with
            | Some f -> TextReaderBatcher.ProcessTextAsync (stdOut, f) : SysTask
            | None -> Task.CompletedTask
        use stdErr =
            match stderrAltOutput with
            | Some stderrAltOutput -> new TeeTextReader(proc.StandardError, stderrAltOutput, leaveOutputOpen = true) : TextReader
            | None -> proc.StandardError
        let stdErrWorker =
            match onStdErrLineRecieved with
            | Some f -> TextReaderBatcher.ProcessTextAsync (stdErr, f) : SysTask
            | None -> Task.CompletedTask
        
        try
            do! Task.WhenAll [
                proc.WaitForExitAsync (?cancellationToken = cancellationToken)
                stdOutWorker
                stdErrWorker
            ]
        with e ->
            ExceptionDispatchInfo.Throw(e)
        
        match stdoutAltOutput with
        | Some w -> do! w.FlushAsync ()
        | None -> ()
        
        match stderrAltOutput with
        | Some w -> do! w.FlushAsync ()
        | None -> ()
        
        return proc.ExitCode
    }
    
    member this.ExecuteAsync (?cancellationToken: CancellationToken) = task {
        if not (File.Exists this.FableToolDll) then
            this.Log.LogError ("Fable tool not found at {0}", this.FableToolDll)
        else
            let dotnetEntry = Process.GetCurrentProcess().MainModule.FileName
            
            let mutable compilerLogFileWriter = None
            
            try
                compilerLogFileWriter <-
                    match this.CompilerLogFile with
                    | null | "" -> None
                    | compilerLogFile ->
                        Some (TextWriter.Synchronized (new StreamWriter(File.OpenWrite compilerLogFile, AutoFlush = true)))
                
                // let args =
                //     let sb = StringBuilder()
                //     sb.Append(this.FableToolDll).Append(' ').Append(this.InputFsproj) |> ignore
                //     if not (String.IsNullOrWhiteSpace this.OtherFlags) then
                //         sb.Append(' ').Append(this.OtherFlags) |> ignore
                //     sb.ToString ()
                
                let args =
                    [
                        this.FableToolDll
                        this.InputFsproj
                        if not (String.IsNullOrWhiteSpace this.OtherFlags) then this.OtherFlags
                        if not (String.IsNullOrWhiteSpace this.Run) then
                            "--run"
                            this.Run
                        if not (String.IsNullOrWhiteSpace this.RunFast) then
                            "--runFast"
                            this.RunFast
                        if not (String.IsNullOrWhiteSpace this.RunWatch) then
                            "--runWatch"
                            this.RunWatch
                    ]
                    |> String.concat " "
                
                let! exitCode =
                    this.RunProcessAsync (
                        dotnetEntry, Choice1Of2 args,
                        onStdOutLineRecieved = (fun msg -> this.Log.LogMessage (MessageImportance.High, "{0}", msg)),
                        ?stdoutAltOutput = compilerLogFileWriter,
                        onStdErrLineRecieved = (fun msg -> this.Log.LogError ("{0}", msg)),
                        ?stderrAltOutput = compilerLogFileWriter,
                        ?cancellationToken = cancellationToken)
            
                if exitCode = 0 then
                    this.Log.LogMessage (MessageImportance.Normal, "Fable compilation succeeded")
                else
                    this.Log.LogError $"Fable compilation failed (exit code %d{exitCode})"
            
            finally
                match compilerLogFileWriter with
                | Some compilerLogFileWriter ->
                    compilerLogFileWriter.Flush ()
                    compilerLogFileWriter.Dispose ()
                | None -> ()
                
        return not this.Log.HasLoggedErrors
    }
    
    override this.Execute () =
        try
            use _cts = new CancellationTokenSource()
            cts <- Some _cts
            (this.ExecuteAsync _cts.Token).Result
        with e ->
            // this.Log.LogErrorFromException (e, true, true, null)
            this.Log.LogError (e.ToString())
            false
        
    interface ICancelableTask with
        override this.Cancel () = cts |> Option.iter (fun cts -> cts.Cancel ())
