import SwiftUI

struct WatchServerPickerView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var showAdd = false
    @State private var addHost = ""
    @State private var addPort = "5000"
    @State private var addName = ""

    var body: some View {
        NavigationStack {
            List {
                if store.connections.isEmpty {
                    Text("No servers")
                        .foregroundStyle(.secondary)
                } else {
                    ForEach(store.connections, id: \.id) { (conn: ServerConnection) in
                        Button {
                            store.select(conn)
                        } label: {
                            HStack {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(conn.name)
                                        .font(.caption)
                                    Text(conn.host)
                                        .font(.caption2)
                                        .foregroundStyle(.secondary)
                                }
                                Spacer()
                                if store.selectedConnection?.id == conn.id {
                                    Image(systemName: "checkmark")
                                        .foregroundStyle(Color.accentColor)
                                        .imageScale(.small)
                                }
                            }
                        }
                        .swipeActions {
                            Button(role: .destructive) {
                                store.remove(conn)
                            } label: {
                                Image(systemName: "trash")
                            }
                        }
                    }
                }

                Button("Add Server") {
                    showAdd = true
                }
                .foregroundStyle(Color.accentColor)
            }
            .navigationTitle("Servers")
            .sheet(isPresented: $showAdd) {
                addServerSheet
            }
        }
    }

    private var addServerSheet: some View {
        ScrollView {
            VStack(spacing: 8) {
                Text("Add Server")
                    .font(.headline)

                TextField("Name", text: $addName)

                TextField("Host", text: $addHost)
                    .autocorrectionDisabled()

                TextField("Port", text: $addPort)

                Button("Add") {
                    guard !addName.isEmpty, !addHost.isEmpty else { return }
                    store.add(ServerConnection(
                        name: addName,
                        host: addHost,
                        port: Int(addPort) ?? 5000
                    ))
                    showAdd = false
                    addName = ""
                    addHost = ""
                    addPort = "5000"
                }
                .buttonStyle(.borderedProminent)
                .disabled(addName.isEmpty || addHost.isEmpty)

                Button("Cancel") { showAdd = false }
                    .foregroundStyle(.secondary)
            }
            .padding()
        }
    }
}
