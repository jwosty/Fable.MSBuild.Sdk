module App.Friend
open App.FriendOfFriend

type State = { Count: int; Text: string }

let onButtonClicked (state: State) =
    let count' = state.Count + 1
    { state with Count = count'; Text = getButtonText count' }
