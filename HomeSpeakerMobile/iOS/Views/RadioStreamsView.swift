import SwiftUI

struct RadioStreamsView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var streams: [RadioStream] = []
    @State private var isLoading = false
    @State private var showAdd = false
    @State private var editingStream: RadioStream?
    @State private var actionMessage: String?

    var body: some View {
        NavigationStack {
            Group {
                if isLoading && streams.isEmpty {
                    ProgressView("Loading streams…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if streams.isEmpty {
                    ContentUnavailableView {
                        Label("No Radio Streams", systemImage: "radio")
                    } description: {
                        Text("Add internet radio streams to listen to.")
                    }
                } else {
                    streamList
                }
            }
            .navigationTitle("Radio")
            .toolbar {
                ToolbarItem(placement: .primaryAction) {
                    Button { showAdd = true } label: {
                        Image(systemName: "plus")
                    }
                }
            }
            .refreshable { await load() }
            .task { await load() }
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
            .sheet(isPresented: $showAdd) {
                AddEditStreamView { await load() }
            }
            .sheet(item: $editingStream) { stream in
                AddEditStreamView(existing: stream) { await load() }
            }
        }
    }

    private var streamList: some View {
        List {
            ForEach(streams) { stream in
                HStack(spacing: 12) {
                    faviconView(stream: stream)
                    VStack(alignment: .leading, spacing: 2) {
                        Text(stream.name)
                            .font(.body)
                            .lineLimit(1)
                        Text("\(stream.playCount) plays")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    Spacer()
                    Button {
                        Task { await play(stream: stream) }
                    } label: {
                        Image(systemName: "play.fill")
                            .foregroundStyle(Color.accentColor)
                    }
                    .buttonStyle(.borderless)
                }
                .swipeActions(edge: .trailing) {
                    Button(role: .destructive) {
                        Task { await delete(stream: stream) }
                    } label: {
                        Label("Delete", systemImage: "trash")
                    }
                    Button {
                        editingStream = stream
                    } label: {
                        Label("Edit", systemImage: "pencil")
                    }
                    .tint(.orange)
                }
            }
        }
    }

    @ViewBuilder
    private func faviconView(stream: RadioStream) -> some View {
        if let fileName = stream.faviconFileName, let api = store.api {
            AsyncImage(url: api.faviconURL(for: fileName)) { image in
                image.resizable().scaledToFit()
            } placeholder: {
                Image(systemName: "radio")
                    .foregroundStyle(.secondary)
            }
            .frame(width: 36, height: 36)
            .clipShape(RoundedRectangle(cornerRadius: 6))
        } else {
            Image(systemName: "radio")
                .foregroundStyle(.secondary)
                .frame(width: 36, height: 36)
        }
    }

    private func load() async {
        guard let api = store.api else { return }
        isLoading = true
        defer { isLoading = false }
        streams = (try? await api.getRadioStreams()) ?? []
    }

    private func play(stream: RadioStream) async {
        guard let api = store.api else { return }
        try? await api.playRadioStream(id: stream.id)
        showMessage("Playing: \(stream.name)")
    }

    private func delete(stream: RadioStream) async {
        guard let api = store.api else { return }
        try? await api.deleteRadioStream(id: stream.id)
        await load()
    }

    private func showMessage(_ msg: String) {
        withAnimation { actionMessage = msg }
        Task {
            try? await Task.sleep(for: .seconds(2))
            withAnimation { actionMessage = nil }
        }
    }
}

struct AddEditStreamView: View {
    var existing: RadioStream?
    let onSaved: () async -> Void

    @Environment(ConnectionStore.self) private var store
    @Environment(\.dismiss) private var dismiss

    @State private var name = ""
    @State private var url = ""
    @State private var isSaving = false
    @State private var error: String?

    var isEditing: Bool { existing != nil }

    var body: some View {
        NavigationStack {
            Form {
                Section("Stream Details") {
                    TextField("Name", text: $name)
                        .textInputAutocapitalization(.words)
                    TextField("Stream URL", text: $url)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .keyboardType(.URL)
                }
                if let error {
                    Section {
                        Text(error)
                            .foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle(isEditing ? "Edit Stream" : "Add Stream")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    if isSaving {
                        ProgressView()
                    } else {
                        Button("Save") { Task { await save() } }
                            .disabled(name.isEmpty || url.isEmpty)
                    }
                }
            }
            .onAppear {
                if let existing {
                    name = existing.name
                    url = existing.url
                }
            }
        }
    }

    private func save() async {
        guard let api = store.api else { return }
        isSaving = true
        do {
            if let existing {
                _ = try await api.updateRadioStream(id: existing.id, name: name, url: url)
            } else {
                _ = try await api.createRadioStream(name: name, url: url)
            }
            await onSaved()
            dismiss()
        } catch {
            self.error = error.localizedDescription
        }
        isSaving = false
    }
}
