import SwiftUI

/// "Who are you?": picks the person this phone belongs to on the current server.
struct AnchorWhoView: View {
    let people: [AnchorPerson]
    let selected: String?
    let onPick: (String?) -> Void

    var body: some View {
        NavigationStack {
            List {
                ForEach(people) { person in
                    Button {
                        onPick(person.id)
                    } label: {
                        HStack(spacing: 12) {
                            AnchorPersonMark(person: person, size: 28)
                            Text(person.name).foregroundStyle(.primary)
                            Spacer()
                            if person.id == selected {
                                Image(systemName: "checkmark").foregroundStyle(.tint)
                            }
                        }
                    }
                }
                if selected != nil {
                    Button("I'm not on this list", role: .destructive) { onPick(nil) }
                }
            }
            .navigationTitle("Who are you?")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { onPick(selected) }
                }
            }
        }
        .presentationDetents([.medium, .large])
    }
}
