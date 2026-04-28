import SwiftUI

struct MoreView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var recentlyPlayed: [Song] = []
    @State private var features: Features?
    @State private var temperature: TemperatureStatus?
    @State private var forecast: ForecastStatus?
    @State private var bloodSugar: BloodSugarStatus?
    @State private var showServerList = false

    var body: some View {
        NavigationStack {
            List {
                serverSection

                recentlyPlayedSection

                radioSection

                youtubeSection

                if features?.temperatureEnabled == true {
                    temperatureSection
                }

                if features?.bloodSugarEnabled == true {
                    bloodSugarSection
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

    private var recentlyPlayedSection: some View {
        Section("Recently Played") {
            if recentlyPlayed.isEmpty {
                Text("No recent plays")
                    .foregroundStyle(.secondary)
            } else {
                ForEach(Array(recentlyPlayed.prefix(5))) { (song: Song) in
                    HStack {
                        Image(systemName: "music.note")
                            .foregroundStyle(.secondary)
                            .frame(width: 20)
                        VStack(alignment: .leading, spacing: 2) {
                            Text(song.displayTitle)
                                .lineLimit(1)
                            Text(song.displayArtist)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                                .lineLimit(1)
                        }
                        Spacer()
                        Button {
                            Task {
                                guard let api = store.api else { return }
                                try? await api.playSong(song.songId)
                            }
                        } label: {
                            Image(systemName: "play.fill")
                                .foregroundStyle(Color.accentColor)
                        }
                        .buttonStyle(.borderless)
                    }
                }
            }
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
        async let recentTask = api.getRecentlyPlayed(limit: 5)
        async let featuresTask = api.getFeatures()
        recentlyPlayed = (try? await recentTask) ?? []
        features = try? await featuresTask

        if features?.temperatureEnabled == true {
            temperature = try? await api.getTemperature()
            forecast = try? await api.getForecast()
        }
        if features?.bloodSugarEnabled == true {
            bloodSugar = try? await api.getBloodSugar()
        }
    }
}
