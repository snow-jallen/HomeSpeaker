import SwiftUI

struct MainTabView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var selectedTab = 0
    @State private var showServerPicker = false

    var body: some View {
        TabView(selection: $selectedTab) {
            NowPlayingView()
                .tabItem { Label("Now Playing", systemImage: "music.note") }
                .tag(0)

            MusicLibraryView()
                .tabItem { Label("Library", systemImage: "music.note.list") }
                .tag(1)

            QueueView()
                .tabItem { Label("Queue", systemImage: "list.number") }
                .tag(2)

            PlaylistsView()
                .tabItem { Label("Playlists", systemImage: "music.quarternote.3") }
                .tag(3)

            MoreView()
                .tabItem { Label("More", systemImage: "ellipsis.circle") }
                .tag(4)
        }
        .sheet(isPresented: $showServerPicker) {
            ServerPickerSheet()
        }
    }
}

struct ServerPickerSheet: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            ServerListView()
                .navigationBarTitleDisplayMode(.inline)
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) {
                        Button("Done") { dismiss() }
                    }
                }
        }
    }
}

struct ServerListView: View {
    @Environment(ConnectionStore.self) private var store
    @StateObject private var browser = SpeakerBrowser()
    @State private var showAdd = false
    @State private var editingConnection: ServerConnection?
    @State private var addingSpeaker: DiscoveredSpeaker?

    var body: some View {
        List {
            if store.connections.isEmpty {
                ContentUnavailableView {
                    Label("No Servers", systemImage: "wifi.slash")
                } description: {
                    Text("Add a HomeSpeaker server to get started.")
                }
                NearbySpeakersSection(speakers: store.unsavedSpeakers(in: browser.found)) { speaker in
                    addingSpeaker = speaker
                }
            } else {
                ForEach(store.connections, id: \.id) { (connection: ServerConnection) in
                    Button {
                        store.select(connection)
                    } label: {
                        HStack {
                            VStack(alignment: .leading) {
                                Text(connection.name)
                                    .font(.headline)
                                Text("\(connection.host):\(connection.port)")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                            Spacer()
                            if store.selectedConnection?.id == connection.id {
                                Image(systemName: "checkmark")
                                    .foregroundStyle(Color.accentColor)
                            }
                        }
                    }
                    .foregroundStyle(.primary)
                    .swipeActions(edge: .trailing) {
                        Button(role: .destructive) {
                            store.remove(connection)
                        } label: {
                            Label("Delete", systemImage: "trash")
                        }
                        Button {
                            editingConnection = connection
                        } label: {
                            Label("Edit", systemImage: "pencil")
                        }
                        .tint(.orange)
                    }
                }
            }
        }
        .navigationTitle("Servers")
        .toolbar {
            ToolbarItem(placement: .primaryAction) {
                Button { showAdd = true } label: {
                    Image(systemName: "plus")
                }
            }
        }
        .sheet(isPresented: $showAdd) {
            AddEditServerView()
        }
        .sheet(item: $editingConnection) { conn in
            AddEditServerView(existing: conn)
        }
        .sheet(item: $addingSpeaker) { speaker in
            AddEditServerView(prefill: speaker)
        }
        .onAppear { browser.start() }
        .onDisappear { browser.stop() }
    }
}

/// Speakers advertising themselves on the local network (Bonjour) that
/// aren't saved yet. Tapping one fills in the Add Server form.
struct NearbySpeakersSection: View {
    let speakers: [DiscoveredSpeaker]
    let onSelect: (DiscoveredSpeaker) -> Void

    var body: some View {
        Section {
            if speakers.isEmpty {
                HStack(spacing: 8) {
                    ProgressView()
                    Text("Looking for speakers on this network…")
                        .foregroundStyle(.secondary)
                }
            } else {
                ForEach(speakers) { speaker in
                    Button {
                        onSelect(speaker)
                    } label: {
                        HStack {
                            VStack(alignment: .leading) {
                                Text(speaker.suggestedName)
                                    .font(.headline)
                                Text("\(speaker.host):\(speaker.port)")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                            Spacer()
                            Image(systemName: "plus.circle")
                                .foregroundStyle(Color.accentColor)
                        }
                    }
                    .foregroundStyle(.primary)
                }
            }
        } header: {
            Text("Nearby speakers")
        }
    }
}

private extension ConnectionStore {
    /// The discovered speakers that no saved server already points at (same host and port).
    func unsavedSpeakers(in found: [DiscoveredSpeaker]) -> [DiscoveredSpeaker] {
        found.filter { speaker in
            !connections.contains { speaker.matches($0) }
        }
    }
}

struct AddEditServerView: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(\.dismiss) private var dismiss

    var existing: ServerConnection?
    /// A speaker found on the network to fill the form with.
    var prefill: DiscoveredSpeaker?

    @StateObject private var browser = SpeakerBrowser()
    @State private var name = ""
    @State private var host = ""
    @State private var port = "80"
    @State private var remoteURL = ""
    @State private var isVerifying = false
    @State private var pendingConn: ServerConnection?
    @State private var verifyWarning: VerifyWarning?

    var isEditing: Bool { existing != nil }

    struct VerifyWarning: Identifiable {
        let id = UUID()
        let title: String
        let message: String
    }

    var body: some View {
        NavigationStack {
            Form {
                if !isEditing {
                    NearbySpeakersSection(speakers: store.unsavedSpeakers(in: browser.found)) { speaker in
                        apply(speaker)
                    }
                }
                Section("Connection Details") {
                    TextField("Name", text: $name)
                        .textInputAutocapitalization(.words)
                    TextField("Host or IP address", text: $host)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .keyboardType(.URL)
                    TextField("Port", text: $port)
                        .keyboardType(.numberPad)
                }
                Section {
                    Text("Enter the hostname or IP address of your HomeSpeaker server, e.g. 192.168.1.100 or homespeaker.local")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Section {
                    TextField("Remote address (optional)", text: $remoteURL)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .keyboardType(.URL)
                } header: {
                    Text("Away From Home")
                } footer: {
                    Text("Used when the address above can't be reached, e.g. kitchen.boston-cloud.ts.net over Tailscale. Uses https unless you type http://.")
                }
            }
            .navigationTitle(isEditing ? "Edit Server" : "Add Server")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    if isVerifying {
                        ProgressView()
                    } else {
                        Button("Save") { save() }
                            .disabled(name.isEmpty || host.isEmpty)
                    }
                }
            }
            .onAppear {
                if let existing {
                    name = existing.name
                    host = existing.host
                    port = "\(existing.port)"
                    remoteURL = existing.remoteURL ?? ""
                } else {
                    if let prefill {
                        apply(prefill)
                    }
                    browser.start()
                }
            }
            .onDisappear { browser.stop() }
            .alert(item: $verifyWarning) { warning in
                Alert(
                    title: Text(warning.title),
                    message: Text(warning.message),
                    primaryButton: .default(Text("Save Anyway")) {
                        if let conn = pendingConn { commitSave(conn) }
                    },
                    secondaryButton: .cancel(Text("Go Back"))
                )
            }
        }
    }

    private func apply(_ speaker: DiscoveredSpeaker) {
        name = speaker.suggestedName
        host = speaker.hostForConnection
        port = "\(speaker.port)"
    }

    private func save() {
        let portNum = Int(port) ?? 80
        let trimmedRemote = remoteURL.trimmingCharacters(in: .whitespacesAndNewlines)
        let remote = trimmedRemote.isEmpty ? nil : trimmedRemote
        let conn: ServerConnection
        if var existing {
            existing.name = name
            existing.host = host
            existing.port = portNum
            existing.remoteURL = remote
            conn = existing
        } else {
            conn = ServerConnection(name: name, host: host, port: portNum, remoteURL: remote)
        }

        isVerifying = true
        pendingConn = conn

        Task {
            defer { isVerifying = false }
            let client = APIClient(connection: conn)
            do {
                _ = try await client.getPlayerStatus()
                commitSave(conn)
            } catch let error as APIError {
                switch error {
                case .networkError:
                    verifyWarning = VerifyWarning(
                        title: "Server Unreachable",
                        message: "Couldn't connect to \(conn.host). Make sure the address is correct and the server is running."
                    )
                case .serverError, .decodingError:
                    verifyWarning = VerifyWarning(
                        title: "Unexpected Response",
                        message: "\(conn.host) is reachable but doesn't appear to be running HomeSpeaker. Double-check the address and port."
                    )
                case .invalidURL:
                    verifyWarning = VerifyWarning(
                        title: "Invalid Address",
                        message: "The host address doesn't look valid. Please check and try again."
                    )
                }
            } catch {
                verifyWarning = VerifyWarning(
                    title: "Server Unreachable",
                    message: "Couldn't connect to \(conn.host): \(error.localizedDescription)"
                )
            }
        }
    }

    private func commitSave(_ conn: ServerConnection) {
        if isEditing {
            store.update(conn)
        } else {
            store.add(conn)
        }
        dismiss()
    }
}
