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

open System.Text.RegularExpressions

// The sample formlet collects data for a Customer and upon commit produces a
//   Customer value

type Individual =
  {
    FirstName : string
    LastName  : string
    SocialNo  : string
  }
  static member New fn ln sno : Individual = { FirstName = fn; LastName = ln; SocialNo = sno }

type Company =
  {
    Name      : string
    CompanyNo : string
  }
  static member New n cno : Company = { Name = n; CompanyNo = cno }

type Entity =
  | Individual  of Individual
  | Company     of Company

// Basic Address model
type Address =
  {
    CarryOver : string
    Name      : string
    Street1   : string
    Street2   : string
    Street3   : string
    Zip       : string
    City      : string
    County    : string
    Country   : string
  }
  static member New co n s1 s2 s3 zip city county country =
    {
      CarryOver = co
      Name      = n
      Street1   = s1
      Street2   = s2
      Street3   = s3
      Zip       = zip
      City      = city
      County    = county
      Country   = country
    }

// Basic NewCustomer model with optional delivery address
type NewCustomer =
  {
    Entity          : Entity
    InvoiceAddress  : Address
    DeliveryAddress : Address option
  }
  static member New e ia da =
    {
      Entity          = e
      InvoiceAddress  = ia
      DeliveryAddress = da
    }

type MyModel    = M of Model
type MyMessage  =
  | Commit
  | Cancel
  | Reset
  | UpdateForm of ModelUpdate

let sampleFormlet =
  // A labeled textual input with validation and validation feedback
  let input lbl hint v =
    Formlet.text hint ""
    |> v
    |> Formlet.withLabel lbl
    |> Formlet.withValidationFeedback
    |> Formlet.withFormGroup

  let regexSocialNo = Regex "^\d{6}-\d{5}$"

  let validateSocialNo = Validate.regex regexSocialNo "You must provide a valid Social No (DDMMYY-CCCCC)."

  let individual =
    Formlet.value Individual.New
    <*> input "First name" "Enter first name" Validate.notEmpty
    <*> input "Last name"  "Enter last name"  Validate.notEmpty
    <*> input "Social no"  "Enter social no"  (Validate.notEmpty >> validateSocialNo)
    |> Formlet.withSubModel "Individual"
    |> Formlet.withCard "Individual"
    |>> Individual

  let company =
    Formlet.value Company.New
    <*> input "Name"        "Enter company name"  Validate.notEmpty
    <*> input "Company no"  "Enter company no"    Validate.notEmpty
    |> Formlet.withSubModel "Company"
    |> Formlet.withCard "Company"
    |>> Company

  let entity =
    Formlet.select 0 [|"Individual", individual; "Company", company|]
    |> Formlet.withLabel "Individual or Company?"
    |> Formlet.withFormGroup
    |> Formlet.unlift

  // The address formlet
  //  Uses Applicative Functor apply to apply the collected
  //  values to Address.New
  let address lbl =
    Formlet.value Address.New
    <*> input "Carry over"  ""  Validate.yes
    <*> input "Name"        ""  Validate.notEmpty
    <*> input "Street"      ""  Validate.notEmpty
    <*> input "Street"      ""  Validate.yes
    <*> input "Street"      ""  Validate.yes
    <*> input "Zip"         ""  Validate.notEmpty
    <*> input "City"        ""  Validate.notEmpty
    <*> input "County"      ""  Validate.yes
    <*> input "Country"     ""  Validate.notEmpty
    |> Formlet.withCard lbl

  // The customer formlet
  //  Uses Applicative Functor apply to apply the collected
  //  values to Customer.New
  Formlet.value NewCustomer.New
  <*> entity
  <*> address "Invoice address"
  // Note the user of withOption to create an optional delivery address input
  <*> (address "Delivery address" |> Formlet.withOption "Use delivery address?")

let onCommit tv = printfn "Success: %A" tv
let onCancel () = printfn "Cancelled"

let element = Formlet.mkForm sampleFormlet onCommit onCancel
ReactDom.render(element, document.getElementById("react-app"))
