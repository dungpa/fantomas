﻿namespace Fantomas

open Fantomas
open FormatConfig

[<Sealed>]
type CodeFormatter =
    static member ParseAsync(fileName, source, parsingOptions, checker) =
        CodeFormatterImpl.createFormatContext fileName source
        |> CodeFormatterImpl.parse checker parsingOptions

    static member FormatASTAsync(ast, fileName, defines, source, config) =
        let formatContext = CodeFormatterImpl.createFormatContext fileName (Option.defaultValue (SourceOrigin.SourceString "") source)
        CodeFormatterImpl.formatAST ast defines formatContext config
        |> async.Return

    static member FormatDocumentAsync(fileName, source, config, parsingOptions, checker) =
        CodeFormatterImpl.createFormatContext fileName source
        |> CodeFormatterImpl.formatDocument checker parsingOptions config

    static member FormatSelectionAsync(fileName, selection, source, config, parsingOptions, checker) =
        CodeFormatterImpl.createFormatContext fileName source
        |> CodeFormatterImpl.formatSelection checker parsingOptions selection config

    static member IsValidFSharpCodeAsync(fileName, source, parsingOptions, checker) =
        CodeFormatterImpl.createFormatContext fileName source
        |> CodeFormatterImpl.isValidFSharpCode checker parsingOptions

    static member IsValidASTAsync ast = 
        async { return CodeFormatterImpl.isValidAST ast }

    static member MakePos(line, col) = 
        CodeFormatterImpl.makePos line col

    static member MakeRange(fileName, startLine, startCol, endLine, endCol) = 
        CodeFormatterImpl.makeRange fileName startLine startCol endLine endCol

    static member GetVersion() = Fantomas.Version.fantomasVersion.Value

    static member ReadConfiguration(fileOrFolder) =
        try
            let configurationFiles =
                ConfigFile.findConfigurationFiles fileOrFolder

            if List.isEmpty configurationFiles then failwithf "No configuration files were found for %s" fileOrFolder

            let (config,warnings) =
                List.fold (fun (currentConfig, warnings) configPath ->
                    let updatedConfig, warningsForPath = ConfigFile.applyOptionsToConfig currentConfig configPath
                    (updatedConfig, warnings @ warningsForPath)
                ) (FormatConfig.Default, []) configurationFiles

            match warnings with
            | [] -> FormatConfigFileParseResult.Success config
            | w -> FormatConfigFileParseResult.PartialSuccess (config, w)
        with
        | exn -> FormatConfigFileParseResult.Failure exn