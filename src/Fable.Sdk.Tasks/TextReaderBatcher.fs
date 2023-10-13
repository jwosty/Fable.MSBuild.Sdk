namespace Fable.Sdk.Tasks
open System
open System.Buffers
open System.Collections.Concurrent
open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks

type private BatchedTextReaderMessage =
    | ReceivedText of Memory<char>
    | KeepAlive
    | End

type internal TextReaderBatcher() =
    static member ProcessTextAsync (reader: TextReader, onTextReceived: string -> unit, ?trimSingleTrailingNewline: bool, ?chunkSizeHint: int, ?maxBufferSizeHint: int, ?batchEndWait: TimeSpan, ?cancellationToken: CancellationToken) = task {
        let batchEndWait = defaultArg batchEndWait (TimeSpan.FromSeconds 3)
        
        let trimSingleTrailingNewline = defaultArg trimSingleTrailingNewline true
        let chunkSizeHint = defaultArg chunkSizeHint 4096
        let maxBufferSizeHint = defaultArg maxBufferSizeHint (chunkSizeHint * 16)
        
        let sync = obj()
        let sb = StringBuilder(maxBufferSizeHint)
        
        let processTextEvent (event: BatchedTextReaderMessage) =
            // Trims a *single* trailing newline, if it exists
            let trimTrailingNewline (sb: StringBuilder) =
                let z = if sb.Length > 0 then Some (sb[sb.Length - 1]) else None
                let y = if sb.Length > 1 then Some (sb[sb.Length - 2]) else None
                match y, z with
                | Some '\r', Some '\n'          -> sb.Length <- sb.Length - 2
                | _,         Some ('\r' | '\n') -> sb.Length <- sb.Length - 1
                | _,         _                  -> ()
            
            let text =
                lock sync (fun () ->
                    let shouldFlush =
                        match event with
                        | ReceivedText chunk ->
                            sb.Append chunk |> ignore
                            sb.Length >= maxBufferSizeHint
                        | KeepAlive | End -> true
                    
                    let shouldFlush = shouldFlush && sb.Length > 0
                    if shouldFlush then
                        if trimSingleTrailingNewline then trimTrailingNewline sb
                        let text = sb.ToString ()
                        if not (String.IsNullOrEmpty text) then
                            sb.Clear () |> ignore
                            Some text
                        else
                            None
                    else
                        None
                )
            match text with
            | Some text -> onTextReceived text
            | None -> ()
        
        let readLoop () = task {
            use timer = new Timer((fun _ -> processTextEvent KeepAlive), Unchecked.defaultof<obj>, batchEndWait, Timeout.InfiniteTimeSpan) in
            
            use buffer = MemoryPool<char>.Shared.Rent chunkSizeHint
            // TODO: ReadAsync(Memory<char>, CancellationToken) from .NET 8
            // TODO: while! (while-bang) from F# 8
            let! _countRead = reader.ReadAsync buffer.Memory
            let mutable countRead = _countRead
            
            while countRead > 0 do
                let! _countRead = reader.ReadAsync buffer.Memory
                countRead <- _countRead
                // This restarts the timer
                timer.Change (batchEndWait, Timeout.InfiniteTimeSpan) |> ignore
                let chunk = buffer.Memory.Slice(0, countRead)
                processTextEvent (ReceivedText chunk)
        }
        
        do! readLoop ()
        processTextEvent End
    }
    
    static member SubscribeTextReceived (reader: TextReader, onTextReceived: string -> unit, ?trimSingleTrailingNewline: bool, ?chunkSizeHint: int, ?maxBufferSizeHint: int, ?batchEndWait: TimeSpan, ?cancellationToken: CancellationToken) =
        Task.Run (fun () -> TextReaderBatcher.ProcessTextAsync (reader, onTextReceived, ?trimSingleTrailingNewline = trimSingleTrailingNewline, ?chunkSizeHint = chunkSizeHint, ?maxBufferSizeHint = maxBufferSizeHint, ?batchEndWait = batchEndWait, ?cancellationToken = cancellationToken) : Task)
        |> ignore