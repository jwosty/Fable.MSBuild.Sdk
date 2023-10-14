module App
open App.Friend

open Browser.Dom

// Mutable variable to count the number of times we clicked the button
let mutable buttonState = { Count = 0; ButtonText = "" }

// Get a reference to our button and cast the Element to an HTMLButtonElement
let myButton = document.querySelector(".my-button") :?> Browser.Types.HTMLButtonElement

// Register our listener
let handler = createClickHandler (fun text -> myButton.innerText <- text)

myButton.onclick <- fun _ ->
    buttonState <- onButtonClicked buttonState handler
    myButton.innerText <- buttonState.Text
