import SwiftUI

/// Your anchors for today on the wrist: tap to mark one done. Asks who you are the first time
/// (remembered per server on the watch).
struct WatchAnchorsView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var board: AnchorBoard?
    @State private var meId: String?
    @State private var busy: Set<String> = []
    @State private var message: String?
    @State private var unavailable = false

    private var me: AnchorPerson? {
        guard let meId else { return nil }
        return board?.people.first { $0.id == meId }
    }

    var body: some View {
        NavigationStack {
            Group {
                if unavailable {
                    Text("Anchors are off on this speaker.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                } else if let board, let me {
                    List {
                        ForEach(AnchorSlots.grouped(me.anchors.filter { ["open", "kept"].contains($0.cell(on: board.today)?.state ?? "") }), id: \.slot) { group in
                            Section(AnchorSlots.label(group.slot)) {
                                ForEach(group.anchors) { anchor in
                                    row(anchor, cell: anchor.cell(on: board.today), today: board.today, color: AnchorColor.of(me.color))
                                }
                            }
                        }
                        Button("Not \(me.name)?") { setMe(nil) }
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                } else if let board {
                    List(board.people.filter(\.isLocal)) { person in
                        Button(person.name) { setMe(person.id) }
                    }
                } else {
                    ProgressView()
                }
            }
            .navigationTitle(me.map { "\($0.todayDone)/\($0.todayTotal)" } ?? "Anchors")
        }
        .overlay(alignment: .bottom) {
            if let message {
                Text(message)
                    .font(.caption2)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 4)
                    .background(.regularMaterial, in: Capsule())
                    .padding(.bottom, 4)
            }
        }
        .task { await load() }
    }

    private func row(_ anchor: Anchor, cell: AnchorCell?, today: String, color: Color) -> some View {
        Button {
            Task { await toggle(anchor, cell: cell, today: today) }
        } label: {
            HStack {
                Image(systemName: cell?.isKept == true ? "checkmark.circle.fill" : "circle")
                    .foregroundStyle(cell?.isKept == true ? color : .secondary)
                Text(anchor.title)
                    .font(.caption)
                    .lineLimit(2)
            }
        }
        .disabled(!anchor.writable || busy.contains(anchor.id))
    }

    private func setMe(_ id: String?) {
        if let serverId = store.selectedConnection?.id {
            AnchorMe.set(id, serverId: serverId)
        }
        meId = id
    }

    private func load() async {
        guard let api = store.api else { return }
        if let serverId = store.selectedConnection?.id {
            meId = AnchorMe.get(serverId: serverId)
        }
        do {
            board = try await api.getAnchorBoard()
            unavailable = false
        } catch APIError.serverError(404, _) {
            unavailable = true
        } catch {
            flash("Couldn't load")
        }
    }

    private func toggle(_ anchor: Anchor, cell: AnchorCell?, today: String) async {
        guard let api = store.api else { return }
        busy.insert(anchor.id)
        defer { busy.remove(anchor.id) }
        do {
            try await api.setAnchorDone(anchorId: anchor.id, date: today, done: cell?.isKept != true)
            await load()
        } catch {
            flash("Didn't save")
        }
    }

    private func flash(_ text: String) {
        withAnimation { message = text }
        Task {
            try? await Task.sleep(for: .seconds(1.5))
            withAnimation { message = nil }
        }
    }
}
