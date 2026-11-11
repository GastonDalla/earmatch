// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Earmatch",
    platforms: [.macOS(.v14)],
    dependencies: [
        .package(url: "https://github.com/sparkle-project/Sparkle", exact: "2.10.1"),
    ],
    targets: [
        // Pure logic: parsing rekordbox's Accessibility snapshot, pixel detection, phase math.
        .target(name: "EarmatchCore"),
        // macOS adapters: Accessibility, ScreenCaptureKit, overlay windows, menu bar, updates.
        .executableTarget(
            name: "Earmatch",
            dependencies: [
                "EarmatchCore",
                .product(name: "Sparkle", package: "Sparkle"),
            ],
            swiftSettings: [.swiftLanguageMode(.v5)],
            linkerSettings: [.unsafeFlags(["-Xlinker", "-rpath", "-Xlinker", "@executable_path/../Frameworks"])]
        ),
        .testTarget(
            name: "EarmatchCoreTests",
            dependencies: ["EarmatchCore"],
            resources: [.copy("Fixtures")]
        ),
    ]
)
