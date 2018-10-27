module Fantomas.Tests.LetBindingTests

open NUnit.Framework
open FsUnit

open Fantomas.CodeFormatter
open Fantomas.Tests.TestHelper

[<Test>]
let ``let in should be preserved``() =
    formatSourceString false "let x = 1 in ()" config
    |> should equal """let x = 1 in ()
"""

[<Test>]
let ``let in should be preserved with PreserveEOL option``() =
    formatSourceString false "let x = 1 in ()" { config with PreserveEndOfLine = true }
    |> should equal """let x = 1 in ()
"""
