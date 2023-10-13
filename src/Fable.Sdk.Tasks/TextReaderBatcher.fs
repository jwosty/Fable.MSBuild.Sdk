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
    static member ProcessTextAsync (reader: TextReader, onTextReceived: string -> unit, ?chunkSizeHint: int, ?maxBufferSizeHint: int, ?batchEndWait: TimeSpan, ?cancellationToken: CancellationToken) = task {
        let batchEndWait = defaultArg batchEndWait (TimeSpan.FromSeconds 3)
        
        let chunkSizeHint = defaultArg chunkSizeHint 4096
        let maxBufferSizeHint = defaultArg maxBufferSizeHint (chunkSizeHint * 16)
        
        let sync = obj()
        let sb = StringBuilder(maxBufferSizeHint)
        
        let processTextEvent (event: BatchedTextReaderMessage) =
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
                        let text = sb.ToString()
                        sb.Clear () |> ignore
                        Some text
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
    
    static member SubscribeTextReceived (reader: TextReader, onTextReceived: string -> unit, ?chunkSizeHint: int, ?maxBufferSizeHint: int, ?batchEndWait: TimeSpan, ?cancellationToken: CancellationToken) =
        Task.Run (fun () -> TextReaderBatcher.ProcessTextAsync (reader, onTextReceived, ?chunkSizeHint = chunkSizeHint, ?maxBufferSizeHint = maxBufferSizeHint, ?batchEndWait = batchEndWait, ?cancellationToken = cancellationToken) : Task)
        |> ignore