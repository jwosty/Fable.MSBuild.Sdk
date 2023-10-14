namespace Fable.Sdk.Tasks
open System
open System.Buffers
open System.Collections.Concurrent
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.ExceptionServices
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
    
    member private this.StartProcess (startInfo: ProcessStartInfo) =
        this.Log.LogMessage (MessageImportance.High, "Running: {0}", startInfo.FileName, startInfo.Arguments)
        Process.Start startInfo
    
    member private this.RunProcessAsync (fileName: string, ?arguments: string, ?useShellExecute: bool, ?onStdOutLineRecieved: string -> unit, ?stdoutAltOutput: TextWriter, ?onStdErrLineRecieved: string -> unit, ?stderrAltOutput: TextWriter, ?cancellationToken: CancellationToken) = task {
        let startInfo = ProcessStartInfo(FileName = fileName)
        arguments |> Option.iter (fun arguments -> startInfo.Arguments <- arguments)
        useShellExecute |> Option.iter (fun useShellExecute -> startInfo.UseShellExecute <- useShellExecute)
        startInfo.RedirectStandardOutput <- Option.isSome onStdOutLineRecieved || Option.isSome stdoutAltOutput
        startInfo.RedirectStandardError <- Option.isSome onStdErrLineRecieved || Option.isSome stderrAltOutput
        
        this.Log.LogMessage (MessageImportance.High, "Running process: {0} {1}", fileName, defaultArg arguments "")
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
                
                let! exitCode =
                    this.RunProcessAsync (
                        dotnetEntry, $"%s{this.FableToolDll} %s{this.InputFsproj}",
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
