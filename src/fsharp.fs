[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Ionide.VSCode.FSharp

open System
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Fable.PowerPack
open Fable.Import.vscode
open Ionide.VSCode.Helpers
open Ionide.VSCode.FSharp

let private logger = consoleOnlyLogger "IONIDE-ACTIVATE"

let activate (context: ExtensionContext) =
    logger.Info "Activating"

    let df = createEmpty<DocumentFilter>
    df.language <- Some "fsharp"
    let df' : DocumentSelector = df |> U3.Case2

    let legacyFsi = "FSharp.legacyFSI" |> Configuration.get false
    let resolve = "FSharp.resolveNamespaces" |> Configuration.get false
    let solutionExplorer = "FSharp.enableTreeView" |> Configuration.get true
    let codeOutline = "FSharp.codeOutline" |> Configuration.get true

    let init = DateTime.Now

    Project.clearCacheIfOutdated ()

    promise {
        do! LanguageService.start () |> Promise.ignore

        let progressOpts = createEmpty<ProgressOptions>
        progressOpts.location <- ProgressLocation.Window

        promise {
            try
                printfn "DOING"
                do! window.withProgress(progressOpts, (fun p -> promise {
                    printfn "PROGRESS"
                    let pm = createEmpty<ProgressMessage>
                    pm.message <- "Loading current project"
                    p.report pm
                    LineLens.activate context
                    let parseVisibleTextEditors = Errors.activate context
                    pm.message <- "Loading all projects"
                    p.report pm
                    printfn "BEFORE CODELENS"
                    CodeLens.activate df' context
                    printfn "AFTER CODELENS"

                    printfn "BEFORE LINTER"
                    Linter.activate df' context
                    printfn "AFTER LINTER"
                    (*
                    if codeOutline then CodeOutline.activate context
                    if solutionExplorer then SolutionExplorer.activate context

                    do! Project.activate context parseVisibleTextEditors*)
                }))

                let e = DateTime.Now - init
                logger.Info(sprintf "Startup took: %f ms" e.TotalMilliseconds)
            with
            | ex -> logger.Error("BOOM %O", ex)
        }
        |> Promise.start

        Tooltip.activate df' context
        Autocomplete.activate df' context
        ParameterHints.activate df' context
        Definition.activate df' context
        Reference.activate df' context
        Symbols.activate df' context
        Highlights.activate df' context
        Rename.activate df' context
        WorkspaceSymbols.activate df' context
        QuickInfo.activate context
        QuickFix.activate df' context
        if resolve then ResolveNamespaces.activate df' context
        UnionCaseGenerator.activate df' context
        Help.activate context
        Expecto.activate context
        MSBuild.activate context
        SignatureData.activate context
        Debugger.activate context
    }
    |> logger.ErrorOnFailed "Subsystems init failed"

    Forge.activate context
    if legacyFsi then LegacyFsi.activate context else Fsi.activate context

    logger.Info "Activated"
    ()

let deactivate(disposables: Disposable[]) =
    LanguageService.stop ()
    ()
