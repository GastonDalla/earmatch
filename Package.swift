// swift-tools-version: 6.4
import PackageDescription

let upcomingFeatures: [SwiftSetting] = [
    .enableUpcomingFeature("NonisolatedNonsendingByDefault"),
    .enableUpcomingFeature("InferIsolatedConformances"),
]

let package = Package(
    name: "Earmatch",
    platforms: [.macOS(.v14)],
    dependencies: [
        .package(url: "https://github.com/sparkle-project/Sparkle", exact: "2.10.1"),
    ],
    targets: [
        .target(
            name: "EarmatchCore",
            swiftSettings: upcomingFeatures
        ),
        .executableTarget(
            name: "Earmatch",
            dependencies: [
                "EarmatchCore",
                .product(name: "Sparkle", package: "Sparkle"),
            ],
            swiftSettings: [.defaultIsolation(MainActor.self)] + upcomingFeatures,
            linkerSettings: [.unsafeFlags(["-Xlinker", "-rpath", "-Xlinker", "@executable_path/../Frameworks"])]
        ),
        .testTarget(
            name: "EarmatchCoreTests",
            dependencies: ["EarmatchCore"],
            resources: [.copy("Fixtures")],
            swiftSettings: upcomingFeatures
        ),
    ]
)
