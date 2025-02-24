# HomeSpeaker

## Running Locally
- Install ffmpeg `winget install gyan.ffmpeg.shared`
- Run the following command to run the Aspire Dashboard in a container
   `docker run --rm -it -p 18888:18888 -p 4317:18889 -p 4318:18890 -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true -e DASHBOARD__OTLP__CORS__ALLOWEDORIGINS=http://localhost:5028 --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:9.0`
- Make sure you're running the `https` profile for the HomeSpeaker.Server2 project

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
