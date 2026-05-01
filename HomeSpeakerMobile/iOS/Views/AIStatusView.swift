import SwiftUI

struct AIStatusView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var status: AiLibraryStatusDto?
    @State private var isLoading = false
    @State private var error: String?
    @State private var isResuming = false
    
    private var pollTimer = Timer.publish(every: 3, on: .main, in: .common).autoconnect()
    
    var body: some View {
        NavigationStack {
            Group {
                if isLoading && status == nil {
                    ProgressView("Loading status…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if let error {
                    errorView(error: error)
                } else if let status {
                    statusContent(status: status)
                } else {
                    ContentUnavailableView {
                        Label("Status Unavailable", systemImage: "questionmark.circle")
                    } description: {
                        Text("Could not load AI status.")
                    }
                }
            }
            .navigationTitle("AI Status")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        Task { await refresh() }
                    } label: {
                        Image(systemName: "arrow.clockwise")
                    }
                    .disabled(isLoading)
                }
            }
            .task { await load() }
            .refreshable { await load() }
            .onReceive(pollTimer) { _ in
                if status?.isProcessing == true {
                    Task { await refresh() }
                }
            }
        }
    }
    
    private func errorView(error: String) -> some View {
        ContentUnavailableView {
            Label("Load Failed", systemImage: "exclamationmark.triangle")
        } description: {
            Text(error)
        } actions: {
            Button("Try Again") {
                Task { await load() }
            }
        }
    }
    
    private func statusContent(status: AiLibraryStatusDto) -> some View {
        List {
            Section("Processing Status") {
                HStack {
                    Text("State")
                    Spacer()
                    HStack(spacing: 6) {
                        if status.isProcessing {
                            ProgressView()
                                .scaleEffect(0.8)
                        }
                        Text(status.stateDisplay)
                            .foregroundStyle(status.isProcessing ? Color.accentColor : .secondary)
                    }
                }
                
                if status.totalTracks > 0 {
                    VStack(alignment: .leading, spacing: 8) {
                        HStack {
                            Text("Progress")
                            Spacer()
                            Text(String(format: "%.1f%%", status.percentComplete * 100))
                                .foregroundStyle(.secondary)
                                .monospacedDigit()
                        }
                        ProgressView(value: status.percentComplete)
                            .tint(Color.accentColor)
                    }
                }
            }
            
            Section("Track Counts") {
                countRow(label: "Total", value: status.totalTracks)
                countRow(label: "Completed", value: status.completedTracks, color: .green)
                countRow(label: "Queued", value: status.queuedTracks, color: .blue)
                countRow(label: "Processing", value: status.processingTracks, color: .orange)
                if status.failedTracks > 0 {
                    countRow(label: "Failed", value: status.failedTracks, color: .red)
                }
            }
            
            if let lastScan = status.lastScanUtc {
                Section("Last Scan") {
                    Text(formatTimestamp(lastScan))
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            
            if let batchId = status.currentBatchId {
                Section("Current Batch") {
                    Text(batchId)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            
            Section {
                Button {
                    Task { await resumeProcessing() }
                } label: {
                    HStack {
                        if isResuming {
                            ProgressView()
                                .scaleEffect(0.8)
                        }
                        Text(status.isProcessing ? "Processing Running" : "Resume Processing")
                    }
                }
                .disabled(status.isProcessing || isResuming)
            } footer: {
                Text("Start or resume AI analysis of your music library. Analysis runs in the background and can take some time for large libraries.")
                    .font(.caption)
            }
        }
    }
    
    private func countRow(label: String, value: Int, color: Color? = nil) -> some View {
        HStack {
            Text(label)
            Spacer()
            Text("\(value)")
                .foregroundStyle(color ?? .secondary)
                .monospacedDigit()
        }
    }
    
    private func formatTimestamp(_ iso: String) -> String {
        let formatter = ISO8601DateFormatter()
        guard let date = formatter.date(from: iso) else {
            return iso
        }
        
        let relativeFormatter = RelativeDateTimeFormatter()
        relativeFormatter.unitsStyle = .full
        return relativeFormatter.localizedString(for: date, relativeTo: Date())
    }
    
    private func load() async {
        guard let api = store.api else { return }
        isLoading = true
        error = nil
        defer { isLoading = false }
        
        do {
            status = try await api.getAiStatus()
        } catch {
            self.error = error.localizedDescription
        }
    }
    
    private func refresh() async {
        guard let api = store.api else { return }
        status = try? await api.getAiStatus()
    }
    
    private func resumeProcessing() async {
        guard let api = store.api else { return }
        isResuming = true
        defer { isResuming = false }
        
        try? await api.resumeAiProcessing()
        try? await Task.sleep(for: .seconds(1))
        await refresh()
    }
}
