import SwiftUI

/// Edit one person's anchors: add, rename, move between slots, choose days and a target time,
/// reorder, retire and restore. Anchors from another app are listed read-only.
struct AnchorEditView: View {
    let personId: String

    @Environment(ConnectionStore.self) private var store
    @State private var person: AnchorPerson?
    @State private var newTitle = ""
    @State private var newSlot = "day"
    @State private var editing: Anchor?
    @State private var error: String?

    var body: some View {
        List {
            if let error {
                Section { Text(error).foregroundStyle(.red) }
            }

            Section("Add an anchor") {
                TextField("e.g. Read scriptures", text: $newTitle)
                Picker("When", selection: $newSlot) {
                    ForEach(AnchorSlots.all, id: \.self) { Text(AnchorSlots.label($0)).tag($0) }
                }
                Button("Add") { Task { await add() } }
                    .disabled(newTitle.trimmingCharacters(in: .whitespaces).isEmpty)
            }

            if let person {
                ForEach(AnchorSlots.grouped(person.anchors.filter { !$0.retired }), id: \.slot) { group in
                    Section(AnchorSlots.label(group.slot)) {
                        ForEach(group.anchors) { anchor in
                            Button {
                                if anchor.editable { editing = anchor }
                            } label: {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(anchor.title).foregroundStyle(.primary)
                                    Text(describe(anchor)).font(.caption).foregroundStyle(.secondary)
                                }
                            }
                            .disabled(!anchor.editable)
                        }
                        .onMove { from, to in
                            Task { await reorder(group.anchors, from: from, to: to) }
                        }
                    }
                }

                let retired = person.anchors.filter { $0.retired && $0.editable }
                if !retired.isEmpty {
                    Section("Retired") {
                        ForEach(retired) { anchor in
                            HStack {
                                Text(anchor.title).foregroundStyle(.secondary)
                                Spacer()
                                Button("Restore") { Task { await run { try await $0.restoreAnchor(id: anchor.id) } } }
                            }
                        }
                    }
                }
            }
        }
        .navigationTitle(person.map { "\($0.name)'s anchors" } ?? "Edit anchors")
        .toolbar { EditButton() }
        .task { await load() }
        .sheet(item: $editing) { anchor in
            AnchorFormView(anchor: anchor) { request in
                editing = nil
                if let request {
                    Task { await run { try await $0.updateAnchor(id: anchor.id, request) } }
                }
            } onRetire: {
                editing = nil
                Task { await run { try await $0.retireAnchor(id: anchor.id) } }
            }
        }
    }

    private func describe(_ anchor: Anchor) -> String {
        var parts = [AnchorDays.label(anchor.days)]
        if let target = anchor.targetTime { parts.append("by " + AnchorTime.display(target)) }
        if !anchor.editable { parts.append("edit it in the app it comes from") }
        return parts.joined(separator: " · ")
    }

    private func load() async {
        guard let api = store.api else { return }
        do {
            person = try await api.getAnchorDay(personId: personId)
            error = nil
        } catch {
            self.error = "Couldn't load anchors: \(error.localizedDescription)"
        }
    }

    private func add() async {
        let title = newTitle.trimmingCharacters(in: .whitespaces)
        await run { _ = try await $0.createAnchor(personId: personId, AnchorRequest(title: title, slot: newSlot)) }
        if error == nil { newTitle = "" }
    }

    private func reorder(_ inSlot: [Anchor], from: IndexSet, to: Int) async {
        guard let person else { return }
        var moved = inSlot
        moved.move(fromOffsets: from, toOffset: to)
        // The server keeps one order per person; slots sort first, so send every slot in display order.
        let others = AnchorSlots.grouped(person.anchors.filter { $0.editable && $0.slot != inSlot.first?.slot }).flatMap(\.anchors)
        let ids = (moved + others).sorted { AnchorSlots.rank($0.slot) < AnchorSlots.rank($1.slot) }.map(\.id)
        await run { try await $0.orderAnchors(personId: personId, ids: ids) }
    }

    private func run(_ action: (APIClient) async throws -> Void) async {
        guard let api = store.api else { return }
        do {
            try await action(api)
            error = nil
        } catch {
            self.error = "That didn't save: \(error.localizedDescription)"
        }
        await load()
    }
}

/// The form for one anchor. Day changes apply from today on; earlier days keep their schedule.
struct AnchorFormView: View {
    let anchor: Anchor
    let onDone: (AnchorRequest?) -> Void
    let onRetire: () -> Void

    @State private var title: String
    @State private var slot: String
    @State private var days: Int
    @State private var hasTarget: Bool
    @State private var target: Date

    init(anchor: Anchor, onDone: @escaping (AnchorRequest?) -> Void, onRetire: @escaping () -> Void) {
        self.anchor = anchor
        self.onDone = onDone
        self.onRetire = onRetire
        _title = State(initialValue: anchor.title)
        _slot = State(initialValue: anchor.slot)
        _days = State(initialValue: anchor.days)
        _hasTarget = State(initialValue: anchor.targetTime != nil)
        _target = State(initialValue: AnchorTime.date(anchor.targetTime) ?? Calendar.current.date(bySettingHour: 7, minute: 0, second: 0, of: .now) ?? .now)
    }

    var body: some View {
        NavigationStack {
            Form {
                TextField("Title", text: $title)
                Picker("When", selection: $slot) {
                    ForEach(AnchorSlots.all, id: \.self) { Text(AnchorSlots.label($0)).tag($0) }
                }
                Section {
                    Toggle("Every day", isOn: Binding(get: { days & AnchorDays.every == AnchorDays.every }, set: { days = $0 ? AnchorDays.every : 62 }))
                    HStack {
                        ForEach(AnchorDays.week, id: \.bit) { day in
                            let on = days & day.bit != 0
                            Button(day.short) {
                                let next = days ^ day.bit
                                if next != 0 { days = next }
                            }
                            .buttonStyle(.bordered)
                            .tint(on ? .accentColor : .secondary)
                            .accessibilityLabel(day.name)
                            .accessibilityAddTraits(on ? .isSelected : [])
                        }
                    }
                } header: {
                    Text("Days")
                } footer: {
                    Text("Changes count from today; earlier days keep the schedule they had.")
                }
                Section {
                    Toggle("Target time", isOn: $hasTarget)
                    if hasTarget {
                        DatePicker("Time", selection: $target, displayedComponents: .hourAndMinute)
                    }
                }
                Section {
                    Button("Retire", role: .destructive, action: onRetire)
                } footer: {
                    Text("Stops tracking it from today. Its history stays.")
                }
            }
            .navigationTitle("Edit anchor")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Cancel") { onDone(nil) } }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        onDone(AnchorRequest(title: title, slot: slot, targetTime: hasTarget ? AnchorTime.wire(target) : "", days: days))
                    }
                    .disabled(title.trimmingCharacters(in: .whitespaces).isEmpty)
                }
            }
        }
    }
}
