import SwiftUI

struct MoreView: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(OfflineDownloadsStore.self) private var offlineDownloads
    @State private var features: Features?
    @State private var temperature: TemperatureStatus?
    @State private var bloodSugar: BloodSugarStatus?
    @State private var showServerList = false

    var body: some View {
        NavigationStack {
            List {
                if features?.bloodSugarEnabled == true {
                    bloodSugarSection
                }

                serverSection

                radioSection

                youtubeSection

                aiSection

                offlineSection

                if features?.temperatureEnabled == true {
                    temperatureSection
                }

                settingsSection
            }
            .navigationTitle("More")
            .refreshable { await loadAll() }
            .task { await loadAll() }
            .sheet(isPresented: $showServerList) {
                ServerPickerSheet()
            }
        }
    }

    // MARK: - Sections

    private var serverSection: some View {
        Section("Active Server") {
            Button {
                showServerList = true
            } label: {
                HStack {
                    Label(store.selectedConnection?.name ?? "No server", systemImage: "hifispeaker.2")
                    Spacer()
                    if let conn = store.selectedConnection {
                        Text("\(conn.host):\(conn.port)")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    Image(systemName: "chevron.right")
                        .font(.caption)
                        .foregroundStyle(.tertiary)
                }
            }
            .foregroundStyle(.primary)
        }
    }

    private var radioSection: some View {
        Section {
            NavigationLink {
                RadioStreamsView()
            } label: {
                Label("Internet Radio", systemImage: "radio")
            }
        }
    }

    private var youtubeSection: some View {
        Section {
            NavigationLink {
                YouTubeView()
            } label: {
                Label("YouTube", systemImage: "play.rectangle")
            }
        }
    }

    private var aiSection: some View {
        Section("AI") {
            NavigationLink {
                AIPlaylistsView()
            } label: {
                Label("AI Playlists", systemImage: "sparkles")
            }
            NavigationLink {
                AIStatusView()
            } label: {
                Label("AI Status", systemImage: "cpu")
            }
        }
    }

    private var offlineSection: some View {
        Section("Offline") {
            NavigationLink {
                OfflineDownloadsView()
            } label: {
                HStack {
                    Label("Offline Downloads", systemImage: "arrow.down.circle")
                    Spacer()
                    Text(offlineDownloads.summaryLine)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
        }
    }

    private var temperatureSection: some View {
        Section("Temperature") {
            if let temp = temperature {
                temperatureRows(temp)
            } else {
                ProgressView()
            }
        }
    }

    @ViewBuilder
    private func temperatureRows(_ temp: TemperatureStatus) -> some View {
        if let outside = temp.outsideTemperature {
            tempRow("Outside", value: outside)
        }
        if let greenhouse = temp.greenhouseTemperature {
            tempRow("Greenhouse", value: greenhouse)
        }
        if let momDad = temp.momAndDadsRoomTemperature {
            tempRow("Primary Bedroom", value: momDad)
        }
        if let boys = temp.boysRoomTemperature {
            tempRow("Boys Room", value: boys)
        }
        if let older = temp.olderGirlsRoomTemperature {
            tempRow("Older Girls Room", value: older)
        }
        if let younger = temp.youngerGirlsRoomTemperature {
            tempRow("Younger Girls Room", value: younger)
        }
        if let close = temp.shouldWindowsBeClosed {
            HStack {
                Image(systemName: close ? "window.closed" : "window.open.slash")
                    .foregroundStyle(close ? .orange : .green)
                Text(close ? "Close windows" : "Windows OK")
            }
        }
    }

    private func tempRow(_ label: String, value: Double) -> some View {
        HStack {
            Text(label)
            Spacer()
            Text(String(format: "%.1f°F", value))
                .foregroundStyle(.secondary)
                .monospacedDigit()
        }
    }

    private var bloodSugarSection: some View {
        Section("Blood Sugar") {
            if let bs = bloodSugar, let reading = bs.currentReading {
                HStack {
                    Text(bs.directionArrow)
                        .font(.title)
                    VStack(alignment: .leading) {
                        if let sgv = reading.sgv {
                            Text(String(format: "%.0f mg/dL", sgv))
                                .font(.title2.bold())
                        }
                        if bs.isStale == true {
                            Text("Stale reading")
                                .font(.caption)
                                .foregroundStyle(.orange)
                        }
                    }
                }
            } else {
                ProgressView()
            }
        }
    }

    private var settingsSection: some View {
        Section("Settings") {
            NavigationLink {
                ServerListView()
            } label: {
                Label("Manage Servers", systemImage: "server.rack")
            }
        }
    }

    // MARK: - Data Loading

    private func loadAll() async {
        guard let api = store.api else { return }

        // Features is a quick local-config lookup that tells us which optional
        // widgets to show.
        features = try? await api.getFeatures()
        let bloodSugarEnabled = features?.bloodSugarEnabled == true
        let temperatureEnabled = features?.temperatureEnabled == true

        // Load the optional widgets concurrently so a slow or unreachable
        // endpoint (each has a 10s timeout) can't block the others.
        async let bloodSugarResult: BloodSugarStatus? =
            bloodSugarEnabled ? (try? await api.getBloodSugar()) : nil
        async let temperatureResult: TemperatureStatus? =
            temperatureEnabled ? (try? await api.getTemperature()) : nil

        bloodSugar = await bloodSugarResult
        temperature = await temperatureResult
    }
}
