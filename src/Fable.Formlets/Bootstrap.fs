// ----------------------------------------------------------------------------------------------
// Copyright 2018 Mårten Rånge
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------------------

/// Formlets based on the popular CSS library Bootstrap
module Fable.Formlets.Bootstrap

open Fable.Import.React

open Fable.Helpers.React
open Fable.Helpers.React.Props

open Fable.Formlets.Core
open Fable.Formlets.Core.Details

open System.Text

type FormletProps<'T> = 
  { 
    Formlet   : Formlet<'T>
    OnCommit  : 'T -> unit
    OnCancel  : unit -> unit
  }

type FormletState = 
  { 
    Model : Model
  }
  static member Zero : FormletState = { Model = Model.Empty }

type FormletComponent<'T>(initialProps : FormletProps<'T>) =
  inherit Component<FormletProps<'T>, FormletState>(initialProps)
  do
    base.setInitState FormletState.Zero

  member x.updateModel mu : unit =
    let m = Formlet.update mu x.state.Model
    x.setState { Model = m }

  member x.commit tv : unit =
    x.props.OnCommit tv

  member x.cancel () : unit =
    x.props.OnCancel ()

  member x.reset () : unit =
    x.setState { Model = Model.Empty }
    ()

  override x.render() : ReactElement =
    let t             = x.props.Formlet
    let t             = adapt t
    let ig            = IdGenerator.New 1000
    let tv, tvt, tft  = invoke t ig [] x.state.Model (Dispatcher.D x.updateModel)

    let tes           = flatten tvt
    let tfs           = flatten tft
    let valid         = isGood tft
    let onCommit _    = if valid then x.commit tv else ()
    let onCancel _    = x.cancel ()
    let onReset  _    = x.reset ()
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
            OnClick action
            Style   [CSSProp.MarginRight "8px"]
            Type    "button"
          |]
          [|str lbl|]
      div
        [|Style [CSSProp.MarginBottom "12px"; CSSProp.MarginTop "12px"]|]
        [|
          btn onCommit  "btn btn-primary" "Commit" (not valid)
          btn onCancel  "btn"             "Cancel" false
          btn onReset   "btn"             "Reset"  false
        |]

    form
      [|Style [CSSProp.Margin "12px"]|]
      [|
        be
        ul
        (if tes.Length > 0 then div [||] tes else tes.[0])
        be
      |]

module Formlet =

  /// Wraps the visual element of t inside a labeled card container
  ///   The label is added to the formlet path
  let inline withCard lbl t : Formlet<_> =
    let t = adapt t
    Ft <| fun ig fp m d ->
      let fp            = (FormletPathElement.Named lbl)::fp
      let tv, tvt, tft  = invoke t ig fp m d
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
  let inline text hint initial : Formlet<string> =
    Ft <| fun ig fp m d ->
      let v =
        match m with
        | Model.Value v -> v
        | _             -> initial

      let aa : IHTMLProp list =
          [
// TODO: OnBlur preferable as it forces less rebuilds, but default value causes resets to be missed
//            DefaultValue  v
//            OnBlur        <| fun v -> update d (box v.target :?> Fable.Import.Browser.HTMLInputElement).value
            OnChange      <| fun v -> update d v.Value
            Placeholder   hint
            Value         v
          ]

      let tvt = delayedElement input aa "form-control" []

      v, tvt, zero ()

  /// Primitive labeled checkbox input formlet
  ///   Requires an id to associate the label with the checkbox
  let inline checkBox lbl : Formlet<bool> =
    Ft <| fun ig fp m d ->
      let id        = IdGenerator.Next ig
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
                Id        id
                Class     "form-check-input"
                Type      "checkbox"
                OnChange  <| fun v -> update d (if isChecked then "" else "on")
              |]
            label [|HTMLAttr.HtmlFor id|] [|str lbl|]
          |]
      let tvt       = delayedElement d [] "form-check" []

      isChecked, tvt, zero ()

  /// Primitive select input formlet
  let inline select initial (options : (string*'T) array) : Formlet<'T> =
    if options.Length = 0 then failwithf "select requires 1 or more options"

    let options_ =
      options
      |> Array.mapi (fun i (v, _) -> option [|Value (string i)|] [|str v|])

    Ft <| fun ig fp m d ->
      let i =
        match m with
        | Model.Value v ->
          let b, i  = System.Int32.TryParse v
          if b then i else 0
        | _             -> initial

      let i = clamp i 0 (options.Length - 1)

      let aa : IHTMLProp list =
          [
            OnChange  <| fun v -> update d v.Value
            Value     (string i)
          ]

      let d     =
        flip select options_
      let tvt   = delayedElement d aa "form-control" []

      let v = options.[i]

      snd v, tvt, zero ()

  /// Adds a label to a Formlet
  ///   Requires an id to associate the label with the visual element
  let inline withLabel lbl t : Formlet<_> =
    let t = adapt t
    Ft <| fun ig fp m d ->
      let id            = IdGenerator.Next ig
      let fp            = (FormletPathElement.Named lbl)::fp
      let tv, tvt, tft  = invoke t ig fp m d
      let e             = label [|HTMLAttr.HtmlFor id|] [|str lbl|]
      let tvt           = ViewTree.WithAttribute (Id id, tvt)
      let tvt           = join (ViewTree.Element e) tvt

      tv, tvt, tft

  /// Adds a validation feedback to a Formlet
  let inline withValidationFeedback t : Formlet<_> =
    let t = adapt t
    Ft <| fun ig fp m d ->
      let tv, tvt, tft  = invoke t ig fp m d
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
  let inline withOption lbl t : Formlet<_ option> =
    checkBox lbl >>= (fun v -> if v then Formlet.map t Some else Formlet.value None)

  /// Wraps the Formlet in a div with class "form-group"
  let inline withFormGroup t = Formlet.withContainer div t |> Formlet.withClass "form-group"

  // Creates a Form element from a formlet
  //  onCommit is called when user clicks Commit
  //  onCancel is called when user clicks Cancel
  let inline mkForm formlet onCommit onCancel : ReactElement = 
    ofType<FormletComponent<_>,_,_> { Formlet = formlet; OnCommit = onCommit; OnCancel = onCancel } []
