namespace Ionide.VSCode.FSharp

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.PowerPack
open Fable.Import
open Fable.Import.vscode
open Fable.Import.Node

open DTO
open Ionide.VSCode.Helpers

module Fsi =
    let mutable fsiOutput : Terminal option = None
    let mutable fsiOutputPID : int option = None
    let mutable lastSelectionSent : string option = None

    let isPowershell () =
        let t = "terminal.integrated.shell.windows" |> Configuration.get ""
        t.ToLower().Contains "powershell"

    let sendCd () =
        let editor = window.activeTextEditor
        let file,dir =
            if  JS.isDefined editor then
                let file = editor.document.fileName
                let dir = Path.dirname file
                file,dir
            else
                let dir = workspace.rootPath
                Path.join(dir, "tmp.fsx"), dir
        let msg1 = sprintf "# silentCd @\"%s\";;\n" dir
        let msg2 = (sprintf "# %d @\"%s\" \n" 1 file)


        fsiOutput |> Option.iter (fun n ->
            n.sendText(msg1, false)
            n.sendText(msg2, false)
            n.sendText(";;\n", false)
        )

    let private start () =
        promise {
            fsiOutput |> Option.iter (fun n -> n.dispose())
            let parms =
                let fsiParams =
                    "FSharp.fsiExtraParameters"
                    |> Configuration.get Array.empty<string>
                    |> List.ofArray

                if Environment.isWin then
                    [ "--fsi-server-input-codepage:65001" ] @ fsiParams
                else
                    fsiParams
                |> Array.ofList

            let terminal = window.createTerminal("F# Interactive", Environment.fsi, parms)
            promise {
                let! pId = terminal.processId
                fsiOutputPID <- Some pId
            } |> ignore

            fsiOutput <- Some terminal
            sendCd ()
            terminal.show(true)
            return terminal
            }
        |> Promise.onFail (fun _ ->
            window.showErrorMessage "Failed to spawn FSI, please ensure it's in PATH" |> ignore)

    let private chunkStringBySize (size : int) (str : string) =
        let mutable i1 = 0
        [while i1 < str.Length do
            let i2 = min (i1 + size) (str.Length)
            yield str.[i1..i2-1]
            i1 <- i2]

    let private send (msg : string) =
        let msgWithNewline = msg + "\n;;\n"
        promise {
            try
                let! fp =
                    match fsiOutput with
                    | None -> start ()
                    | Some fo -> Promise.lift fo

                fp.show true

                //send in chunks of 256, terminal has a character limit
                msgWithNewline
                |> chunkStringBySize 256
                |> List.iter (fun x -> fp.sendText(x,false))

                lastSelectionSent <- Some msg
            with
            | error -> window.showErrorMessage "Failed to send text to FSI" |> ignore
        }

    let private sendLine () =
        let editor = window.activeTextEditor
        let file = editor.document.fileName
        let pos = editor.selection.start
        let line = editor.document.lineAt pos
        promise {
            let! _ = send line.text
            commands.executeCommand "cursorDown" |> ignore}
        |> Promise.suppress // prevent unhandled promise exception

    let private sendSelection () =
        let editor = window.activeTextEditor
        let file = editor.document.fileName

        if editor.selection.isEmpty then
            sendLine ()
        else
            let range = Range(editor.selection.anchor.line, editor.selection.anchor.character, editor.selection.active.line, editor.selection.active.character)
            let text = editor.document.getText range
            send text
            |> Promise.suppress // prevent unhandled promise exception

    let private sendLastSelection () =
        match lastSelectionSent with
        | Some x ->
            promise {
                if "FSharp.saveOnSendLastSelection" |> Configuration.get false then
                    do! window.activeTextEditor.document.save () |> Promise.ignore
                do! send x
            }
            |> Promise.suppress // prevent unhandled promise exception
        | None -> ()

    let private sendFile () =
        let editor = window.activeTextEditor
        let text = editor.document.getText ()
        send text
        |> Promise.suppress // prevent unhandled promise exception

    let private referenceAssembly (path:ProjectReferencePath) = path |> sprintf "#r @\"%s\"" |> send
    let private referenceAssemblies = Promise.executeForAll referenceAssembly

    let private sendReferences () =
        window.activeTextEditor.document.fileName
        |> Project.tryFindLoadedProjectByFile
        |> Option.iter (fun p -> p.References  |> List.filter (fun n -> n.EndsWith "FSharp.Core.dll" |> not && n.EndsWith "mscorlib.dll" |> not )  |> referenceAssemblies |> Promise.suppress |> ignore)

    let private handleCloseTerminal (terminal:Terminal) =
        fsiOutputPID
        |> Option.iter (fun currentTerminalPID ->
            promise {
                let! closedTerminalPID = terminal.processId
                if closedTerminalPID = currentTerminalPID then
                    fsiOutput <- None
                    fsiOutputPID <- None }
            |> Promise.suppress) // prevent unhandled promise exception
        |> ignore

    let private generateProjectReferences () =
        let ctn =
            window.activeTextEditor.document.fileName
            |> Project.tryFindLoadedProjectByFile
            |> Option.map (fun p ->
                [|
                    yield! p.References |> List.filter (fun n -> n.EndsWith "FSharp.Core.dll" |> not && n.EndsWith "mscorlib.dll" |> not ) |> List.map (sprintf "#r @\"%s\"")
                    yield! p.Files |> List.map (sprintf "#load @\"%s\"")
                |])
        promise {
            match ctn with
            | Some c ->
                let path = Path.join(workspace.rootPath, "references.fsx")
                let! td = vscode.Uri.parse ("untitled:" + path) |> workspace.openTextDocument
                let! te = window.showTextDocument(td, ViewColumn.Three)
                let! res = te.edit (fun e ->
                    let p = Position(0.,0.)
                    let ctn = c |> String.concat "\n"
                    e.insert(p,ctn))


                return ()
            | None ->
                return ()
        }




    let activate (context: ExtensionContext) =
        window.onDidChangeActiveTextEditor $ ((fun n -> if JS.isDefined n then sendCd()), (), context.subscriptions) |> ignore
        window.onDidCloseTerminal $ (handleCloseTerminal, (), context.subscriptions) |> ignore

        commands.registerCommand("fsi.Start", start |> unbox<Func<obj,obj>>) |> context.subscriptions.Add
        commands.registerCommand("fsi.SendLine", sendLine |> unbox<Func<obj,obj>>) |> context.subscriptions.Add
        commands.registerCommand("fsi.SendSelection", sendSelection |> unbox<Func<obj,obj>>) |> context.subscriptions.Add
        commands.registerCommand("fsi.SendLastSelection", sendLastSelection |> unbox<Func<obj,obj>>) |> context.subscriptions.Add
        commands.registerCommand("fsi.SendFile", sendFile |> unbox<Func<obj,obj>>) |> context.subscriptions.Add
        commands.registerCommand("fsi.SendProjectReferences", sendReferences |> unbox<Func<obj,obj>>) |> context.subscriptions.Add
        commands.registerCommand("fsi.GenerateProjectReferences", generateProjectReferences |> unbox<Func<obj,obj>>) |> context.subscriptions.Add

