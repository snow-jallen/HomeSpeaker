import SwiftUI

struct WatchQueueView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var queue: [Song] = []
    @State private var isLoading = false

    var body: some View {
        NavigationStack {
            Group {
                if isLoading && queue.isEmpty {
                    ProgressView()
                } else if queue.isEmpty {
                    Text("Queue empty")
                        .foregroundStyle(.secondary)
                } else {
                    List {
                        ForEach(Array(queue.enumerated()), id: \.offset) { index, song in
                            VStack(alignment: .leading, spacing: 2) {
                                Text(song.displayTitle)
                                    .font(.caption)
                                    .lineLimit(1)
                                Text(song.displayArtist)
                                    .font(.caption2)
                                    .foregroundStyle(.secondary)
                                    .lineLimit(1)
                            }
                        }
                    }
                }
            }
            .navigationTitle("Queue")
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        Task { await shuffle() }
                    } label: {
                        Image(systemName: "shuffle")
                    }
                }
            }
        }
        .task { await load() }
    }

    private func load() async {
        guard let api = store.api else { return }
        isLoading = true
        queue = (try? await api.getQueue()) ?? []
        isLoading = false
    }

    private func shuffle() async {
        guard let api = store.api else { return }
        try? await api.shuffleQueue()
        await load()
    }
}
