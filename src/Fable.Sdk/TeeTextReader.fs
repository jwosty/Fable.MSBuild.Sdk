namespace Fable.Sdk.Tasks

open System
open System.IO
open System.Threading.Tasks

/// Like the `tee` command, but with TextReaders/TextWriters
type TeeTextReader(input: TextReader, output: TextWriter, ?leaveInputOpen: bool, ?leaveOutputOpen: bool) =
    inherit TextReader()
    
    let leaveInputOpen = defaultArg leaveInputOpen false
    let leaveOutputOpen = defaultArg leaveOutputOpen false
    
    let ensureBoth (f1: unit -> unit) (f2: unit -> unit) =
        let mutable exns = []
        try f1 ()
        with e -> exns <- e::exns
        
        try f2 ()
        with e -> exns <- e::exns
        
        if not (List.isEmpty exns) then
            raise (AggregateException(exns))
    
    let (|>&) x f =
        f x
        x
    
    override this.Close () =
        ensureBoth
            (fun () -> if not leaveInputOpen then input.Close ())
            (fun () -> if not leaveInputOpen then output.Close ())
        
    override this.Dispose disposing =
        if disposing then
            ensureBoth
                (fun () -> if not leaveInputOpen then (input : IDisposable).Dispose ())
                (fun () -> if not leaveOutputOpen then (output : IDisposable).Dispose ())
    
    override this.Peek () = input.Peek ()
    
    override this.Read () =
        input.Read ()
        |>& output.Write
    
    override this.Read (buffer: Span<char>) =
        let countRead = input.Read buffer
        output.Write (buffer.Slice(0, countRead))
        countRead
    
    override this.Read (buffer: char[], index, count) =
        let countRead = input.Read (buffer, index, count)
        output.Write (ReadOnlySpan<_>(buffer, index, countRead))
        countRead
    
    override this.ReadBlock (buffer: Span<char>) =
        let countRead = input.ReadBlock buffer
        output.Write(buffer.Slice(0, countRead))
        countRead
    
    override this.ReadBlock (buffer: char[], index, count) =
        let countRead = input.ReadBlock (buffer, index, count)
        output.Write (ReadOnlySpan<_>(buffer, index, countRead))
        countRead
        
    override this.ReadLine () =
        input.ReadLine ()
        |>& output.WriteLine
    
    override this.ReadToEnd() =
        input.ReadToEnd ()
        |>& output.Write
    
    override this.ReadAsync (buffer: char[], index, count) = task {
        let! countRead = input.ReadAsync (buffer, index, count)
        do! output.WriteAsync (ReadOnlyMemory(buffer, index, countRead))
        return countRead
    }
    
    override this.ReadAsync (buffer: Memory<char>, cancellationToken) = ValueTask<int>(task {
        let! countRead = input.ReadAsync (buffer, cancellationToken)
        do! output.WriteAsync (buffer.Slice(0, countRead), cancellationToken)
        return countRead
    })
    
    override this.ReadBlockAsync (buffer: char[], index, count) = task {
        let! countRead = input.ReadBlockAsync (buffer, index, count) : Task<_>
        do! output.WriteAsync (ReadOnlyMemory(buffer, index, countRead))
        return countRead
    }
    
    override this.ReadBlockAsync (buffer: Memory<char>, cancellationToken) = ValueTask<int>(task {
        let! countRead = input.ReadBlockAsync (buffer, cancellationToken)
        do! output.WriteAsync (buffer.Slice(0, countRead), cancellationToken)
        return countRead
    })
    
    // TODO: .NET 8 overloads with CancellationToken
    override this.ReadLineAsync () = task {
        let! result = input.ReadLineAsync ()
        do! output.WriteLineAsync result
        return result
    }
    
    override this.ReadToEndAsync () = task {
        let! result = input.ReadToEndAsync ()
        do! output.WriteAsync result
        return result
    }
    