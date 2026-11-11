import Foundation
import EarmatchCore

final class SettingsStore {
    static let didChange = Notification.Name("EarmatchSettingsDidChange")
    private let defaults: UserDefaults
    private let key = "overlaySettings"

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    var settings: OverlaySettings {
        get {
            guard let data = defaults.data(forKey: key),
                  let decoded = try? JSONDecoder().decode(OverlaySettings.self, from: data)
            else { return OverlaySettings() }
            return decoded
        }
        set {
            guard newValue != settings, let data = try? JSONEncoder().encode(newValue) else { return }
            defaults.set(data, forKey: key)
            NotificationCenter.default.post(name: Self.didChange, object: self)
        }
    }

    func update(_ change: (inout OverlaySettings) -> Void) {
        var copy = settings
        change(&copy)
        settings = copy
    }
}
