import SwiftUI

struct WatchRadioStreamsView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var streams: [RadioStream] = []
    @State private var isLoading = false
    @State private var actionMessage: String?

    var body: some View {
        NavigationStack {
            Group {
                if isLoading && streams.isEmpty {
                    ProgressView()
                } else if streams.isEmpty {
                    Text("No streams")
                        .foregroundStyle(.secondary)
                } else {
                    List(streams) { stream in
                        Button {
                            Task {
                                try? await store.api?.playRadioStream(id: stream.id)
                                flash(stream.name)
                            }
                        } label: {
                            Text(stream.name)
                                .font(.caption)
                                .lineLimit(2)
                        }
                    }
                }
            }
            .navigationTitle("Radio")
        }
        .overlay(alignment: .bottom) {
            if let msg = actionMessage {
                Text(msg)
                    .font(.caption2)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 4)
                    .background(.regularMaterial, in: Capsule())
                    .padding(.bottom, 4)
            }
        }
        .task { await load() }
    }

    private func load() async {
        guard let api = store.api else { return }
        isLoading = true
        streams = (try? await api.getRadioStreams()) ?? []
        isLoading = false
    }

    private func flash(_ msg: String) {
        withAnimation { actionMessage = msg }
        Task {
            try? await Task.sleep(for: .seconds(1.5))
            withAnimation { actionMessage = nil }
        }
    }
}
