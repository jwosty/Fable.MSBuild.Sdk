module App
open App.Friend

open Browser.Dom

// Mutable state to keep track of the number of times we clicked the button, and the button text
let mutable buttonState = { Count = 0; Text = "" }

// Get a reference to our button and cast the Element to an HTMLButtonElement
let myButton = document.querySelector(".my-button") :?> Browser.Types.HTMLButtonElement

// Register our listener
myButton.onclick <- fun _ ->
    buttonState <- onButtonClicked buttonState
    myButton.innerText <- buttonState.Text
