import Foundation
import Testing

@Suite struct LocalizationCatalogTests {
    static let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
    static let languages = ["en", "es"]
    static let untranslated: Set<String> = ["Earmatch", "⌃⌥⌘E"]

    enum Kind: String {
        case text, template, format, plural
    }

    struct Use: Hashable {
        let kind: Kind
        let key: String
        let place: String
    }

    static func swiftFiles(in directory: String) -> [(place: String, lines: [String])] {
        let base = root.appending(path: directory)
        let paths = FileManager.default.enumerator(atPath: base.path(percentEncoded: false))?.compactMap { $0 as? String } ?? []
        return paths.filter { $0.hasSuffix(".swift") }.sorted().compactMap { path in
            guard let text = try? String(contentsOf: base.appending(path: path), encoding: .utf8) else { return nil }
            return ("\(directory)/\(path)", text.components(separatedBy: "\n"))
        }
    }

    static let call = try! NSRegularExpression(pattern: #"L10n\.(text|template|format|plural)\(\s*("(?:[^"\\]|\\.)*")?"#)

    static let scan: (uses: [Use], problems: [String]) = {
        var uses: [Use] = []
        var problems: [String] = []
        for file in swiftFiles(in: "Sources") {
            for (index, line) in file.lines.enumerated() {
                let place = "\(file.place):\(index + 1)"
                let range = NSRange(line.startIndex..., in: line)
                for match in call.matches(in: line, range: range) {
                    let kind = Kind(rawValue: String(line[Range(match.range(at: 1), in: line)!]))!
                    guard let literalRange = Range(match.range(at: 2), in: line) else {
                        problems.append("\(place): the key is not a string literal")
                        continue
                    }
                    let literal = String(line[literalRange].dropFirst().dropLast())
                    if literal.contains("\\(") {
                        problems.append("\(place): the key interpolates; use a format key")
                        continue
                    }
                    uses.append(Use(kind: kind, key: unescape(literal), place: place))
                }
            }
        }
        return (uses, problems)
    }()

    static func unescape(_ literal: String) -> String {
        var result = ""
        var iterator = literal.makeIterator()
        while let character = iterator.next() {
            guard character == "\\", let escaped = iterator.next() else {
                result.append(character)
                continue
            }
            switch escaped {
            case "n": result.append("\n")
            case "t": result.append("\t")
            default: result.append(escaped)
            }
        }
        return result
    }

    static func plist(_ language: String, _ file: String) -> [String: Any]? {
        let url = root.appending(path: "Support/\(language).lproj/\(file)")
        guard let data = try? Data(contentsOf: url) else { return nil }
        return try? PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any]
    }

    static func strings(_ language: String) -> [String: String] {
        (plist(language, "Localizable.strings") as? [String: String]) ?? [:]
    }

    static func plurals(_ language: String) -> [String: Any] {
        plist(language, "Localizable.stringsdict") ?? [:]
    }

    static let specifier = try! NSRegularExpression(pattern: #"%(?:(\d+)\$)?[-+ #0']*(?:\d+|\*)?(?:\.(?:\d+|\*))?(hh|h|ll|l|q|z|t|j|L)?([@dDiuUxXoOfeEgGcCsSpaA%])"#)

    static func arguments(_ format: String) -> [String] {
        var byPosition: [Int: String] = [:]
        var next = 1
        let range = NSRange(format.startIndex..., in: format)
        for match in specifier.matches(in: format, range: range) {
            let conversion = String(format[Range(match.range(at: 3), in: format)!])
            guard conversion != "%" else { continue }
            let position = Range(match.range(at: 1), in: format).flatMap { Int(format[$0]) } ?? next
            next = position + 1
            let kind = switch conversion {
            case "@": "object"
            case "f", "e", "E", "g", "G", "a", "A": "double"
            default: "integer"
            }
            byPosition[position] = kind
        }
        return byPosition.sorted { $0.key < $1.key }.map(\.value)
    }

    @Test func everyKeyIsAStringLiteral() {
        #expect(Self.scan.problems.isEmpty, "\(Self.scan.problems)")
        #expect(Self.scan.uses.count > 100)
    }

    @Test(arguments: languages) func everyKeyInTheCodeIsTranslated(language: String) {
        let strings = Self.strings(language)
        let plurals = Self.plurals(language)
        let missing = Self.scan.uses.filter { use in
            use.kind == .plural ? plurals[use.key] == nil : strings[use.key] == nil
        }
        #expect(missing.isEmpty, "\(language) is missing: \(missing.map { "\($0.key) (\($0.place))" })")
    }

    @Test(arguments: languages) func everyTranslationIsUsed(language: String) {
        let used = Set(Self.scan.uses.map(\.key))
        let unused = Set(Self.strings(language).keys).union(Self.plurals(language).keys).subtracting(used)
        #expect(unused.isEmpty, "\(language) has unused keys: \(unused.sorted())")
    }

    @Test(arguments: languages) func translatedFormatsReadTheSameArguments(language: String) {
        let strings = Self.strings(language)
        var broken: [String] = []
        for use in Set(Self.scan.uses.map { Use(kind: $0.kind, key: $0.key, place: "") }) where use.kind == .format || use.kind == .template {
            guard let translation = strings[use.key] else { continue }
            if Self.arguments(translation) != Self.arguments(use.key) { broken.append("\(use.key) → \(translation)") }
        }
        #expect(broken.isEmpty, "\(language): \(broken)")
    }

    @Test(arguments: languages) func pluralFormsReadTheCount(language: String) throws {
        let plurals = Self.plurals(language)
        for use in Self.scan.uses where use.kind == .plural {
            let entry = try #require(plurals[use.key] as? [String: Any], "\(language): \(use.key)")
            let format = try #require(entry["NSStringLocalizedFormatKey"] as? String)
            let variables = entry.filter { $0.key != "NSStringLocalizedFormatKey" }
            #expect(variables.count == 1, "\(language): \(use.key)")
            for (name, value) in variables {
                #expect(format.contains("%#@\(name)@"), "\(language): \(use.key)")
                let rule = try #require(value as? [String: String])
                #expect(rule["NSStringFormatSpecTypeKey"] == "NSStringPluralRuleType")
                let forms = rule.filter { !$0.key.hasPrefix("NSStringFormat") }
                #expect(forms["other"] != nil && forms["one"] != nil, "\(language): \(use.key)")
                for (form, text) in forms {
                    let reads = Self.arguments(text)
                    #expect(reads.isEmpty || reads == ["integer"], "\(language): \(use.key) \(form)")
                }
            }
        }
    }

    @Test func theBundleDeclaresEveryLanguage() throws {
        let data = try Data(contentsOf: Self.root.appending(path: "Support/Info.plist"))
        let info = try #require(try PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any])
        #expect(info["CFBundleLocalizations"] as? [String] == Self.languages)
        #expect(info["CFBundleDevelopmentRegion"] as? String == "en")
        for language in Self.languages {
            let strings = try #require(Self.plist(language, "InfoPlist.strings"), "\(language)")
            #expect(strings["NSHumanReadableCopyright"] != nil, "\(language)")
        }
    }

    @Test func appTextIsNeverABareLiteral() {
        let literal = #""((?:[^"\\]|\\.)*)""#
        let patterns = [
            #"\b(?:Text|Toggle|Label|Button|Picker|Section)\(\s*"# + literal,
            #"\.(?:help|accessibilityLabel|accessibilityHint|accessibilityValue|navigationTitle)\(\s*"# + literal,
            #"\b(?:title|subtitle|reason|text|accessibilityText|withTitle|name|accessibilityDescription|caption|message):\s*"# + literal,
            #"\b(?:note|item|submenu|fail)\(\s*"# + literal,
        ].map { try! NSRegularExpression(pattern: $0) }
        var bare: [String] = []
        for file in Self.swiftFiles(in: "Sources/Earmatch") {
            for (index, line) in file.lines.enumerated() where !line.trimmingCharacters(in: .whitespaces).hasPrefix("//") {
                let range = NSRange(line.startIndex..., in: line)
                for pattern in patterns {
                    for match in pattern.matches(in: line, range: range) {
                        let text = String(line[Range(match.range(at: 1), in: line)!])
                        guard text.rangeOfCharacter(from: .letters) != nil, !Self.untranslated.contains(text) else { continue }
                        bare.append("\(file.place):\(index + 1) \"\(text)\"")
                    }
                }
            }
        }
        #expect(bare.isEmpty, "\(bare)")
    }
}
