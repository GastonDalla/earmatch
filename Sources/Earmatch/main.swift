import AppKit
import EarmatchCore

AppLanguage.install(CommandLine.arguments.contains(WindowRenderRequest.flag) ? .system : AppLanguage.storedChoice)

if CommandLine.arguments.contains("--dump-ax") {
    exit(DiagnosticsCommand.dumpAccessibility())
}

do {
    if let request = try WindowRenderRequest(arguments: CommandLine.arguments) {
        exit(WindowRenderCommand.run(request))
    }
} catch {
    FileHandle.standardError.write(Data((error.message + "\n").utf8))
    exit(64)
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory)
app.run()
