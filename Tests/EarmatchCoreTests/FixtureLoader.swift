import Testing
import Foundation
import EarmatchCore

enum Fixture {
    static func url(_ name: String) throws -> URL {
        try #require(Bundle.module.url(forResource: name, withExtension: nil, subdirectory: "Fixtures"))
    }

    static func snapshot(_ name: String) throws -> RekordboxWindowSnapshot {
        try JSONDecoder().decode(RekordboxWindowSnapshot.self, from: Data(contentsOf: url(name)))
    }
}
