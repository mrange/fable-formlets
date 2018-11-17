module App

open Elmish
open Elmish.React
open Fable.Helpers.React
open Fable.Helpers.React.Props

open Fable.Formlets
open Fable.Formlets.Core

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

let sampleForm =
  // Callbacks required to map the Formlet model to the MVU model
  let extractModel  (M m) = m
  let onUpdate d    mu    = d (UpdateForm mu)
  let onCommit d     v    =
    printfn "Commit: %A" v
    d Commit
  let onCancel d          = d Cancel
  let onReset  d          = d Reset

  // A labeled textual input with validation and validation feedback
  let input lbl hint v =
    Bootstrap.text hint ""
    |> v
    |> Bootstrap.withLabel lbl lbl
    |> Bootstrap.withValidationFeedback
    |> Bootstrap.withFormGroup

  let regexSocialNo = Regex "^\d{6}-\d{5}$"

  let validateSocialNo = Validate.regex regexSocialNo "You must provide a valid Social No (DDMMYY-CCCCC)."

  let individual =
    Formlet.value Individual.New
    <*> input "First name" "Enter first name" Validate.notEmpty
    <*> input "Last name"  "Enter last name"  Validate.notEmpty
    <*> input "Social no"  "Enter social no"  (Validate.notEmpty >> validateSocialNo)
    |> Bootstrap.withCard "Individual"
    |> Formlet.withAttribute (Id "Individual")
    |>> Individual

  let company =
    Formlet.value Company.New
    <*> input "Name"        "Enter company name"  Validate.notEmpty
    <*> input "Company no"  "Enter company no"    Validate.notEmpty
    |> Bootstrap.withCard "Company"
    |> Formlet.withAttribute (Id "Company")
    |>> Company

  let entity =
    Bootstrap.select 0 [|"Individual", individual; "Company", company|] 
    |> Bootstrap.withLabel "select-entity" "Individual or Company?"
    |> Bootstrap.withFormGroup
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
    |> Bootstrap.withCard lbl

  // The customer formlet
  //  Uses Applicative Functor apply to apply the collected
  //  values to Customer.New
  let newCustomer =
    Formlet.value NewCustomer.New
    <*> entity
    <*> address "Invoice address"
    // Note the user of withOption to create an optional delivery address input
    <*> (address "Delivery address" |> Bootstrap.withOption "delivery-address?" "Use delivery address?")

  // Make it into a form
  newCustomer |> Bootstrap.asForm extractModel onUpdate onCommit onCancel onReset

let init () = M Model.Empty

// Handles view messages
let update msg (M model) =
  match msg with
  | Commit        -> M model
  | Cancel        -> M model
  | Reset         -> init ()
  | UpdateForm mu ->
    // Updates the embedded Formlet model
    // Prints to console for debugging
    let before = model
    printfn "Update - msg   : %A" msg
    printfn "Update - before: %A" before
    let after  = Form.update mu model
    printfn "Update - after : %A" after
    M after

// The view
let view model dispatch =
  div
    [|Style [CSSProp.Margin "12px"]|]
    [|Form.view sampleForm model dispatch|]

// App
Program.mkSimple init update view
|> Program.withReact "elmish-app"
|> Program.withConsoleTrace
|> Program.run
