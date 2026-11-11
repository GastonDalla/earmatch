import SwiftUI

struct Surface<SurfaceShape: InsettableShape, Fill: ShapeStyle, Stroke: ShapeStyle>: ViewModifier {
    let shape: SurfaceShape
    let fill: Fill
    let stroke: Stroke
    var lineWidth: CGFloat = 1

    func body(content: Content) -> some View {
        content
            .background(fill, in: shape)
            .overlay { shape.strokeBorder(stroke, lineWidth: lineWidth) }
    }
}

extension View {
    func surface(_ shape: some InsettableShape, fill: some ShapeStyle, stroke: some ShapeStyle, lineWidth: CGFloat = 1) -> some View {
        modifier(Surface(shape: shape, fill: fill, stroke: stroke, lineWidth: lineWidth))
    }

    func surface(cornerRadius: CGFloat, fill: some ShapeStyle, stroke: some ShapeStyle) -> some View {
        surface(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous), fill: fill, stroke: stroke)
    }
}
