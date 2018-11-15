/// Formlets based on the popular CSS library Bootstrap
module Fable.Formlets.Bootstrap

open Fable.Formlets.Core
open Fable.Formlets.Core.Details

open Fable.Helpers.React
open Fable.Helpers.React.Props

open System.Text

/// Wraps the visual element of t inside a labeled card container
///   The label is added to the formlet path
let withCard lbl t : Formlet<_> =
  let t = adapt t
  Ft <| fun fp m d ->
    let fp            = (FormletPathElement.Named lbl)::fp
    let tv, tvt, tft  = invoke t fp m d
    let tes           = flatten tvt
    let d             =
      flip div
        [|
          div [|Class "card-header" |]  [|str lbl|]
          div [|Class "card-body"   |]  tes
        |]
    let tvt           = delayedElement d [] "card" [MarginBottom "12px"]

    tv, tvt, tft

/// Primitive text input formlet
let text hint initial : Formlet<string> =
  Ft <| fun fp m d ->
    let v =
      match m with
      | Model.Value v -> v
      | _             -> initial

    let aa : IHTMLProp list =
        [
// TODO: OnBlur preferable as it forces less rebuilds, but default value causes resets to be missed
// TODO: Fix reset
          DefaultValue  v
          OnBlur        <| fun v -> update d (box v.target :?> Fable.Import.Browser.HTMLInputElement).value
//              OnChange      <| fun v -> update d v.Value
          Placeholder   hint
//              Value         v
        ]

    let tvt = delayedElement input aa "form-control" []

    v, tvt, zero ()

/// Primitive labeled checkbox input formlet
///   Requires an id to associate the label with the checkbox
let checkBox id lbl : Formlet<bool> =
  Ft <| fun fp m d ->
    let isChecked =
      match m with
      | Model.Value "on"  -> true
      | _                 -> false
    let d         =
      flip div
        [|
          input
            [|
              Checked   isChecked
              Class     "form-check-input"
              Id        id
              Type      "checkbox"
              OnChange  <| fun v -> update d (if isChecked then "" else "on")
            |]
          label [|HTMLAttr.HtmlFor id|] [|str lbl|]
        |]
    let tvt       = delayedElement d [] "form-check" []

    isChecked, tvt, zero ()

/// Adds a label to a Formlet
///   Requires an id to associate the label with the visual element
let withLabel id lbl t : Formlet<_> =
  let t = adapt t
  Ft <| fun fp m d ->
    let fp            = (FormletPathElement.Named lbl)::fp
    let tv, tvt, tft  = invoke t fp m d
    let e             = label [|HTMLAttr.HtmlFor id|] [|str lbl|]
    let tvt           = ViewTree.WithAttribute (Id id, tvt)
    let tvt           = join (ViewTree.Element e) tvt

    tv, tvt, tft

/// Adds a validation feedback to a Formlet
let withValidationFeedback t : Formlet<_> =
  let t = adapt t
  Ft <| fun fp m d ->
    let tv, tvt, tft  = invoke t fp m d
    if isGood tft then
      tv, tvt, tft
    else
      let tes           = flatten tft
      let sb            = StringBuilder 16
      let inline app s  = sb.Append (s : string) |> ignore
      for (suppress, fp, msg) in tes do
        if not suppress then
          app msg
          app " "
      let e =
        div [|Class "invalid-feedback"|] [|str (sb.ToString ())|]
      let tvt = join tvt (ViewTree.Element e)

      tv, tvt, tft

/// Makes a Formlet optional by displaying a check box that when ticked
///   shows the visual element for t.
///   Requires an id to associate the label with the visual element
let withOption id lbl t : Formlet<_ option> =
  checkBox id lbl >>= (fun v -> if v then Formlet.map t Some else Formlet.value None)

/// Wraps the Formlet in a div with class "form-group"
let withFormGroup t = Formlet.withContainer div t |> Formlet.withClass "form-group"

/// Maps a Formlet into a Form, a Form is usuable with Fable.Elmish MVU.
///   As the Formlet Model and the MVU model might be different the caller
///   needs to supply extractModel which extracts the Formlet Model from the
///   MVU model.
///   In addition the called provides callbacks how to handle onUpdate, onCommit,
///   onCancel and onReset.
let asForm extractModel onUpdate onCommit onCancel onReset (t : Formlet<'T>) : Form<'Model, 'Msg> =
  let t = adapt t
  F <| fun m d ->
    let tv, tvt, tft  = invoke t [] (extractModel m) (Dispatcher.D <| fun mu -> onUpdate d mu)

    let tes           = flatten tvt
    let tfs           = flatten tft
    let valid         = isGood tft
    let onCommit d    = if valid then onCommit d tv else ()
    let lis           =
      tfs
      |> Array.map (fun (s, p, m) ->
        let p   = pathToString p
        let cls = if s then "list-group-item list-group-item-warning" else "list-group-item list-group-item-danger"
        li [|Class cls|] [|str (sprintf "§ %s - %s" p m)|])
    let ul            = ul [|Class "list-group"; Style [CSSProp.MarginBottom "12px"]|] lis
    let be            =
      let inline btn action cls lbl dis =
        button
          [|
            Class   cls
            Disabled dis
            OnClick <|fun _ -> action d
            Style   [CSSProp.MarginRight "8px"]
            Type    "button"
          |]
          [|str lbl|]
      div
        [|Style [CSSProp.MarginBottom "12px"; CSSProp.MarginTop "12px"]|]
        [|
          btn onCommit "btn btn-primary" "Commit" (not valid)
          btn onCancel "btn"             "Cancel" false
          btn onReset  "btn"             "Reset"  false
        |]

    form
      [||]
      [|
        be
        ul
        (if tes.Length > 0 then div [||] tes else tes.[0])
        be
      |]

