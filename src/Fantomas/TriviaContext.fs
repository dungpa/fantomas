module internal Fantomas.TriviaContext

open FSharp.Compiler.Ast
open FSharp.Compiler.Range
open Fantomas
open Fantomas.Context
open Fantomas.TriviaTypes

let tok (range: range) (s: string) =
    enterNodeToken range +> (!-s) +> leaveNodeToken range

let tokN (range: range) (tokenName: string) f =
    enterNodeTokenByName range tokenName +> f +> leaveNodeTokenByName range tokenName

let firstNewlineOrComment (es: SynExpr list) (ctx: Context) =
    es
    |> List.tryHead
    |> Option.bind (fun e -> TriviaHelpers.findByRange ctx.Trivia e.Range)
    |> fun cb ->
        match cb with
        | Some ({ ContentBefore = (TriviaContent.Newline|TriviaContent.Comment _) as head ::rest } as tn) ->
            let updatedTriviaNodes =
                ctx.Trivia
                |> List.map (fun t ->
                    if t = tn then
                        { tn with ContentBefore = rest }
                    else t
                )

            let ctx' = { ctx with Trivia = updatedTriviaNodes }
            printTriviaContent head ctx'
        | _ -> sepNone ctx

let triviaAfterArrow (range: range) (ctx: Context) =
    let hasCommentAfterArrow =
        findTriviaTokenFromName range ctx.Trivia "RARROW"
        |> Option.bind (fun t ->
            t.ContentAfter
            |> List.tryFind (function | Comment(LineCommentAfterSourceCode(_)) -> true | _ -> false)
        )
        |> Option.isSome
    ((tokN range "RARROW" sepArrow) +> ifElse hasCommentAfterArrow sepNln sepNone) ctx

let ``else if / elif`` (rangeOfIfThenElse: range) (ctx: Context) =
    let keywords =
        ctx.Trivia
        |> TriviaHelpers.``keyword tokens inside range`` ["ELSE";"IF";"ELIF"] rangeOfIfThenElse
        |> List.map (fun (tok, t) -> (tok.TokenName, t))

    let resultExpr =
        match keywords with
        | ("ELSE", elseTrivia)::("IF", ifTrivia)::_ ->
            let commentAfterElseKeyword = TriviaHelpers.``has line comment after`` elseTrivia
            let commentAfterIfKeyword = TriviaHelpers.``has line comment after`` ifTrivia

            tokN rangeOfIfThenElse "ELSE" (!- "else") +>
            ifElse commentAfterElseKeyword sepNln sepSpace +>
            tokN rangeOfIfThenElse "IF" (!- "if ") +>
            ifElse commentAfterIfKeyword (indent +> sepNln) sepNone

        | ("ELIF",_)::_
        | [("ELIF",_)] ->
            tokN rangeOfIfThenElse "ELIF" (!- "elif ")

    resultExpr ctx