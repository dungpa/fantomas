module Fantomas.Tests.TupleTests

open NUnit.Framework
open FsUnit

open Fantomas.Tests.TestHelper

[<Test>]
let ``tuple with lamba should add parenthesis`` () =
    formatSourceString false """
let private carouselSample =
    FunctionComponent.Of<obj>(fun _ ->
        fragment [] []
    ,"CarouselSample")
"""
        ({ config with
               MaxValueBindingWidth = 75 })
    |> should equal """let private carouselSample = FunctionComponent.Of<obj>((fun _ -> fragment [] []), "CarouselSample")
"""

[<Test>]
let ``multiline item in tuple - paren on its line`` () =
    formatSourceString false """(x,
 if longExpressionMakingTheIfElseMultiline && a then answerWhenTheConditionIsTrue
 else answerWhenTheConditionIsFalse)
"""  config
    |> should equal """(x,
 (if longExpressionMakingTheIfElseMultiline && a
  then answerWhenTheConditionIsTrue
  else answerWhenTheConditionIsFalse))
"""

[<Test>]
let ``multiline SynPat.Tuple should have parenthesis, 824`` () =
    formatSourceString false """
namespace GWallet.Backend.Tests

module Foo =

    let someOtherFunc () =
        let var1withAVeryLongLongLongLongLongLongName, var2withAVeryLongLongLongLongLongLongName =
            someFunc 1, someFunc 2

        ()
"""  { config with MaxLineLength = 80 }
    |> prepend newline
    |> should equal """
namespace GWallet.Backend.Tests

module Foo =

    let someOtherFunc () =
        let (var1withAVeryLongLongLongLongLongLongName,
             var2withAVeryLongLongLongLongLongLongName) =
            someFunc 1, someFunc 2

        ()
"""

[<Test>]
let ``multiline SynPat.Tuple with existing parenthesis should not add additional parenthesis`` () =
    formatSourceString false """
namespace GWallet.Backend.Tests

module Foo =

    let someOtherFunc () =
        let (var1withAVeryLongLongLongLongLongLongName,
             var2withAVeryLongLongLongLongLongLongName) =
            someFunc 1, someFunc 2

        ()
"""  { config with MaxLineLength = 80 }
    |> prepend newline
    |> should equal """
namespace GWallet.Backend.Tests

module Foo =

    let someOtherFunc () =
        let (var1withAVeryLongLongLongLongLongLongName,
             var2withAVeryLongLongLongLongLongLongName) =
            someFunc 1, someFunc 2

        ()
"""
