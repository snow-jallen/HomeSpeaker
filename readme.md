# HomeSpeaker

## Features

### AI Auto-Playlists
HomeSpeaker now includes AI-powered automatic playlist generation! Using OpenAI's GPT model, the system can analyze your music library and automatically categorize songs into genre-based playlists such as:
- Peaceful Instrumental
- Upbeat
- Classical
- Rock
- Jazz
- Electronic
- Folk
- Worship
- Children's Music
- Holiday
- And more...

Songs can belong to multiple genres, providing flexible playlist options.

## Running Locally
- Install ffmpeg `winget install gyan.ffmpeg.shared`
- Run the following command to run the Aspire Dashboard in a container
   `docker run --rm -it -p 18888:18888 -p 4317:18889 -p 4318:18890 -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true -e DASHBOARD__OTLP__CORS__ALLOWEDORIGINS=http://localhost:5028 --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:9.0`
- Make sure you're running the `https` profile for the HomeSpeaker.Server2 project

## Configuration

### OpenAI API Key Setup
To use the AI auto-playlists feature:

1. Get an API key from [OpenAI Platform](https://platform.openai.com/)
2. For local development:
   - Copy `.env.example` to `.env`
   - Set `OPENAI_API_KEY=your_actual_api_key`
3. For production deployment:
   - Add `OPENAI_API_KEY` to your GitHub repository secrets
   - The deployment workflow will automatically inject it during deployment

**Note:** The API key is never committed to the repository and is passed securely via environment variables.

## Deployment Notes

You have to create a certificate on the host machine

```bash
dotnet dev-certs https -ep %USERPROFILE%\.aspnet\https\aspnetapp.pfx -p $CREDENTIAL_PLACEHOLDER$
dotnet dev-certs https --trust
```

Then in the docker compose you can map a volume to that dir and set the password as an environment variable.

## Deploying

To have GitHub Actions deploy a new version, create a new tag

```bash
git tag -a yyyy.m.d -m yyyy.m.d
```

Then push those tags

```bash
git push --tags
```  

Then a new version will be deployed on the self-hosted runner.
