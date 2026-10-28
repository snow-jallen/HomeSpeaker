import SwiftUI

struct MoreView: View {
    @Environment(ConnectionStore.self) private var store
    @Environment(OfflineDownloadsStore.self) private var offlineDownloads
    @Environment(PushNotificationStore.self) private var pushNotifications
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

                offlineSection

                notificationsSection

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

    private var notificationsSection: some View {
        Section("Notifications") {
            NavigationLink {
                PushNotificationsView()
            } label: {
                HStack {
                    Label("Push Alerts", systemImage: "bell.badge")
                    Spacer()
                    Text(pushNotifications.summaryLine)
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
        let outsideName = temp.outsideRoomName
        let rooms = temp.rooms ?? []
        // Outside first: the room the server names as outside, else the bare
        // outside reading. Then every other room in the server's order.
        if let outsideName, let outsideRoom = rooms.first(where: { $0.name == outsideName }) {
            tempRow(outsideRoom.name, value: outsideRoom.temperature)
        } else if outsideName != nil || temp.outsideTemperature != nil {
            tempRow(outsideName ?? "Outside", value: temp.outsideTemperature)
        }
        ForEach(Array(rooms.enumerated()), id: \.offset) { _, room in
            if room.name != outsideName {
                tempRow(room.name, value: room.temperature)
            }
        }
        if let close = temp.shouldWindowsBeClosed, temp.indoorTemperature != nil || temp.rooms == nil {
            HStack {
                Image(systemName: close ? "window.closed" : "window.open.slash")
                    .foregroundStyle(close ? .orange : .green)
                Text(close ? "Close windows" : "Windows OK")
            }
        }
    }

    /// `value` is °F from the server; shown in the server's unit, or "–" when unread.
    private func tempRow(_ label: String, value: Double?) -> some View {
        HStack {
            Text(label)
            Spacer()
            Text(formattedTemperature(value))
                .foregroundStyle(.secondary)
                .monospacedDigit()
        }
    }

    private func formattedTemperature(_ fahrenheit: Double?) -> String {
        guard let fahrenheit else { return "–" }
        let value = features?.displayTemperature(fahrenheit) ?? fahrenheit
        let symbol = features?.temperatureUnitSymbol ?? "°F"
        return String(format: "%.1f", value) + symbol
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
            NavigationLink {
                SiriCommandsView()
            } label: {
                Label("Siri Commands", systemImage: "mic.fill")
            }
            NavigationLink {
                SiriCommandHistoryView()
            } label: {
                Label("Siri Command History", systemImage: "clock.arrow.circlepath")
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
