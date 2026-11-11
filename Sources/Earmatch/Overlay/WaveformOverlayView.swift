import AppKit
import EarmatchCore
import QuartzCore

final class WaveformOverlayView: LayerHostView {
    private var panes: [Int: DeckPane] = [:]
    private var bands: [Int: PhaseMeterLayer] = [:]
    private var dividers: [CALayer] = []
    private var plan: WaveformOverlayPlan?
    private var strips: [Int: CGRect] = [:]
    private var shownFrame: FrameReading?
    private var shownMaster: Int?

    func configure(plan: WaveformOverlayPlan, strips: [Int: CGRect]) {
        guard plan != self.plan || strips != self.strips else { return }
        self.plan = plan
        self.strips = strips
        Self.withoutAnimation {
            while dividers.count < plan.dividers.count {
                let divider = CALayer()
                divider.backgroundColor = OverlayColor.divider
                root.addSublayer(divider)
                dividers.append(divider)
            }
            for (index, divider) in dividers.enumerated() {
                divider.isHidden = index >= plan.dividers.count
                if index < plan.dividers.count { divider.frame = plan.dividers[index] }
            }
            let paneDecks = Set(plan.panes.map(\.deck)), bandDecks = Set(plan.bands.keys)
            for number in Set(panes.keys) where !paneDecks.contains(number) {
                panes.removeValue(forKey: number)?.removeFromSuperlayer()
            }
            for number in Set(bands.keys) where !bandDecks.contains(number) {
                bands.removeValue(forKey: number)?.removeFromSuperlayer()
            }
            for spec in plan.panes {
                let pane = panes[spec.deck] ?? DeckPane()
                if pane.superlayer == nil { root.insertSublayer(pane, at: 0) }
                panes[spec.deck] = pane
                pane.frame = spec.frame
                pane.layout(waveformHeight: spec.waveformHeight, showsMeter: spec.showsMeter, showsWaveforms: plan.allowsMasterWaveform)
            }
            for (number, frame) in plan.bands {
                let band = bands[number] ?? PhaseMeterLayer()
                if band.superlayer == nil { root.addSublayer(band) }
                bands[number] = band
                band.backgroundColor = OverlayColor.band
                band.cornerRadius = 3
                band.rowsCenter = 0.5
                band.frame = frame
                band.isHidden = true
            }
        }
    }

    func showFrame(_ frame: FrameReading?, master: Int?) {
        guard let plan else { return }
        let showsWaveforms = plan.allowsMasterWaveform
        shownFrame = showsWaveforms ? frame : nil
        shownMaster = master
        let masterStrip = master.flatMap { strips[$0] }
        for spec in plan.panes {
            guard let pane = panes[spec.deck] else { continue }
            if showsWaveforms, let frame {
                pane.showWaveform(frame, source: spec.source)
                pane.meter.showMasterWaveform(master == spec.deck ? nil : frame, strip: masterStrip)
            } else {
                pane.showWaveform(nil, source: .zero)
                pane.meter.showMasterWaveform(nil, strip: nil)
            }
        }
    }

    func showMeter(_ display: PhaseMeterDisplay?, deck: Int) {
        let scale = window?.backingScaleFactor ?? 2
        if let pane = panes[deck] {
            pane.meter.show(pane.showsMeter ? display : nil, scale: scale)
            if pane.meter.isWaitingForMasterWaveform, let master = shownMaster, master != deck, let strip = strips[master] {
                pane.meter.showMasterWaveform(shownFrame, strip: strip)
            }
        }
        bands[deck]?.show(display, scale: scale)
    }
}

final class SideMeterView: LayerHostView {
    let meter = PhaseMeterLayer()

    override init() {
        super.init()
        meter.backgroundColor = OverlayColor.band
        meter.cornerRadius = 3
        meter.rowsCenter = 0.5
        root.addSublayer(meter)
    }

    override func layout() {
        super.layout()
        Self.withoutAnimation { meter.frame = bounds }
    }

    func show(_ display: PhaseMeterDisplay?) {
        if meter.frame != bounds { meter.frame = bounds }
        meter.show(display, scale: window?.backingScaleFactor ?? 2)
    }
}

nonisolated private final class DeckPane: CALayer {
    let meter = PhaseMeterLayer()
    private(set) var showsMeter = true
    private let waveform = CALayer()
    private var shownSurface: IOSurfaceRef?

    override init() {
        super.init()
        backgroundColor = OverlayColor.black
        masksToBounds = true
        waveform.contentsGravity = .resize
        waveform.isOpaque = true
        addSublayer(waveform)
        addSublayer(meter)
    }

    override init(layer: Any) { super.init(layer: layer) }

    @available(*, unavailable)
    required init?(coder: NSCoder) { fatalError("init(coder:) is not supported") }

    func layout(waveformHeight: CGFloat, showsMeter: Bool, showsWaveforms: Bool) {
        self.showsMeter = showsMeter
        waveform.frame = CGRect(x: 0, y: bounds.height - waveformHeight, width: bounds.width, height: waveformHeight)
        meter.frame = CGRect(x: 0, y: 0, width: bounds.width, height: bounds.height - waveformHeight)
        meter.playheadExtension = waveformHeight
        meter.allowsMasterWaveform = showsWaveforms
        if !showsMeter { meter.isHidden = true }
    }

    func showWaveform(_ frame: FrameReading?, source: CGRect) {
        guard let frame, let surface = frame.surface, frame.pixelSize.width > 0, frame.pixelSize.height > 0 else {
            waveform.isHidden = true
            waveform.contents = nil
            shownSurface = nil
            return
        }
        waveform.isHidden = false
        if shownSurface !== surface {
            waveform.contents = surface
            shownSurface = surface
        }
        let pixels = frame.pixelRect(source)
        waveform.contentsRect = CGRect(x: pixels.minX / frame.pixelSize.width, y: pixels.minY / frame.pixelSize.height,
                                       width: pixels.width / frame.pixelSize.width, height: pixels.height / frame.pixelSize.height)
    }
}
