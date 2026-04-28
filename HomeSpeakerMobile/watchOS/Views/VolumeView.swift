import SwiftUI

struct WatchVolumeView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var volume: Double = 50
    @State private var isUpdating = false

    var body: some View {
        VStack(spacing: 12) {
            Text("Volume")
                .font(.headline)

            Text("\(Int(volume))")
                .font(.system(size: 40, weight: .bold, design: .rounded))
                .monospacedDigit()

            Slider(value: $volume, in: 0...100, step: 5)
                .tint(.accentColor)
                .onChange(of: volume) { _, newValue in
                    Task { await setVolume(Int(newValue)) }
                }

            HStack(spacing: 20) {
                Button {
                    volume = max(0, volume - 10)
                    Task { await setVolume(Int(volume)) }
                } label: {
                    Image(systemName: "minus")
                        .font(.title3)
                }
                .buttonStyle(.bordered)

                Button {
                    volume = min(100, volume + 10)
                    Task { await setVolume(Int(volume)) }
                } label: {
                    Image(systemName: "plus")
                        .font(.title3)
                }
                .buttonStyle(.bordered)
            }
        }
        .padding()
        .task {
            guard let api = store.api else { return }
            if let status = try? await api.getPlayerStatus() {
                volume = Double(status.volume)
            }
        }
    }

    private func setVolume(_ level: Int) async {
        guard let api = store.api else { return }
        try? await api.setVolume(level)
    }
}
