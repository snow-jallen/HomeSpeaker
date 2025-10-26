using Foundation;
using Intents;
using HomeSpeaker.MauiApp.Platforms.iOS.Intents;

namespace HomeSpeaker.MauiApp;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool ContinueUserActivity(UIKit.UIApplication application, NSUserActivity userActivity, UIKit.UIApplicationRestorationHandler completionHandler)
    {
        if (userActivity.ActivityType == "INPlayPlaylistIntent")
        {
            var interaction = userActivity.GetInteraction();
            if (interaction?.Intent is INPlayPlaylistIntent intent)
            {
                var handler = new PlayPlaylistIntentHandler(IntPtr.Zero);
                handler.HandlePlayPlaylist(intent, response =>
                {
                    // Intent handled
                });
                return true;
            }
        }
        
        return base.ContinueUserActivity(application, userActivity, completionHandler);
    }
}
