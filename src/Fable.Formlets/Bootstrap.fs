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

type IFormletComponent =
  interface
    abstract processorConnected     : unit -> unit
    abstract processorDisconnected  : unit -> unit
    abstract updateModel            : Model -> unit
  end

[<RequireQualifiedAccess>]
type FormletProcessorMessage =
  | Quit
  | Set       of IFormletComponent
  | Delta     of ModelUpdate
  | Update

type FormletProcessor = MailboxProcessor<FormletProcessorMessage>

type FormletProps<'T> =
  {
    Formlet   : Formlet<'T>
    Processor : FormletProcessor
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

  interface IFormletComponent with
    member x.processorConnected     () = ()
    member x.processorDisconnected  () = ()
    member x.updateModel            m  = x.setState (fun _ _ -> { Model = m })

  member x.dispatchUpdate mu : unit =
    x.props.Processor.Post (FormletProcessorMessage.Delta mu)

  member x.commit tv : unit =
    x.props.OnCommit tv

  member x.cancel () : unit =
    x.props.OnCancel ()

  member x.reset () : unit =
    x.setState (fun _ _ -> { Model = Model.Empty })

  override x.render() : ReactElement =
    let t             = x.props.Formlet
    let t             = adapt t
    let fc            = FormletContext.New 1000
    let tv, tvt, tft  = invoke t fc [] x.state.Model (Dispatcher.D x.dispatchUpdate)

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
    Ft <| fun fc mp m d ->
      let mp            = (ModelPathElement.Named lbl)::mp
      let tv, tvt, tft  = invoke t fc mp m d
      let tes           = flatten tvt
      let d             =
        flip div
          [|
            div [|Class "card-header" |]  [|str lbl|]
            div [|Class "card-body"   |]  tes
          |]
      let tvt           = delayedElement d [] "card" [MarginBottom "12px"]

      tv, tvt, tft

  /// Primitive button input formlet
  let inline button lbl : Formlet<float> =
    let btn = flip button [|str lbl|]
    Ft <| fun fc mp m d ->
      let v =
        match m with
        | Model.Number v  -> v
        | _               -> 0.0

      let aa : IHTMLProp list =
          [
            OnClick <| fun _ -> number d (v + 1.0)
            Type    "button"
          ]

      let tvt = delayedElement btn aa "btn" []

      v, tvt, zero ()

  /// Primitive text input formlet
  let inline text hint initial : Formlet<string> =
    Ft <| fun fc mp m d ->
      let v =
        match m with
        | Model.String v  -> v
        | _               -> initial

      let aa : IHTMLProp list =
          [
// TODO: OnBlur preferable as it forces less rebuilds, but default value causes resets to be missed
//            DefaultValue  v
//            OnBlur        <| fun v -> update d (box v.target :?> Fable.Import.Browser.HTMLInputElement).value
            OnChange      <| fun v -> string_ d v.Value
            Placeholder   hint
            Value         v
          ]

      let tvt = delayedElement input aa "form-control" []

      v, tvt, zero ()

  /// Primitive labeled checkbox input formlet
  ///   Requires an id to associate the label with the checkbox
  let inline checkBox lbl : Formlet<bool> =
    Ft <| fun fc mp m d ->
      let id        = FormletContext.NextId fc
      let isChecked =
        match m with
        | Model.Bool b      -> b
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
                OnChange  <| fun v -> bool_ d v.Checked
              |]
            label [|HTMLAttr.HtmlFor id|] [|str lbl|]
          |]
      let tvt       = delayedElement d [] "form-check" []

      isChecked, tvt, zero ()

  /// Primitive select input formlet
  let inline select initial (options : (string*'T) array) : Formlet<'T> =
    if options.Length = 0 then failwithf "select requires 1 or more options"

    let opts =
      options
      |> Array.mapi (fun i (v, _) -> option [|Value (string i)|] [|str v|])
    let sel = flip select opts

    Ft <| fun fc mp m d ->
      let i =
        match m with
        | Model.Number v  -> int v
        | _               -> initial

      let i = clamp i 0 (options.Length - 1)

      let aa : IHTMLProp list =
          [
            OnChange  <| fun v -> number d (float v.Value)
            Value     (string i)
          ]

      let tvt   = delayedElement sel aa "form-control" []

      let v = options.[i]

      snd v, tvt, zero ()

  /// Adds a validation feedback to a Formlet
  let inline withValidationFeedback t : Formlet<_> =
    let t = adapt t
    Ft <| fun fc mp m d ->
      let tv, tvt, tft  = invoke t fc mp m d
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
    checkBox lbl >>= (fun v -> if v then Formlet.map Some t else Formlet.value None)

  /// Wraps the Formlet in a div with class "form-group"
  let inline withFormGroup t = Formlet.withContainer div t |> Formlet.withClass "form-group"

  // Creates a Form element from a formlet
  //  onCommit is called when user clicks Commit
  //  onCancel is called when user clicks Cancel
  let inline mkForm formlet processor onCommit onCancel : ReactElement =
    ofType<FormletComponent<_>,_,_> { Formlet = formlet; Processor = processor; OnCommit = onCommit; OnCancel = onCancel } []

  let inline mkProcessor () = 
    let processor (mbp : MailboxProcessor<FormletProcessorMessage>) = 
      let rec loop (comp : IFormletComponent option) (model : Model) (deltas : ModelUpdate list) =
        let folder d s = Formlet.update s d
        async {
          let! receive = mbp.Receive ()
          printfn "Received: %A" receive
          return! 
            match receive with
            | FormletProcessorMessage.Quit     -> 
              async.Return ()
            | FormletProcessorMessage.Set c    -> 
              match comp with
              | Some c -> c.processorDisconnected ()
              | None   -> ()
              c.processorConnected ()
              c.updateModel model
              loop (Some c) model deltas
            | FormletProcessorMessage.Delta mu ->
              mbp.Post FormletProcessorMessage.Update
              loop comp model (mu::deltas)
            | FormletProcessorMessage.Update   ->
              if deltas |> List.isEmpty then loop comp model deltas
              else
                let model = List.foldBack folder deltas model
                match comp with
                | Some c -> c.updateModel model
                | None   -> ()
                loop comp model deltas
        }
      loop None Model.Empty []
    MailboxProcessor.Start processor

