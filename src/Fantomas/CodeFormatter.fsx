﻿#r "../../packages/FSharp.Compiler.Service/lib/net45/FSharp.Compiler.Service.dll"

#load "Utils.fs"
#load "TokenMatcher.fs"
#load "FormatConfig.fs"
#load "SourceParser.fs"
#load "SourceTransformer.fs"
#load "CodePrinter.fs"
#load "CodeFormatterImpl.fs"
#load "CodeFormatter.fs"

open Fantomas.TokenMatcher
open Fantomas.FormatConfig
open Fantomas.SourceParser
open Fantomas.CodePrinter
open Fantomas.CodeFormatter
open Microsoft.FSharp.Compiler.Range
open Microsoft.FSharp.Compiler.Interactive.Shell
open Microsoft.FSharp.Compiler.Interactive.Shell.Settings
open Fantomas

CodeFormatter.Parse ("Program.fs", """let plus a b = a + b""")

let config = FormatConfig.Default

let formatSrc (s : string) = 
    formatSourceString false (s.Replace("\r\n", "\n")) config |> printfn "%A";;

fsi.AddPrinter (fun (p : pos) -> p.ToString())
fsi.AddPrinter (fun (r : range) -> r.ToString())

"""let pattern = 
    (x + y)
      .Replace(seperator + "**" + seperator, replacementSeparator + "(.|?" + replacementSeparator + ")?" )
      .Replace("**" + seperator, ".|(?<=^|" + replacementSeparator + ")" )
"""
|> formatSrc

// le nojaf
let parseAndPrint input = 
    CodeFormatter.Parse ("Program.fs", input)   
    |> printfn "%A"