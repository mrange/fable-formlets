﻿// ----------------------------------------------------------------------------------------------
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

/// Formlets are composable form elements allowing reactive forms to be created
///   from basic primitives.
module Fable.Formlets.Core

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Import.React

open System.Text
open System.Text.RegularExpressions

[<RequireQualifiedAccess>]
type ModelPathElement =
  | Named   of string

/// FormletPath is the implicit path to the Formlet elements
///   Used to generate contextful validation errors
type ModelPath = ModelPathElement list

/// FailureTree represents the failure state of a value generated
///   when invoking the Formlet. If the failure tree "IsGood" the
///   generated value is valid. Is flattened to generate human-readable
///   validation errors.
[<RequireQualifiedAccess>]
type FailureTree =
  | Empty
  | Leaf        of ModelPath*string
  | Suppress    of FailureTree
  | Fork        of FailureTree*FailureTree

  static member IsGood x =
    match x with
    | Empty
    | Suppress _  -> true
    | Leaf _
    | Fork (_, _) -> false

  static member Zero        : FailureTree = Empty
  static member Join (l, r) : FailureTree =
    match l, r with
    | Empty       , _           -> r
    | _           , Empty       -> l
    | Suppress l  , Suppress r  -> Suppress (Fork (l, r))
    | _           , _           -> Fork (l, r)

/// Model of the Formlet state
[<RequireQualifiedAccess>]
type Model =
  | Empty
  | SubModel  of string*Model
  | Value     of string
  | Fork      of Model*Model

  static member Zero        : Model = Empty
  static member Join (l, r) : Model = Fork (l, r)

/// ModelUpdate is generated by view events, it points out
//    where in the model a change should be applied
[<RequireQualifiedAccess>]
type ModelUpdate =
  | Update    of string
  | SubModel  of string*ModelUpdate
  | Left      of ModelUpdate
  | Right     of ModelUpdate

/// ViewTree is generated by the Formlet and represents
///   the view to generate. Is flattened when creating container elements.
[<RequireQualifiedAccess>]
type ViewTree =
  | Empty
  | Element         of ReactElement
  // DelayedElement is needed when an element should render differently depending
  //  on the validation results or attributes attached using with(Attribute|Class|Style)
  //  The reason for this design is that elements are immutable after construction but
  //  a validation error is usually discovered after the visual element is created in
  //  the tree. An alternative implementation would be to instead pass the validation
  //  function when traversing the formlets and evaluate it when creating the visual
  //  element. This design felt more complex but potentially more performant.
  | DelayedElement  of (IHTMLProp list -> string -> CSSProp list -> ReactElement)
  | WithAttribute   of IHTMLProp*ViewTree
  | WithClass       of string*ViewTree
  | WithStyle       of CSSProp*ViewTree
  | Fork            of ViewTree*ViewTree

  static member Zero        : ViewTree = Empty
  static member Join (l, r) : ViewTree =
    match l, r with
    | Empty       , _           -> r
    | _           , Empty       -> l
    | _           , _           -> Fork (l, r)

type FormletContext =
  | Fc of (unit -> string)

  static member NextId (Fc g) : string = g ()

  static member New initial =
    let mutable i = initial
    Fc (fun () -> i <- i + 1; sprintf "id-%d" i)

/// Dispatcher is a callback used by the view to indicate a model change.
type Dispatcher =
  | D of (ModelUpdate -> unit)

  static member inline Update   (D d) v  : unit        = d (ModelUpdate.Update v)
  static member inline Left     (D d)    : Dispatcher  = D (fun mu -> d (ModelUpdate.Left mu))
  static member inline Right    (D d)    : Dispatcher  = D (fun mu -> d (ModelUpdate.Right mu))
  static member inline SubModel (D d) n  : Dispatcher  = D (fun mu -> d (ModelUpdate.SubModel (n, mu)))

/// Formlet is a function that given a path to the current model element, a model element
///   and dispatcher function generates a value 'T, the view tree and the failure tree.
type Formlet<'T> = Ft of (FormletContext -> ModelPath -> Model -> Dispatcher -> 'T*ViewTree*FailureTree)

module Details =
  // TODO: Why doesn't this work?
  //let inline adapt  (Ft f)        = OptimizedClosures.FSharpFunc<_, _, _, _, _>.Adapt f
  //let inline invoke f fp ps m d   = (f : OptimizedClosures.FSharpFunc<_, _, _, _, _>).Invoke (fp, ps, m, d)
  let inline adapt  (Ft f)        = f
  let inline invoke f fc mp m d   = f fc mp m d

  let inline flip f a b           = f b a

  let inline isGood ft            = FailureTree.IsGood ft

  let inline clamp v f t          = if v < f then v elif t < v then t else v

  let rec pathStringLoop (sb : StringBuilder) ps =
    let inline app s = sb.Append (s : string) |> ignore
    match ps with
    | []                                -> ()
    | (ModelPathElement.Named n)::ps  -> pathStringLoop sb ps; app "."; app n

  let pathToString ps =
    let sb = StringBuilder 16
    pathStringLoop sb ps
    sb.ToString ()
  let inline update   d v         = Dispatcher.Update   d v
  let inline left     d           = Dispatcher.Left     d
  let inline right    d           = Dispatcher.Right    d
  let inline subModel d n         = Dispatcher.SubModel d n

  let inline zero     ()          = LanguagePrimitives.GenericZero<_>
  let inline join     l r         = (^T : (static member Join : ^T * ^T -> ^T) (l, r))
  let inline flatten  l           = (^T : (static member Flatten : ^T  -> ^U) l)
  let inline toString l           = (^T : (member ToString : unit  -> string) l)

  let inline delayedElement_ e    =
    ViewTree.DelayedElement <| fun aa cc ss ->
      let aa = (Class cc :> IHTMLProp)::(Style ss :> IHTMLProp)::aa
      e aa
  let inline delayedElement e a c s  =
    ViewTree.DelayedElement <| fun aa cc ss ->
      let aa = a@aa
      let cc = c + " " + cc
      let ss = s@ss
      let aa = (Class cc :> IHTMLProp)::(Style ss :> IHTMLProp)::aa
      e aa

  module Loops =
    module Form =
      let rec update msg m =
        match msg, m with
        | ModelUpdate.Update v        , _                                   -> Model.Value v
        | ModelUpdate.SubModel (n, mu), Model.SubModel (nn, m) when n = nn  -> Model.SubModel (n, update mu m)
        | ModelUpdate.SubModel (n, mu), _                                   -> Model.SubModel (n, update mu (zero ()))
        | ModelUpdate.Left   u        , Model.Fork (l, r)                   -> Model.Fork (update u l, r)
        | ModelUpdate.Right  u        , Model.Fork (l, r)                   -> Model.Fork (l, update u r)
        // mu is either left or right, create a new fork and update it
        | _                           , _                                   -> update msg (Model.Fork (zero (), zero ()))

    module FailureTree =
      let rec flatten (ra : ResizeArray<_>) suppress ft =
        match ft with
        | FailureTree.Empty       -> ()
        | FailureTree.Leaf (p, m) -> ra.Add (suppress, p, m)
        | FailureTree.Suppress ft -> flatten ra true ft
        | FailureTree.Fork (l, r) ->
          flatten ra suppress l
          flatten ra suppress r

    module ViewTree =
      let rec flatten (ra : ResizeArray<_>) aa cc ss vt =
        match vt with
        | ViewTree.Empty                  -> ()
        | ViewTree.Element        e       -> ra.Add e
        | ViewTree.DelayedElement d       -> ra.Add (d aa cc ss)
        | ViewTree.WithAttribute  (a, t)  -> flatten ra (a::aa) cc ss t
        | ViewTree.WithClass      (c, t)  -> flatten ra aa (c + " " + cc) ss t
        | ViewTree.WithStyle      (s, t)  -> flatten ra aa cc (s::ss) t
        | ViewTree.Fork           (l, r)  ->
          flatten ra aa cc ss l
          flatten ra aa cc ss r

  module Combinations =
    let apply     f s = f s
    let andAlso   f s = f, s
    let keepLeft  f _ = f
    let keepRight _ s = s
open Details

type FailureTree with
  static member Flatten ft  : (bool*ModelPath*string) array =
    let ra = ResizeArray<_> 16
    Loops.FailureTree.flatten ra false ft
    ra.ToArray ()

type ViewTree with
  static member Flatten vt : ReactElement array =
    let ra = ResizeArray<_> 16
    Loops.ViewTree.flatten ra [] "" [] vt
    ra.ToArray ()

/// Formlets are composable form elements allowing reactive forms to be created
///   from basic primitives.
module Formlet =
  let inline update m msg : Model = Loops.Form.update msg m

  // TODO: do we benefit from inlining these functions?

  /// A Formlet that always produces value and no visual element
  let inline value v : Formlet<_> =
    Ft <| fun fc mp m d ->
      v, zero (), zero ()

  let inline lift v = value v

  /// Monadic bind for Formlets, usually you should try to use
  ///   apply over bind as it is allows for better caching of resources.
  let inline bind t uf : Formlet<_> =
    let t = adapt t
    Ft <| fun fc mp m d ->
      let tm, um =
        match m with
        | Model.Fork (l, r) -> l, r
        | _                 -> zero (), zero ()

      let tv, tvt, tft  = invoke t fc mp tm (left d)
      let u             = uf tv
      let u             = adapt u
      let uv, uvt, uft  = invoke u fc mp um (right d)

      uv, join tvt uvt, join tft uft

  let inline unlift t : Formlet<_> =
    bind t id

  // Combines the result of two formlets using a combination function
  let inline combine f t u : Formlet<_> =
    let t = adapt t
    let u = adapt u
    Ft <| fun fc mp m d ->
      let tm, um =
        match m with
        | Model.Fork (l, r) -> l, r
        | _                 -> zero (), zero ()

      let tv, tvt, tft  = invoke t fc mp tm (left d)
      let uv, uvt, uft  = invoke u fc mp um (right d)

      (f tv uv), join tvt uvt, join tft uft

  /// Applicative functor apply
  let inline apply     t u : Formlet<_> = combine Combinations.apply     t u
  /// Combines the result of two formlets as a pair
  let inline andAlso   t u : Formlet<_> = combine Combinations.andAlso   t u
  /// Combines the result of two formlets, keep left result
  let inline keepLeft  t u : Formlet<_> = combine Combinations.keepLeft  t u
  /// Combines the result of two formlets, keep right result
  let inline keepRight t u : Formlet<_> = combine Combinations.keepRight t u

  /// Functor map
  let inline map t f : Formlet<_> =
    let t = adapt t
    Ft <| fun fc mp m d ->
      let tv, tvt, tft  = invoke t fc mp m d

      f tv, tvt, tft

  let inline withSubModel n t : Formlet<_> =
    let t = adapt t
    Ft <| fun fc mp m d ->
      let sm =
        match m with
        | Model.SubModel (nn, sm) when n = nn -> sm
        | _                                   -> zero ()
      invoke t fc mp sm (subModel d n)

  /// Appends an attribute to visual element of t
  ///  Note; Class and Style should be append using withClass and withStyle to
  ///  allow aggregating of them
  let inline withAttribute p t : Formlet<_> =
    let t = adapt t
    Ft <| fun fc mp m d ->
      let tv, tvt, tft  = invoke t fc mp m d
      let tvt           = ViewTree.WithAttribute (p, tvt)

      tv, tvt, tft

  /// Appends a class to visual element of t
  let inline withClass c t : Formlet<_> =
    let t = adapt t
    Ft <| fun fc mp m d ->
      let tv, tvt, tft  = invoke t fc mp m d
      let tvt           = ViewTree.WithClass (c, tvt)

      tv, tvt, tft

  /// Appends a style to visual element of t
  let inline withStyle s t : Formlet<_> =
    let t = adapt t
    Ft <| fun fc mp m d ->
      let tv, tvt, tft  = invoke t fc mp m d
      let tvt           = ViewTree.WithStyle (s, tvt)

      tv, tvt, tft

  /// Wraps the visual element of t inside a container (like div)
  let inline withContainer c t : Formlet<_> =
    let t = adapt t
    Ft <| fun fc mp m d ->
      let tv, tvt, tft  = invoke t fc mp m d
      let tes           = flatten tvt
      let d             = (flip c) tes
      let tvt           = delayedElement_ d

      tv, tvt, tft

  /// Adds a label to a Formlet
  ///   Requires an id to associate the label with the visual element
  let inline withLabel lbl t : Formlet<_> =
    let t = adapt t
    Ft <| fun fc mp m d ->
      let id            = FormletContext.NextId fc
      let mp            = (ModelPathElement.Named lbl)::mp
      let tv, tvt, tft  = invoke t fc mp m d
      let e             = label [|HTMLAttr.HtmlFor id|] [|str lbl|]
      let tvt           = ViewTree.WithAttribute (Id id, tvt)
      let tvt           = join (ViewTree.Element e) tvt

      tv, tvt, tft

  /// Computation expression builder for formlets
  type Builder () =
    class
      member inline x.Bind       (t, uf) = bind  t uf
      member inline x.Return     v       = value v
      member inline x.ReturnFrom t       = t : Formlet<_>
      member inline x.Zero       ()      = value ()
    end

/// Computation expression builder for formlets
let formlet = Formlet.Builder ()

type Formlet<'T> with
  static member inline (>>=) (t, uf) = Formlet.bind  t uf
  static member inline (<*>) (f, t)  = Formlet.apply f t
  static member inline (|>>) (t, f)  = Formlet.map   t f

  static member inline (<&>) (f, t)  = Formlet.andAlso   f t
  static member inline (.>>.)(f, t)  = Formlet.andAlso   f t
  static member inline (.>>) (f, t)  = Formlet.keepLeft  f t
  static member inline (>>.) (f, t)  = Formlet.keepRight f t

/// Formlets combinators that validate the result of a Formlet
module Validate =

  /// Always succeeds
  let inline yes t : Formlet<_> = t

  /// Formlet fails to validate with msg if v return false
  let inline test (v : 'T -> bool) (msg : string) t : Formlet<_> =
    let t = adapt t
    Ft <| fun fc mp m d ->
      let tv, tvt, tft  = invoke t fc mp m d
      let valid         = v tv
      let tft           = if valid then tft else join tft (FailureTree.Leaf (mp, msg))
      // TODO: This is really bootstrap and this should be moved into bootstrap somehow
      let tvt           = ViewTree.WithClass ((if valid then "is-valid" else "is-invalid"), tvt)

      tv, tvt, tft

  /// Formlet fails to validate if empty string
  let inline notEmpty t : Formlet<string> =
    t
    |> Formlet.withAttribute (Required true)
    |> test (fun v -> String.length v > 0) "You must provide a value."

  /// Formlet fails to validate with msg if string don't match regex r
  let inline regex (r : Regex) (msg : string) t : Formlet<string> =
    test r.IsMatch msg t
