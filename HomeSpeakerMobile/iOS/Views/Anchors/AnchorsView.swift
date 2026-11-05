import SwiftUI

/// Anchors: the few things that hold down each person's day. Opens on your own day once
/// you've said who you are on this phone (remembered per server).
struct AnchorsView: View {
    @Environment(ConnectionStore.self) private var store
    @State private var board: AnchorBoard?
    @State private var meId: String?
    @State private var error: String?
    @State private var choosing = false

    private var me: AnchorPerson? {
        guard let meId else { return nil }
        return board?.people.first { $0.id == meId }
    }

    var body: some View {
        List {
            if let error {
                Section { Text(error).foregroundStyle(.red) }
            }
            if let board, board.stale == true || (board.feed.map { $0.status != "ok" } ?? false) {
                Section {
                    Label(staleText(board), systemImage: "wifi.exclamationmark")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
            }
            if let board {
                if let me {
                    Section {
                        NavigationLink {
                            AnchorDayView(personId: me.id)
                        } label: {
                            AnchorPersonRow(person: me, today: board.today, prominent: true)
                        }
                    } header: {
                        Text("You")
                    }
                } else {
                    Section {
                        Button("Who are you?") { choosing = true }
                    } footer: {
                        Text("Pick yourself once and Anchors opens on your own day.")
                    }
                }

                Section("Everyone") {
                    ForEach(board.people.filter { $0.id != meId }) { person in
                        NavigationLink {
                            AnchorDayView(personId: person.id)
                        } label: {
                            AnchorPersonRow(person: person, today: board.today, prominent: false)
                        }
                    }
                    if board.people.isEmpty {
                        Text("No one has anchors yet. Add people on the HomeSpeaker web page (Anchors › People).")
                            .foregroundStyle(.secondary)
                    }
                }
            } else if error == nil {
                ProgressView()
            }
        }
        .navigationTitle("Anchors")
        .toolbar {
            if board != nil {
                Button("Not you?") { choosing = true }
            }
        }
        .refreshable { await load() }
        .task { await load() }
        .sheet(isPresented: $choosing) {
            AnchorWhoView(people: (board?.people ?? []).filter(\.isLocal), selected: meId) { picked in
                if let serverId = store.selectedConnection?.id {
                    AnchorMe.set(picked, serverId: serverId)
                }
                meId = picked
                choosing = false
            }
        }
    }

    private func staleText(_ board: AnchorBoard) -> String {
        if board.stale == true { return "The speaker that keeps your anchors isn't answering; showing what it last said." }
        let name = board.feed?.name ?? "The other app"
        return board.feed?.status == "stale"
            ? "\(name) isn't answering; showing what it last said."
            : "\(name) isn't answering, so its anchors are missing."
    }

    private func load() async {
        guard let api = store.api else { return }
        if let serverId = store.selectedConnection?.id {
            meId = AnchorMe.get(serverId: serverId)
        }
        do {
            board = try await api.getAnchorBoard()
            error = nil
        } catch {
            self.error = "Couldn't load anchors: \(error.localizedDescription)"
        }
    }
}

/// A person's name, colour and how today is going.
struct AnchorPersonRow: View {
    let person: AnchorPerson
    let today: String
    let prominent: Bool

    var body: some View {
        HStack(spacing: 12) {
            AnchorPersonMark(person: person, size: prominent ? 36 : 26)
            VStack(alignment: .leading, spacing: 2) {
                Text(person.name).font(prominent ? .title3.bold() : .body.weight(.semibold))
                if person.streak > 0 {
                    Text(person.streak == 1 ? "1 anchored day" : "\(person.streak) anchored days in a row")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            Spacer()
            HStack(spacing: 4) {
                ForEach(todayAnchors, id: \.id) { anchor in
                    Circle()
                        .strokeBorder(AnchorColor.of(person.color), lineWidth: 2)
                        .background(Circle().fill(anchor.cell(on: today)?.isKept == true ? AnchorColor.of(person.color) : .clear))
                        .frame(width: 10, height: 10)
                }
            }
            Text("\(person.todayDone)/\(person.todayTotal)")
                .font(.caption.monospacedDigit())
                .foregroundStyle(.secondary)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(person.name), \(person.todayDone) of \(person.todayTotal) anchors done today")
    }

    private var todayAnchors: [Anchor] {
        AnchorSlots.grouped(person.anchors).flatMap(\.anchors).filter {
            let state = $0.cell(on: today)?.state
            return state == "open" || state == "kept"
        }
    }
}

struct AnchorPersonMark: View {
    let person: AnchorPerson
    let size: CGFloat

    var body: some View {
        ZStack {
            Circle().fill(AnchorColor.of(person.color))
            if let emoji = person.emoji, !emoji.isEmpty {
                Text(emoji).font(.system(size: size * 0.55))
            }
        }
        .frame(width: size, height: size)
    }
}
