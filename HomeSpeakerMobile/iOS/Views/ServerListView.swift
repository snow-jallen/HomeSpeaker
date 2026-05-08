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
    @State private var showAdd = false
    @State private var editingConnection: ServerConnection?

    var body: some View {
        List {
            if store.connections.isEmpty {
                ContentUnavailableView {
                    Label("No Servers", systemImage: "wifi.slash")
                } description: {
                    Text("Add a HomeSpeaker server to get started.")
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
    }
}

struct AddEditServerView: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(\.dismiss) private var dismiss

    var existing: ServerConnection?

    @State private var name = ""
    @State private var host = ""
    @State private var port = "5000"
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
                }
            }
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

    private func save() {
        let portNum = Int(port) ?? 5000
        let conn: ServerConnection
        if var existing {
            existing.name = name
            existing.host = host
            existing.port = portNum
            conn = existing
        } else {
            conn = ServerConnection(name: name, host: host, port: portNum)
        }

        isVerifying = true
        pendingConn = conn

        Task {
            defer { isVerifying = false }
            let client = APIClient(baseURL: conn.baseURL)
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
