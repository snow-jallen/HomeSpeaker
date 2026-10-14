import SwiftUI
import WidgetKit

@main
struct HomeSpeakerWidgetBundle: WidgetBundle {
    var body: some Widget {
        SpeakerWidget()
        SpeakerLiveActivityWidget()
        if #available(iOS 18.0, *) {
            SpeakerPlaybackControl()
            SpeakerQuietDownControl()
        }
    }
}
