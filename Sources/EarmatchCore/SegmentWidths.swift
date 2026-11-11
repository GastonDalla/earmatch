import Foundation

public enum SegmentWidths {
    public static func distribute(ideal: [Double], available: Double) -> [Double] {
        guard !ideal.isEmpty else { return [] }
        let available = max(available, 0)
        let total = ideal.reduce(0, +)
        if total > available {
            guard total > 0 else { return ideal.map { _ in available / Double(ideal.count) } }
            return ideal.map { $0 * available / total }
        }
        var remaining = available
        var keptCount = 0
        for width in ideal.sorted(by: >) {
            let share = remaining / Double(ideal.count - keptCount)
            guard width > share else { break }
            remaining -= width
            keptCount += 1
        }
        let share = keptCount == ideal.count ? 0 : remaining / Double(ideal.count - keptCount)
        return ideal.map { max($0, share) }
    }
}
