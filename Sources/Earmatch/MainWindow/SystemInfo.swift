import Foundation
import EarmatchCore

enum SystemInfo {
    static var shortVersion: String? { Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String }

    static var appVersion: String {
        guard let shortVersion else { return L10n.text("development build") }
        let build = Bundle.main.infoDictionary?["CFBundleVersion"] as? String
        return build.map { "\(shortVersion) (\($0))" } ?? shortVersion
    }

    static var macOSVersion: String { ProcessInfo.processInfo.operatingSystemVersionString }

    static var hardwareModel: String { sysctlString("hw.model") ?? L10n.text("unknown Mac") }

    static var architecture: String {
        #if arch(arm64)
        return "arm64"
        #else
        return sysctlInt("sysctl.proc_translated") == 1 ? L10n.text("x86_64 under Rosetta") : "x86_64"
        #endif
    }

    private static func sysctlString(_ name: String) -> String? {
        var size = 0
        guard sysctlbyname(name, nil, &size, nil, 0) == 0, size > 0 else { return nil }
        var buffer = [CChar](repeating: 0, count: size)
        guard sysctlbyname(name, &buffer, &size, nil, 0) == 0 else { return nil }
        return String(decoding: buffer.prefix { $0 != 0 }.map { UInt8(bitPattern: $0) }, as: UTF8.self)
    }

    private static func sysctlInt(_ name: String) -> Int32? {
        var value: Int32 = 0
        var size = MemoryLayout<Int32>.size
        guard sysctlbyname(name, &value, &size, nil, 0) == 0 else { return nil }
        return value
    }
}
