module Fantomas.Tests.FSharpScriptTests

open Fantomas
open NUnit.Framework
open FsUnit
open Fantomas.Tests.TestHelper
open System.IO

[<Test>]
let ``source _directory keyword should not be replace with actual path`` () =
    formatSourceString false """
#I __SOURCE_DIRECTORY__
#load ".paket/load/net471/main.group.fsx"
"""  config
    |> should equal """#I __SOURCE_DIRECTORY__
#load ".paket/load/net471/main.group.fsx"
"""

[<Test>]
let ``e2e script test with keyword __source__directory__`` () =
    let source = """
#I       __SOURCE_DIRECTORY__
#load    ".paket/load/net471/main.group.fsx"
"""

    let file = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N") + ".fsx")
    File.WriteAllText(file, source)
    
    let formattedFiles = FakeHelpers.formatCode config [file]
    
    let formattedSource = File.ReadAllText(file)
    List.length formattedFiles == 1
    File.Delete(file)
    
    formattedSource
    |> String.normalizeNewLine
    |> should equal """#I __SOURCE_DIRECTORY__
#load ".paket/load/net471/main.group.fsx"
"""

[<Test>]
let ``fantomas removes module and namespace if it is only 1 word`` () =
    let source = """namespace Shared   

type Counter = int
"""

    let file = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N") + ".fsx")
    File.WriteAllText(file, source)
    
    let fantomasConfig =
        { FormatConfig.FormatConfig.Default with
            StrictMode = true
            IndentSpaceNum = 2
            SpaceBeforeColon = false }
    let formattedFiles = FakeHelpers.formatCode fantomasConfig [file]
    
    let formattedSource = File.ReadAllText(file)
    List.length formattedFiles == 1
    File.Delete(file)
    
    formattedSource
    |> String.normalizeNewLine
    |> should equal """namespace Shared

type Counter = int
"""