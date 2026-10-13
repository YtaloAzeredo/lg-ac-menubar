// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "LGACMenuBar",
    platforms: [.macOS(.v14)],
    targets: [
        .target(name: "LGACCore"),
        .executableTarget(name: "lgac-dump", dependencies: ["LGACCore"]),
        .testTarget(name: "LGACCoreTests", dependencies: ["LGACCore"]),
    ]
)
