import SwiftUI

/// One person's anchors for a day, grouped Morning / Day / Evening. One tap marks one done.
struct AnchorDayView: View {
    let personId: String

    @Environment(ConnectionStore.self) private var store
    @State private var person: AnchorPerson?
    @State private var date: String?
    @State private var today: String?
    @State private var busy: Set<String> = []
    @State private var error: String?
    @State private var loaded = false

    var body: some View {
        List {
            if let error {
                Section { Text(error).foregroundStyle(.red) }
            }
            if let person, let date {
                if person.streak > 0 {
                    Section {
                        Label(person.streak == 1 ? "1 anchored day" : "\(person.streak) anchored days in a row", systemImage: "flame")
                            .foregroundStyle(.secondary)
                    }
                }
                let visible = person.anchors.filter { ($0.cell(on: date)?.state ?? "none") != "none" }
                if visible.isEmpty {
                    Section {
                        Text(person.anchors.isEmpty ? "\(person.name) has no anchors yet." : "Nothing to do on this day.")
                            .foregroundStyle(.secondary)
                    }
                }
                ForEach(AnchorSlots.grouped(visible), id: \.slot) { group in
                    Section(AnchorSlots.label(group.slot)) {
                        ForEach(group.anchors) { anchor in
                            AnchorCheckRow(anchor: anchor, cell: anchor.cell(on: date), color: AnchorColor.of(person.color), busy: busy.contains(anchor.id)) {
                                Task { await toggle(anchor, on: date) }
                            }
                        }
                    }
                }
            } else if loaded {
                Text("That person isn't here any more.").foregroundStyle(.secondary)
            } else {
                ProgressView()
            }
        }
        .navigationTitle(person?.name ?? "Anchors")
        .toolbar {
            ToolbarItemGroup(placement: .bottomBar) {
                Button { Task { await move(-1) } } label: { Image(systemName: "chevron.left") }
                    .accessibilityLabel("Previous day")
                Spacer()
                Text(dayLabel).font(.subheadline.weight(.semibold))
                Spacer()
                Button { Task { await move(1) } } label: { Image(systemName: "chevron.right") }
                    .disabled(date == nil || date == today)
                    .accessibilityLabel("Next day")
            }
            if let person, person.isLocal {
                ToolbarItem(placement: .primaryAction) {
                    NavigationLink("Edit") { AnchorEditView(personId: person.id) }
                }
            }
        }
        .refreshable { await load() }
        .task { await load() }
    }

    private var dayLabel: String {
        guard let date, let today else { return "" }
        if date == today { return "Today" }
        if date == Self.shift(today, by: -1) { return "Yesterday" }
        // Parsed as UTC midnight, so format in UTC too or western time zones show the day before.
        var style = Date.FormatStyle.dateTime.weekday(.abbreviated).month(.abbreviated).day()
        style.timeZone = TimeZone(identifier: "UTC")!
        return Self.parse(date)?.formatted(style) ?? date
    }

    private func load() async {
        guard let api = store.api else { return }
        do {
            if today == nil {
                today = try await api.getAnchorBoard().today
                date = today
            }
            person = try await api.getAnchorDay(personId: personId, date: date)
            error = nil
        } catch {
            self.error = "Couldn't load anchors: \(error.localizedDescription)"
        }
        loaded = true
    }

    private func move(_ days: Int) async {
        guard let current = date, let next = Self.shift(current, by: days), let today, next <= today else { return }
        date = next
        await load()
    }

    private func toggle(_ anchor: Anchor, on day: String) async {
        guard let api = store.api, !busy.contains(anchor.id) else { return }
        busy.insert(anchor.id)
        defer { busy.remove(anchor.id) }
        do {
            try await api.setAnchorDone(anchorId: anchor.id, date: day, done: anchor.cell(on: day)?.isKept != true)
            await load()
        } catch {
            self.error = "Couldn't update \u{201C}\(anchor.title)\u{201D}."
        }
    }

    private static func dayFormat() -> DateFormatter {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(identifier: "UTC")
        formatter.dateFormat = "yyyy-MM-dd"
        return formatter
    }

    private static func parse(_ day: String) -> Date? { dayFormat().date(from: day) }

    private static func shift(_ day: String, by days: Int) -> String? {
        guard let date = parse(day) else { return nil }
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!
        return calendar.date(byAdding: .day, value: days, to: date).map { dayFormat().string(from: $0) }
    }
}

/// One anchor as a big checkable row.
struct AnchorCheckRow: View {
    let anchor: Anchor
    let cell: AnchorCell?
    let color: Color
    let busy: Bool
    let onToggle: () -> Void

    var body: some View {
        Button(action: onToggle) {
            HStack(spacing: 14) {
                ZStack {
                    Circle()
                        .strokeBorder(cell?.isKept == true ? color : .secondary, style: StrokeStyle(lineWidth: 2.5, dash: cell?.pending == true ? [4, 3] : []))
                        .background(Circle().fill(cell?.isKept == true ? color : .clear))
                    if cell?.isKept == true {
                        Image(systemName: "checkmark").font(.footnote.bold()).foregroundStyle(.black)
                    }
                }
                .frame(width: 30, height: 30)
                VStack(alignment: .leading, spacing: 2) {
                    Text(anchor.title).font(.body.weight(.semibold)).foregroundStyle(.primary)
                    let meta = AnchorTime.meta(for: anchor, cell: cell)
                    if !meta.isEmpty {
                        Text(meta).font(.caption).foregroundStyle(.secondary)
                    }
                }
                Spacer()
                if busy { ProgressView() }
            }
            .padding(.vertical, 6)
            .contentShape(Rectangle())
        }
        .disabled(!anchor.writable || busy)
        .opacity(cell?.state == "off" ? 0.6 : 1)
        .accessibilityAddTraits(cell?.isKept == true ? .isSelected : [])
        .accessibilityHint(anchor.writable ? "Double-tap to mark \(cell?.isKept == true ? "not done" : "done")" : "Checked off in the app it comes from")
    }
}
