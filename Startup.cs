// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.ApplicationInsights;
using Microsoft.Bot.Builder.Integration.ApplicationInsights.Core;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.BotBuilderSamples.Bots;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; // Adicione esta linha
using Microsoft.BotBuilderSamples.Services; // Adicione esta linha se RAGService estiver em Services

namespace Microsoft.BotBuilderSamples
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient().AddControllers().AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.MaxDepth = HttpHelper.BotMessageSerializerSettings.MaxDepth;
            });

            services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
            
            services.AddApplicationInsightsTelemetry();
            services.AddSingleton<IBotTelemetryClient, BotTelemetryClient>();
            
            // Adicionando armazenamento em memória para o estado da conversa e do usuário.
            services.AddSingleton<IStorage, MemoryStorage>();
            services.AddSingleton<ConversationState>();
            services.AddSingleton<UserState>();

            // Registra o RAGService de forma simples. O sistema irá injetar as dependências automaticamente.
            services.AddSingleton<RAGService>();

            // Registra os diálogos que você criou
            services.AddSingleton<DateResolverDialog>();
            services.AddSingleton<FlightBookingDialog>();
            services.AddSingleton<RootDialog>();
            
            // O bot principal que roda os diálogos
            services.AddTransient<IBot, DialogBot<RootDialog>>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseWebSockets()
                .UseRouting()
                .UseAuthorization()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
        }
    }
}