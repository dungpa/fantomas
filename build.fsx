#r "paket:
nuget Fake.BuildServer.AppVeyor
nuget Fake.Core.ReleaseNotes
nuget Fake.Core.Xml
nuget Fake.DotNet.Cli
nuget Fake.DotNet.Paket
nuget Fake.Tools.Git
nuget Fake.Core.Process
nuget Fake.Core.Target //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
  #r "netstandard"
#endif

open Fake.Core
open Fake.IO
open Fake.Core.TargetOperators
open Fake.BuildServer
open System
open Fake.DotNet

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitHome = "https://github.com/fsprojects"
// The name of the project on GitHub
let gitName = "fantomas"

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "Fantomas"

let projectUrl = sprintf "%s/%s" gitHome gitName

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Source code formatter for F#"

let copyright = "Copyright \169 2018"
let iconUrl = "https://raw.githubusercontent.com/fsprojects/fantomas/master/fantomas_logo.png"
let licenceUrl = "https://github.com/fsprojects/fantomas/blob/master/LICENSE.md"
let configuration = DotNet.BuildConfiguration.Release

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = """This library aims at formatting F# source files based on a given configuration.
Fantomas will ensure correct indentation and consistent spacing between elements in the source files.
Some common use cases include
(1) Reformatting a code base to conform a universal page width
(2) Converting legacy code from verbose syntax to light syntax
(3) Formatting auto-generated F# signatures."""

// List of author names (for NuGet package)
let authors = [ "Anh-Dung Phan"; "Gustavo Guerra" ]
let owner = "Anh-Dung Phan"
// Tags for your project (for NuGet package)
let tags = "F# fsharp formatting beautifier indentation indenter"

// (<solutionFile>.sln is built during the building process)
let solutionFile  = "fantomas"
//// Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let release = Fake.Core.ReleaseNotes.parse (IO.File.ReadAllLines "RELEASE_NOTES.md")

// Types and helper functions for building external projects (see the TestExternalProjects target below)
type ProcessStartInfo =
    { ProcessName : string
      Arguments : string list }

[<NoComparison>]
[<NoEquality>]
type ExternalProjectInfo =
    { GitUrl : string
      DirectoryName : string
      Tag : string
      SourceSubDirectory : string
      BuildConfigurationFn : (string -> ProcessStartInfo) }

// Construct the commands/arguments for running an external project build script for both windows and linux
// For linux we run this by invoking sh explicitly and passing the build.sh script as an argument as some
// projects generated on windows don't have the executable permission set for .sh scripts. On windows we
// treat .cmd files as executable
let configureBuildCommandFromDefaultFakeBuildScripts pathToProject =
    if Environment.isWindows
    then { ProcessName = Path.combine pathToProject "build.cmd"; Arguments = [ "Build" ] }
    else { ProcessName = "sh"; Arguments = [ sprintf "%s/build.sh Build" pathToProject ] }

let configureBuildCommandDotnetBuild pathToProject =
    { ProcessName = "dotnet"; Arguments = [ "build"; pathToProject ] }

// Construct the path of the fantomas executable to use for external project tests
let fantomasExecutableForExternalTests projectdir =
    let configuration =
        match configuration with
        | DotNet.BuildConfiguration.Debug -> "debug"
        | DotNet.BuildConfiguration.Release -> "release"
        | DotNet.BuildConfiguration.Custom s -> s
    
    if Environment.isWindows
    then { ProcessName = sprintf "%s/src/Fantomas.Cmd/bin/%s/net452/dotnet-fantomas.exe" projectdir configuration; Arguments = [] }
    else { ProcessName = "dotnet"; Arguments = [ sprintf "%s/src/Fantomas.CoreGlobalTool/bin/%s/netcoreapp2.1/fantomas-tool.dll" projectdir configuration ] }

let externalProjectsToTest = [
    { GitUrl = @"https://github.com/fsprojects/Argu"
      DirectoryName = "Argu"
      Tag = "5.1.0"
      SourceSubDirectory = "src"
      BuildConfigurationFn = configureBuildCommandFromDefaultFakeBuildScripts }
    ]

let externalProjectsToTestFailing = [
    { GitUrl = @"https://github.com/fsprojects/Chessie"
      DirectoryName = "Chessie"
      Tag = "master"
      SourceSubDirectory = "src"
      BuildConfigurationFn = configureBuildCommandFromDefaultFakeBuildScripts }
    { GitUrl = @"https://github.com/fscheck/FsCheck"
      DirectoryName = "FsCheck"
      Tag = "master"
      SourceSubDirectory = "src"
      BuildConfigurationFn = configureBuildCommandFromDefaultFakeBuildScripts }
    { GitUrl = @"https://github.com/fsprojects/fantomas"
      DirectoryName = "Fantomas"
      Tag = "v2.9.0"
      SourceSubDirectory = "src"
      BuildConfigurationFn = configureBuildCommandFromDefaultFakeBuildScripts }
    { GitUrl = @"https://github.com/jack-pappas/ExtCore"
      DirectoryName = "ExtCore"
      Tag = "master"
      SourceSubDirectory = "."
      BuildConfigurationFn = configureBuildCommandDotnetBuild }
    { GitUrl = @"https://github.com/SAFE-Stack/SAFE-BookStore"
      DirectoryName = "SAFE-BookStore"
      Tag = "master"
      SourceSubDirectory = "src"
      BuildConfigurationFn = configureBuildCommandFromDefaultFakeBuildScripts }
    { GitUrl = @"https://github.com/fsprojects/Paket"
      DirectoryName = "Paket"
      Tag = "5.181.1"
      SourceSubDirectory = "src"
      BuildConfigurationFn = configureBuildCommandFromDefaultFakeBuildScripts }
    { GitUrl = @"https://github.com/fsprojects/FSharpPlus"
      DirectoryName = "FSharpPlus"
      Tag = "v1.0.0"
      SourceSubDirectory = "src"
      BuildConfigurationFn = configureBuildCommandFromDefaultFakeBuildScripts }
    ]

// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages

Target.create "Clean" (fun _ ->
    [ "bin"
      "src/Fantomas/bin"
      "src/Fantomas/obj"
      "src/Fantomas.Cmd/bin"
      "src/Fantomas.Cmd/obj"
      "src/Fantomas.CoreGlobalTool/bin"
      "src/Fantomas.CoreGlobalTool/obj" ]
    |> List.iter Shell.cleanDir
)

let isAppVeyor = AppVeyor.detect()

Target.create "ProjectVersion" (fun _ ->
    let version =
        if isAppVeyor then
            sprintf "%s.%s" release.NugetVersion BuildServer.appVeyorBuildVersion
        else
            release.NugetVersion

    let setProjectVersion project =
        let file = sprintf "src/%s/%s.fsproj" project project 
        Xml.poke file "Project/PropertyGroup/Version/text()" version
    
    setProjectVersion "Fantomas"
    setProjectVersion "Fantomas.Cmd"
    setProjectVersion "Fantomas.CoreGlobalTool"
    setProjectVersion "Fantomas.Tests"
)

// --------------------------------------------------------------------------------------
// Build library & test project
Target.create "Build" (fun _ ->
    let sln = sprintf "%s.sln" solutionFile
    DotNet.build (fun p -> { p with Configuration = configuration }) sln
)

Target.create "UnitTests" (fun _ ->
    DotNet.test (fun p ->
        { p with
            Configuration = configuration
            NoRestore = true
            NoBuild = true
            TestAdapterPath = Some "."
            Logger = Some "nunit;LogFilePath=../../TestResults.xml"
            // AdditionalArgs = ["--no-build --no-restore --test-adapter-path:. --logger:nunit;LogFilePath=../../TestResults.xml"]
        }
    ) "src/Fantomas.Tests/Fantomas.Tests.fsproj"
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "Pack" (fun _ ->
    let nugetVersion =
        if isAppVeyor then
            sprintf "%s-latest" release.NugetVersion
        else
            release.NugetVersion

    let pack project =
        let projectPath = sprintf "src/%s/%s.fsproj" project project
        let args =
            let defaultArgs = MSBuild.CliArguments.Create()
            { defaultArgs with
                      Properties = [
                          "Title", project
                          "PackageVersion", nugetVersion
                          "Authors", (String.Join(" ", authors))
                          "Owners", owner
                          "PackageRequireLicenseAcceptance", "false"
                          "Description", description
                          "Summary", summary
                          "PackageReleaseNotes", ((String.toLines release.Notes).Replace(",",""))
                          "Copyright", copyright
                          "PackageTags", tags
                          "PackageProjectUrl", projectUrl
                          "PackageIconUrl", iconUrl
                          "PackageLicenseUrl", licenceUrl
                      ] }
        
        DotNet.pack (fun p ->
            { p with
                  NoBuild = true
                  Configuration = configuration
                  OutputPath = Some "../../bin"
                  MSBuildParams = args
              }) projectPath 

    pack "Fantomas"
    pack "Fantomas.Cmd"
    pack "Fantomas.CoreGlobalTool"
)

// This takes the list of external projects defined above, does a git checkout of the specified repo and tag,
// tries to build the project, then reformats with fantomas and tries to build the project again. If this fails
// then there was a regression in fantomas that mangles the source code
let testExternalProjects externalProjectsToTest =
    let externalBuildErrors =
        let project = Environment.environVar "project"
        externalProjectsToTest
        |> if project="" then id else List.filter (fun p -> p.DirectoryName = project)
        |> List.map (fun project ->
            let relativeProjectDir = sprintf "external-project-tests/%s" project.DirectoryName

            Shell.cleanDir relativeProjectDir
            // Use "shallow" clone by setting depth to 1 to only check out the one commit we want to build
            Fake.Tools.Git.CommandHelper.gitCommand "." (sprintf "clone --branch %s --depth 1 %s %s" project.Tag project.GitUrl relativeProjectDir)

            let fullProjectPath = sprintf "%s/%s" __SOURCE_DIRECTORY__ relativeProjectDir
            let buildStartInfo = project.BuildConfigurationFn fullProjectPath

            let buildExternalProject() =
                buildStartInfo.Arguments
                |> CreateProcess.fromRawCommand buildStartInfo.ProcessName
                |> CreateProcess.withWorkingDirectory relativeProjectDir
                |> CreateProcess.withTimeout (TimeSpan.FromMinutes 5.0)
                |> Proc.run

            let cleanResult = buildExternalProject()
            if cleanResult.ExitCode <> 0 then failwithf "Initial build of external project %s returned with a non-zero exit code" project.DirectoryName

            let fantomasStartInfo =
                fantomasExecutableForExternalTests __SOURCE_DIRECTORY__
            let arguments =
                fantomasStartInfo.Arguments @ [ sprintf "--recurse %s" project.SourceSubDirectory ]
                
            let invokeFantomas() =
                CreateProcess.fromRawCommand fantomasStartInfo.ProcessName arguments
                |> CreateProcess.withWorkingDirectory (sprintf "%s/%s" __SOURCE_DIRECTORY__ relativeProjectDir)
                |> CreateProcess.withTimeout (TimeSpan.FromMinutes 5.0)
                |> Proc.run

            let fantomasResult = invokeFantomas()

            if fantomasResult.ExitCode <> 0
            then Some <| sprintf "Fantomas invokation for %s returned with a non-zero exit code" project.DirectoryName
            else
                let formattedResult = buildExternalProject()
                if formattedResult.ExitCode <> 0
                then Some <| sprintf "Build of external project after fantomas formatting failed for project %s" project.DirectoryName
                else
                    printfn "Successfully built %s after reformatting" project.DirectoryName
                    None
        )
        |> List.choose id
    if not (List.isEmpty externalBuildErrors)
    then failwith (String.Join("\n", externalBuildErrors) )


Target.create "TestExternalProjects" (fun _ -> testExternalProjects externalProjectsToTest)
Target.create "TestExternalProjectsFailing" (fun _ -> testExternalProjects externalProjectsToTestFailing)

Target.create "Push" (fun _ -> Paket.push (fun p -> { p with WorkingDir = "bin" }))

Target.create "MyGet" (fun _ ->
    let prNumber = Environment.environVar "APPVEYOR_PULL_REQUEST_NUMBER"
    let isPullRequest = not (String.IsNullOrEmpty prNumber)
        
    if not isPullRequest then
        Paket.push (fun p ->
            { p with
                WorkingDir = "bin"
                PublishUrl = "https://www.myget.org/F/fantomas/api/v2/package"
                ApiKey = Environment.environVar "myget-key" }
        )
    else
        printfn "Not pushing pull request %s to myget" prNumber
)

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore

"Clean"
    ==> "ProjectVersion"
    ==> "Build"
    ==> "UnitTests"
    ==> "Pack"
    ==> "All"
    ==> "Push"

"Build"
    ==> "TestExternalProjects"

"Pack"
    ==> "MyGet"

Target.runOrDefault "All"