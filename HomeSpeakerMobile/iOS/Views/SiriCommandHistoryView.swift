import SwiftUI

/// Every Siri command the app handled, newest first: what Siri captured, what the app made of
/// it, and what the server did.
struct SiriCommandHistoryView: View {
    @State private var entries: [SiriCommandLog.Entry] = []
    @State private var confirmingClear = false

    var body: some View {
        List {
            ForEach(entries) { entry in
                VStack(alignment: .leading, spacing: 6) {
                    HStack(alignment: .firstTextBaseline) {
                        Text(entry.command)
                            .font(.headline)
                        Spacer()
                        Text(entry.date, format: .dateTime.month().day().hour().minute().second())
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    detail("Siri captured", entry.captured, systemImage: "waveform")
                    detail("Translated to", entry.translated, systemImage: "arrow.right")
                    detail("Server", entry.result, systemImage: "hifispeaker")
                }
                .padding(.vertical, 2)
                .textSelection(.enabled)
            }
        }
        .overlay {
            if entries.isEmpty {
                ContentUnavailableView(
                    "No Siri Commands Yet",
                    systemImage: "mic",
                    description: Text("Commands you give Siri for Home Speaker show up here.")
                )
            }
        }
        .navigationTitle("Siri Command History")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            if !entries.isEmpty {
                Button("Clear", role: .destructive) { confirmingClear = true }
            }
        }
        .confirmationDialog("Clear the Siri command history?", isPresented: $confirmingClear, titleVisibility: .visible) {
            Button("Clear History", role: .destructive) {
                SiriCommandLog.clear()
                entries = []
            }
        }
        .onAppear { entries = SiriCommandLog.entries() }
        .refreshable { entries = SiriCommandLog.entries() }
    }

    private func detail(_ label: String, _ value: String, systemImage: String) -> some View {
        Label {
            VStack(alignment: .leading, spacing: 1) {
                Text(label)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text(value)
                    .font(.subheadline)
            }
        } icon: {
            Image(systemName: systemImage)
                .foregroundStyle(.secondary)
        }
    }
}
