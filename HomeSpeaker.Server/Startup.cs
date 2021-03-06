﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HomeSpeaker.Server.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeSpeaker.Server
{
    public class ConfigKeys
    {
        public const string MediaFolder = "MediaFolder";
    }

    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc();
            services.AddGrpcReflection();

            services.AddSingleton<IDataStore, OnDiskDataStore>();
            services.AddSingleton<IFileSource>(services => new DefaultFileSource(Configuration[ConfigKeys.MediaFolder]));
            services.AddSingleton<ITagParser, DefaultTagParser>();
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                services.AddSingleton<IMusicPlayer, WindowsMusicPlayer>();
            else
                services.AddSingleton<IMusicPlayer, LinuxSoxMusicPlayer>();
            services.AddSingleton<Mp3Library>();
            services.AddSingleton<LifecycleEvents>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger, IHostApplicationLifetime lifetime)
        {
            var events = app.ApplicationServices.GetService<LifecycleEvents>();
            lifetime.ApplicationStopping.Register(events.ApplicationStopping);
            lifetime.ApplicationStarted.Register(events.ApplicationStarted);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<GreeterService>();
                endpoints.MapGrpcService<HomeSpeakerService>();

                endpoints.MapGrpcReflectionService();

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });

            var library = app.ApplicationServices.GetService<Mp3Library>();
            logger.LogInformation($"Sync Started? {library.SyncStarted}; Sync Completed? {library.SyncCompleted}");
        }
    }
}
