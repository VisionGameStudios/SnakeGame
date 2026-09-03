import Darwin
import Foundation

func fail(_ message: String) -> Never {
    fputs(message + "\n", stderr)
    exit(1)
}

func run(_ executable: String, _ arguments: [String]) {
    let process = Process()
    process.executableURL = URL(fileURLWithPath: executable)
    process.arguments = arguments
    do {
        try process.run()
        process.waitUntilExit()
    } catch {
        fail("No se pudo ejecutar \(executable): \(error)")
    }
    if process.terminationStatus != 0 {
        fail("\(executable) terminó con código \(process.terminationStatus)")
    }
}

guard CommandLine.arguments.count == 4 else {
    fail("Uso: SnakeUpdater <zip> <app instalada> <pid>")
}

let archivePath = CommandLine.arguments[1]
let installedApp = CommandLine.arguments[2]
guard let gamePid = Int32(CommandLine.arguments[3]) else {
    fail("PID inválido")
}

let fileManager = FileManager.default
let workDirectory = fileManager.temporaryDirectory.appendingPathComponent("SnakeUpdate-\(UUID().uuidString)")
let extractedDirectory = workDirectory.appendingPathComponent("extracted")
let backupApp = installedApp + ".backup"

do {
    try fileManager.createDirectory(at: extractedDirectory, withIntermediateDirectories: true)
} catch {
    fail("No se pudo crear el directorio temporal: \(error)")
}

while kill(gamePid, 0) == 0 {
    sleep(1)
}

run("/usr/bin/ditto", ["-x", "-k", archivePath, extractedDirectory.path])
let newApp = extractedDirectory.appendingPathComponent("Snake.app").path
guard fileManager.fileExists(atPath: newApp) else {
    fail("El ZIP no contiene Snake.app")
}

do {
    if fileManager.fileExists(atPath: backupApp) {
        try fileManager.removeItem(atPath: backupApp)
    }
    try fileManager.moveItem(atPath: installedApp, toPath: backupApp)
    try fileManager.moveItem(atPath: newApp, toPath: installedApp)
} catch {
    if fileManager.fileExists(atPath: installedApp) == false && fileManager.fileExists(atPath: backupApp) {
        try? fileManager.moveItem(atPath: backupApp, toPath: installedApp)
    }
    fail("No se pudo instalar la actualización: \(error)")
}

run("/usr/bin/open", [installedApp])
try? fileManager.removeItem(at: workDirectory)