import SwiftUI
import EarmatchCore

struct PhaseMeterPreview: View {
    let type: PhaseMeterType
    let live: PhaseMeterDisplay?
    let isPaused: Bool

    var body: some View {
        if let live {
            PhaseMeterCanvas(display: live)
        } else {
            TimelineView(.animation(paused: isPaused)) { timeline in
                PhaseMeterCanvas(display: PreviewClock(date: timeline.date).meterDisplay(style: type))
            }
        }
    }
}
