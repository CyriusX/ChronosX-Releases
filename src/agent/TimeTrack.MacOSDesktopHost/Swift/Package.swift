// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "TimeTrackMacOS",
    platforms: [.macOS(.v12)],
    products: [
        .executable(name: "TimeTrack", targets: ["TimeTrack"])
    ],
    dependencies: [
        .package(url: "https://github.com/sparkle-project/Sparkle.git", from: "2.0.0")
    ],
    targets: [
        .executableTarget(name: "TimeTrack", dependencies: [.product(name: "Sparkle", package: "Sparkle")])
    ]
)