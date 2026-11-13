import AppKit
import LGACCore
import SwiftUI

/// Ícone da barra: símbolo do modo atual; esmaecido se desligado, offline ou sem estado.
struct MenuBarIcon: View {
    let controller: ACController

    var body: some View {
        let active = controller.state?.isOn == true && !controller.isDeviceOffline
        Image(nsImage: StatusIcon.image(symbol: ACLabels.symbol(forJobMode: controller.state?.jobMode), dimmed: !active))
    }
}

enum StatusIcon {
    /// A barra de menu ignora `.opacity` em imagens template, então o esmaecido é desenhado com alpha.
    static func image(symbol: String, dimmed: Bool) -> NSImage {
        let config = NSImage.SymbolConfiguration(pointSize: 14, weight: .medium)
        guard
            let symbolImage = NSImage(systemSymbolName: symbol, accessibilityDescription: "Ar-condicionado")?
                .withSymbolConfiguration(config)
        else { return NSImage() }
        guard dimmed else {
            symbolImage.isTemplate = true
            return symbolImage
        }
        let size = symbolImage.size
        var rect = CGRect(x: 0, y: 0, width: size.width * 2, height: size.height * 2)
        guard let source = symbolImage.cgImage(forProposedRect: &rect, context: nil, hints: nil),
            let context = CGContext(
                data: nil, width: Int(rect.width), height: Int(rect.height), bitsPerComponent: 8, bytesPerRow: 0,
                space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
        else { return symbolImage }
        context.setAlpha(0.35)
        context.draw(source, in: rect)
        guard let faded = context.makeImage() else { return symbolImage }
        let result = NSImage(cgImage: faded, size: size)
        result.isTemplate = true
        return result
    }
}
