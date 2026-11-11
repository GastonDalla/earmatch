import Foundation

public struct WindowRenderRequest: Equatable, Sendable {
    public enum Problem: Error, Equatable, Sendable {
        case missingDirectory
        case invalidSize(String)
        case belowMinimum(String)

        public var message: String {
            let reason = switch self {
            case .missingDirectory: "The output directory is missing."
            case let .invalidSize(size): "The size \"\(size)\" is not default, minimum or WIDTHxHEIGHT."
            case let .belowMinimum(size): "The size \(size) is smaller than the window's minimum size."
            }
            return "\(reason)\n\(Self.usage)"
        }

        static let usage = "Usage: Earmatch --render-window <directory> [--render-size default|minimum|WIDTHxHEIGHT]"
    }

    public static let flag = "--render-window"
    public static let sizeFlag = "--render-size"

    public var directory: URL
    public var size: CGSize

    public init(directory: URL, size: CGSize) {
        self.directory = directory
        self.size = size
    }

    public init?(arguments: [String]) throws(Problem) {
        guard let flagIndex = arguments.firstIndex(of: Self.flag) else { return nil }
        let pathIndex = flagIndex + 1
        guard arguments.indices.contains(pathIndex), !arguments[pathIndex].hasPrefix("--") else { throw .missingDirectory }
        directory = URL(fileURLWithPath: arguments[pathIndex], isDirectory: true)
        size = MainWindowMetrics.defaultSize
        if let sizeIndex = arguments.firstIndex(of: Self.sizeFlag) {
            let value = arguments.indices.contains(sizeIndex + 1) ? arguments[sizeIndex + 1] : ""
            size = try Self.size(from: value)
        }
    }

    private static func size(from value: String) throws(Problem) -> CGSize {
        switch value {
        case "default": return MainWindowMetrics.defaultSize
        case "minimum": return MainWindowMetrics.minimumSize
        default: break
        }
        let parts = value.split(separator: "x", omittingEmptySubsequences: false)
        guard parts.count == 2, let width = Int(parts[0]), let height = Int(parts[1]), width > 0, height > 0 else {
            throw .invalidSize(value)
        }
        let size = CGSize(width: width, height: height)
        guard size.width >= MainWindowMetrics.minimumSize.width, size.height >= MainWindowMetrics.minimumSize.height else {
            throw .belowMinimum(value)
        }
        return size
    }
}
