// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.ApplicationInsights.Core;
using Microsoft.Bot.Schema;

namespace Microsoft.BotBuilderSamples
{
    public class AdapterWithErrorHandler : BotFrameworkHttpAdapter
    {
        private readonly IBotTelemetryClient _telemetryClient;

        public AdapterWithErrorHandler(IConfiguration configuration, ILogger<BotFrameworkHttpAdapter> logger, IBotTelemetryClient telemetryClient)
            : base(configuration, logger)
        {
            _telemetryClient = telemetryClient;

            OnTurnError = async (turnContext, exception) =>
            {
                logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");
                await turnContext.SendActivityAsync("The bot encountered an error or bug.");
                await turnContext.SendActivityAsync("To continue to run this bot, please fix the bot source code.");
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.Message, "https://www.botframework.com/schemas/error", "TurnError");
            };
        }

        // A linha abaixo foi corrigida para resolver a ambiguidade
        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, Microsoft.Bot.Schema.Activity[] activities, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var responses = await base.SendActivitiesAsync(turnContext, activities, cancellationToken);
            stopwatch.Stop();

            // Usando TrackEvent para registrar a métrica
            var metrics = new Dictionary<string, double> { { "Duration", stopwatch.ElapsedMilliseconds } };
            _telemetryClient.TrackEvent("TempoTotalTurno", metrics: metrics);

            return responses;
        }
    }
}