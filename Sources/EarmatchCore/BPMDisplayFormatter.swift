import Foundation

public enum BPMDisplayFormatter {
    public static func parse(_ raw: String) -> Double? {
        let trimmed = raw.trimmingCharacters(in: .whitespaces)
        guard trimmed.range(of: #"^\d{1,3}(\.\d{1,2})?$"#, options: .regularExpression) != nil else { return nil }
        return Double(trimmed)
    }
}
