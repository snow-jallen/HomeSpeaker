# Vendored packages

## YoutubeExplode 6.6.2-pr965

Built from https://github.com/Tyrrrz/YoutubeExplode/pull/965
(fork `mysteryx93/YoutubeExplode`, branch `prime`, commit `1d2f63a`).

YouTube started returning LOGIN_REQUIRED ("Sign in to confirm you're not a
bot") for the ANDROID_VR client that released YoutubeExplode versions
(≤ 6.6.1) use, which makes stream resolution fail with
`VideoUnavailableException` for many public videos (upstream issues #902,
#962, #964). PR 965 switches the primary client to VISIONOS (what yt-dlp
uses) with an ANDROID fallback.

Remove these packages and the `local` source in `NuGet.config` once a fixed
version ships on nuget.org, and point `HomeSpeaker.Server2.csproj` back at
the official release.

To rebuild:

```
git clone --branch prime https://github.com/mysteryx93/YoutubeExplode.git
dotnet pack YoutubeExplode/YoutubeExplode.csproj -c Release -p:Version=6.6.2-pr965 -o out
dotnet pack YoutubeExplode.Converter/YoutubeExplode.Converter.csproj -c Release -p:Version=6.6.2-pr965 -o out
```
