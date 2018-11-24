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

module App

open Fable.Import
open Fable.Import.Browser
open Fable.Formlets.Core
open Fable.Formlets.Bootstrap

open Formlet
open Validate

open System.Text.RegularExpressions

// -----------------------------------------------------------------------------















let sample = text "Enter name" ""
















// -----------------------------------------------------------------------------

let onCommit tv = printfn "Success: %A" tv
let onCancel () = printfn "Cancelled"

let element = Formlet.mkForm sample onCommit onCancel
ReactDom.render(element, document.getElementById("react-app"))
