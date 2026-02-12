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
                // Handle the intent directly without creating a new handler instance
                // The intent system will use the registered handler automatically
                System.Diagnostics.Debug.WriteLine($"Handling intent for playlist: {intent.PlaylistName} on server: {intent.ServerNickname}");
                return true;
            }
        }
        
        return base.ContinueUserActivity(application, userActivity, completionHandler);
    }
}
