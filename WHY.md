# Why and what is formlets?

A few years ago I was trying out [WebSharper](http://websharper.com/). There was much I didn't understand about WebSharper but I fell in love with the concept of Formlets/Piglets. For years I have been trying to make others see it as I do; an awesome and productive way to build reactive Forms that have validation.

Formlets are not a generic solution to all UI related problems but they are awesome for Forms (as the name implies).

Like parsers combinators like FParsec allows us build complex parsers from simple and generic building blocks Formlets allows us to build forms from simple and generic input elements.

One of the simplest forms is a simple text input element:

```fsharp
let f = text "Enter your name" ""
```

![A single input formlet](images/example-0001.PNG)

We can add a label to it:

```fsharp
let f = text "Enter your name" "" |> withLabel "name-id" "Name"
```

![A single labeled input formlet](images/example-0002.PNG)

We can create our input formlet with formlet by creating a function

```fsharp
let input lbl hint validation =
  text hint ""                // Text input
  |> validation               // Apply validation
  |> withLabel lbl lbl        // label the text input
  |> withValidationFeedback   // Display validation failures
  |> withFormGroup            // Wrap it in a form-group (Bootstrap)
```

There is a formlet computation expression which allows us to combine several inputs

```fsharp
let newUser : Formlet<string*string*string> =
  formlet {
    let! firstName  = input "First name" "Enter first name" notEmpty
    let! lastName   = input "Last name"  "Enter last name"  notEmpty
    let! nickName   = input "Nick name"  "Enter nick name"  yes
    return firstName, lastName, nickName
  } |> withCard "New User"
```

![A customer formlet](images/example-0003.PNG)

We can make a slight validation of the Formlet above by showing the user a checkbox that they tick if they like to enter a nick name.

```fsharp
let newUser : Formlet<string*string*string option> =
  formlet {
    let! firstName  = input "First name" "Enter first name" notEmpty
    let! lastName   = input "Last name"  "Enter last name"  notEmpty
    let! hasNick    = checkBox "has-nick" "Do you have a nick name?"
    let! nickName   =
      if hasNick then input "Nick name"  "Enter nick name"  notEmpty |>> Some
      else value None
    return firstName, lastName, nickName
  } |> withCard "New User"
```

![A customer formlet](images/example-0004.PNG)

The cool thing is that the user will see a checkbox that when ticked will display a required text input for the nick. Normally optional behavior in forms require some kind of event handling, not so with formlets.


I think Formlets are a great way to create great forms quickly and I wished more developers talked about them.

## References

1. [WebSharper](http://websharper.com/) - Where I first saw the formlet concept
2. [Haskell formlets](https://chrisdone.com/posts/haskell-formlets) - Not surprisingly Haskell people has been thinking about Formlets for a long time.
