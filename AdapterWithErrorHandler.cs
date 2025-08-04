// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.ApplicationInsights.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
                // Log any leaked exception from the application.
                // NOTE: In production environment, you should consider logging this to Azure
                //       application insights. Visit https://aka.ms/bottelemetry for telemetry
                //       configuration instructions.
                logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");

                // Send a message to the user
                var errorMessageText = "The bot encountered an error or bug.";
                var errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.ExpectingInput);
                await turnContext.SendActivityAsync(errorMessage);

                errorMessageText = "To continue to run this bot, please fix the bot source code.";
                errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.ExpectingInput);
                await turnContext.SendActivityAsync(errorMessage);

                // Send a trace activity, which will be displayed in the Bot Framework Emulator
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.Message, "https://www.botframework.com/schemas/error", "TurnError");
            };
        }

        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, Activity[] activities, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            var responses = await base.SendActivitiesAsync(turnContext, activities, cancellationToken);

            stopwatch.Stop();

            // Track the total time for the turn to complete.
            _telemetryClient.TrackMetric("TempoTotalTurno", stopwatch.ElapsedMilliseconds);

            return responses;
        }
    }
}