import SwiftUI

/// Every speaker at a glance, with quick controls. Tapping a room selects it.
struct RoomsView: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(\.dismiss) private var dismiss
    @State private var rooms: [UUID: RoomState] = [:]
    @State private var message: String?
    @State private var isMoving = false

    enum RoomState {
        case loading
        case unreachable
        case status(PlayerStatus)
    }

    var body: some View {
        NavigationStack {
            List {
                Section {
                    ForEach(store.connections) { connection in
                        roomRow(connection)
                    }
                } footer: {
                    if let message {
                        Text(message)
                    }
                }

                if store.connections.count > 1 {
                    Section {
                        Button(role: .destructive) {
                            Task { await stopAll() }
                        } label: {
                            Label("Stop All Rooms", systemImage: "stop.circle")
                        }
                    }
                }

                Section {
                    NavigationLink {
                        ServerListView()
                    } label: {
                        Label("Manage Servers", systemImage: "server.rack")
                    }
                }
            }
            .navigationTitle("Rooms")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Done") { dismiss() }
                }
            }
            .overlay {
                if isMoving {
                    ProgressView("Moving…")
                        .padding()
                        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 12))
                }
            }
            .task {
                while !Task.isCancelled {
                    await refreshAll()
                    try? await Task.sleep(for: .seconds(3))
                }
            }
        }
    }

    // MARK: - Rows

    private func roomRow(_ connection: ServerConnection) -> some View {
        let state = rooms[connection.id] ?? .loading
        let isSelected = store.selectedConnection?.id == connection.id
        return HStack(spacing: 12) {
            Button {
                store.select(connection)
                dismiss()
            } label: {
                HStack(spacing: 12) {
                    Image(systemName: isSelected ? "checkmark.circle.fill" : "hifispeaker")
                        .foregroundStyle(isSelected ? Color.accentColor : .secondary)
                        .frame(width: 24)
                    VStack(alignment: .leading, spacing: 2) {
                        Text(connection.name)
                            .font(.headline)
                        statusLine(state)
                    }
                    Spacer(minLength: 0)
                }
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)

            if case .status(let status) = state {
                Button {
                    Task { await toggle(connection, status: status) }
                } label: {
                    Image(systemName: status.isPlaying ? "stop.fill" : "play.fill")
                        .frame(width: 32, height: 32)
                }
                .buttonStyle(.borderless)
                if status.isPlaying {
                    Button {
                        Task { await skip(connection) }
                    } label: {
                        Image(systemName: "forward.fill")
                            .frame(width: 32, height: 32)
                    }
                    .buttonStyle(.borderless)
                }
            }
        }
        .contextMenu {
            moveMenu(to: connection)
        }
        .swipeActions(edge: .leading) {
            if let source = movableSource(excluding: connection) {
                Button {
                    Task { await move(from: source, to: connection) }
                } label: {
                    Label("Move Here", systemImage: "arrow.right.circle")
                }
                .tint(.indigo)
            }
        }
    }

    @ViewBuilder
    private func statusLine(_ state: RoomState) -> some View {
        switch state {
        case .loading:
            Text("Checking…")
                .font(.caption)
                .foregroundStyle(.secondary)
        case .unreachable:
            Label("Can't reach", systemImage: "wifi.exclamationmark")
                .font(.caption)
                .foregroundStyle(.orange)
        case .status(let status):
            if status.isPlaying {
                let subtitle = status.displaySubtitle
                Text(subtitle.isEmpty ? status.displayTitle : "\(status.displayTitle) · \(subtitle)")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                Text("Volume \(status.volume)")
                    .font(.caption2)
                    .foregroundStyle(.tertiary)
            } else {
                Text("Not playing")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
    }

    @ViewBuilder
    private func moveMenu(to target: ServerConnection) -> some View {
        ForEach(store.connections.filter { $0.id != target.id && isPlaying($0) }) { source in
            Button {
                Task { await move(from: source, to: target) }
            } label: {
                Label("Move \(source.name)'s Music Here", systemImage: "arrow.right.circle")
            }
        }
    }

    // MARK: - Actions

    private func isPlaying(_ connection: ServerConnection) -> Bool {
        if case .status(let status) = rooms[connection.id] { return status.isPlaying }
        return false
    }

    /// The room to move from when swiping: the selected one if it's playing,
    /// else the only other playing room.
    private func movableSource(excluding target: ServerConnection) -> ServerConnection? {
        if let selected = store.selectedConnection, selected.id != target.id, isPlaying(selected) {
            return selected
        }
        let playing = store.connections.filter { $0.id != target.id && isPlaying($0) }
        return playing.count == 1 ? playing[0] : nil
    }

    private func refreshAll() async {
        let connections = store.connections
        await withTaskGroup(of: (UUID, RoomState).self) { group in
            for connection in connections {
                group.addTask {
                    if let status = try? await APIClient.client(for: connection).getPlayerStatus() {
                        return (connection.id, .status(status))
                    }
                    return (connection.id, .unreachable)
                }
            }
            for await (id, state) in group {
                rooms[id] = state
            }
        }
    }

    private func toggle(_ connection: ServerConnection, status: PlayerStatus) async {
        let api = APIClient.client(for: connection)
        do {
            if status.isPlaying { try await api.stop() } else { try await api.resume() }
        } catch {
            message = "\(connection.name): \(error.localizedDescription)"
        }
        await SpeakerSurfaces.refresh(after: connection)
        await refreshAll()
    }

    private func skip(_ connection: ServerConnection) async {
        try? await APIClient.client(for: connection).skipToNext()
        await SpeakerSurfaces.refresh(after: connection)
        await refreshAll()
    }

    private func stopAll() async {
        let connections = store.connections
        let failed = await withTaskGroup(of: String?.self) { group in
            for connection in connections {
                group.addTask {
                    do {
                        try await APIClient.client(for: connection).stop()
                        return nil
                    } catch {
                        return connection.name
                    }
                }
            }
            var names: [String] = []
            for await name in group {
                if let name { names.append(name) }
            }
            return names.sorted()
        }
        message = failed.isEmpty
            ? "Stopped all rooms."
            : "Couldn't reach \(ListFormatter.localizedString(byJoining: failed))."
        for connection in connections where !failed.contains(connection.name) {
            await SpeakerSurfaces.refresh(after: connection)
        }
        await refreshAll()
    }

    private func move(from source: ServerConnection, to target: ServerConnection) async {
        isMoving = true
        defer { isMoving = false }
        do {
            let result = try await PlaybackMover.move(from: source, to: target)
            message = result.description
            store.select(target)
        } catch {
            message = error.localizedDescription
        }
        SpeakerSurfaces.reloadWidgets()
        await refreshAll()
    }
}
