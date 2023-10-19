module App

let factorial n =
    let rec loop i acc =
        match i with
        | 0 | 1 -> acc
        | _ -> loop (i-1) (acc * i)
    loop n 1

let n = 5

printfn "The factorial of %d is %d" n (factorial n)

