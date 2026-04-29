import SwiftUI

struct YouTubeView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var query = ""
    @State private var results: [VideoDto] = []
    @State private var isSearching = false
    @State private var actionMessage: String?
    @State private var cachingIds: Set<String> = []

    var body: some View {
        NavigationStack {
            Group {
                if results.isEmpty && !isSearching {
                    ContentUnavailableView {
                        Label("Search YouTube", systemImage: "play.rectangle")
                    } description: {
                        Text("Search for videos to stream or download.")
                    }
                } else if isSearching {
                    ProgressView("Searching…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else {
                    resultsList
                }
            }
            .navigationTitle("YouTube")
            .searchable(text: $query, prompt: "Search YouTube")
            .onSubmit(of: .search) { Task { await search() } }
            .overlay(alignment: .bottom) {
                if let msg = actionMessage {
                    Text(msg)
                        .padding(.horizontal, 16)
                        .padding(.vertical, 8)
                        .background(.regularMaterial, in: Capsule())
                        .padding(.bottom, 8)
                        .transition(.move(edge: .bottom).combined(with: .opacity))
                }
            }
        }
    }

    private var resultsList: some View {
        List(results, id: \.id) { (video: VideoDto) in
            HStack(spacing: 12) {
                thumbnailView(video: video)
                VStack(alignment: .leading, spacing: 4) {
                    Text(video.title)
                        .font(.body)
                        .lineLimit(2)
                    if let author = video.author {
                        Text(author)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    if let duration = video.duration {
                        Text(duration)
                            .font(.caption2)
                            .foregroundStyle(.tertiary)
                    }
                }
                Spacer()
                VStack(spacing: 8) {
                    Button {
                        Task { await play(video: video) }
                    } label: {
                        Image(systemName: "play.fill")
                            .foregroundStyle(Color.accentColor)
                    }
                    .buttonStyle(.borderless)

                    if cachingIds.contains(video.id) {
                        ProgressView()
                            .scaleEffect(0.7)
                    } else {
                        Button {
                            Task { await cache(video: video) }
                        } label: {
                            Image(systemName: "arrow.down.circle")
                                .foregroundStyle(.green)
                        }
                        .buttonStyle(.borderless)
                    }
                }
            }
            .padding(.vertical, 4)
        }
    }

    @ViewBuilder
    private func thumbnailView(video: VideoDto) -> some View {
        if let thumb = video.thumbnail, let url = URL(string: thumb) {
            AsyncImage(url: url) { image in
                image.resizable().scaledToFill()
            } placeholder: {
                Rectangle().fill(Color(.systemGray5))
            }
            .frame(width: 80, height: 50)
            .clipShape(RoundedRectangle(cornerRadius: 6))
        } else {
            RoundedRectangle(cornerRadius: 6)
                .fill(Color(.systemGray5))
                .frame(width: 80, height: 50)
                .overlay {
                    Image(systemName: "play.rectangle")
                        .foregroundStyle(.secondary)
                }
        }
    }

    private func search() async {
        guard !query.isEmpty, let api = store.api else { return }
        isSearching = true
        results = (try? await api.searchYouTube(query)) ?? []
        isSearching = false
    }

    private func play(video: VideoDto) async {
        guard let api = store.api else { return }
        try? await api.playYouTubeVideo(id: video.id, title: video.title)
        showMessage("Streaming: \(video.title)")
    }

    private func cache(video: VideoDto) async {
        guard let api = store.api else { return }
        cachingIds.insert(video.id)
        try? await api.cacheYouTubeVideo(video)
        cachingIds.remove(video.id)
        showMessage("Downloading: \(video.title)")
    }

    private func showMessage(_ msg: String) {
        withAnimation { actionMessage = msg }
        Task {
            try? await Task.sleep(for: .seconds(2))
            withAnimation { actionMessage = nil }
        }
    }
}
