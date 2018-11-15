# fable-formlets
Fable Formlets (Applicative Functor Forms kind of)

> Based upon fable2-samples

## Why formlets

Please see: [Why and what is formlets?](WHY.md)

## Requirements

* [dotnet SDK](https://www.microsoft.com/net/download/core) 2.0 or higher
* [node.js](https://nodejs.org) with [npm](https://www.npmjs.com/)
* An F# editor like Visual Studio, Visual Studio Code with [Ionide](http://ionide.io/) or [JetBrains Rider](https://www.jetbrains.com/rider/).

## Building and running the app

* Install JS dependencies: `npm install`
* Move to `src/Fable.Formlets.App`
* Install F# dependencies: `dotnet restore .`
* Start Fable and Webpack dev server: `dotnet fable webpack-dev-server`
* After the first compilation is finished, in your browser open: http://localhost:8080/

Any modification you do to the F# code will be reflected in the web page after saving.

