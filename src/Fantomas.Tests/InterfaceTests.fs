﻿module Fantomas.Tests.InterfaceTests

open NUnit.Framework
open FsUnit

open Fantomas.CodeFormatter
open Fantomas.Tests.TestHelper

[<Test>]
let ``interfaces and inheritance``() =
    formatSourceString false """
type IPrintable =
   abstract member Print : unit -> unit

type SomeClass1(x: int, y: float) =
   interface IPrintable with 
      member this.Print() = printfn "%d %f" x y
type Interface3 =
    inherit Interface1
    inherit Interface2
    abstract member Method3 : int -> int""" config
    |> prepend newline
    |> should equal """
type IPrintable = 
    abstract Print : unit -> unit

type SomeClass1(x : int, y : float) = 
    interface IPrintable with
        member this.Print() = printfn "%d %f" x y

type Interface3 = 
    inherit Interface1
    inherit Interface2
    abstract Method3 : int -> int
"""

[<Test>]
let ``should not add with to inface definitions``() =
    formatSourceString false """type Text(text : string) = 
    interface IDocument
        
    interface Infrastucture with
        member this.Serialize sb = sb.AppendFormat("\"{0}\"", escape v)
        member this.ToXml() = v :> obj
    """ config
    |> should equal """type Text(text : string) = 
    interface IDocument
    interface Infrastucture with
        member this.Serialize sb = sb.AppendFormat("\"{0}\"", escape v)
        member this.ToXml() = v :> obj
"""

[<Test>]
let ``object expressions``() =
    formatSourceString false """let obj1 = { new System.Object() with member x.ToString() = "F#" }""" config
    |> prepend newline
    |> should equal """
let obj1 = 
    { new System.Object() with
          member x.ToString() = "F#" }
"""

[<Test>]
let ``object expressions and interfaces``() =
    formatSourceString false """
    let implementer() = 
        { new ISecond with 
            member this.H() = ()
            member this.J() = ()
          interface IFirst with 
            member this.F() = ()
            member this.G() = () }""" config
    |> prepend newline
    |> should equal """
let implementer() = 
    { new ISecond with
          member this.H() = ()
          member this.J() = ()
      interface IFirst with
          member this.F() = ()
          member this.G() = () }
"""