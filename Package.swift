// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "LGACMenuBar",
    platforms: [.macOS(.v14)],
    targets: [
        .target(name: "LGACCore"),
        .testTarget(name: "LGACCoreTests", dependencies: ["LGACCore"]),
    ]
)
