// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
// Removido: using Microsoft.ApplicationInsights.DataContracts;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class RootDialog : ComponentDialog
    {
        private readonly FlightBookingDialog _flightBookingDialog;
        private readonly IBotTelemetryClient _telemetryClient;

        public RootDialog(ILogger<RootDialog> logger, FlightBookingDialog flightBookingDialog, IBotTelemetryClient telemetryClient)
            : base(nameof(RootDialog))
        {
            _flightBookingDialog = flightBookingDialog;
            _telemetryClient = telemetryClient;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(_flightBookingDialog);

            var waterfallSteps = new WaterfallStep[]
            {
                InitialStepAsync,
            };

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> InitialStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var stopwatch = new Stopwatch();
            var properties = new Dictionary<string, string> { { "UserId", stepContext.Context.Activity.From.Id }, { "DialogId", Id } };

            // Corrigido para usar Severity.Information do Microsoft.Bot.Builder
            _telemetryClient.TrackTrace("Iniciando reconhecimento do LUIS/Orchestrator", Severity.Information, properties);
            stopwatch.Start();
            
            var dialogResult = await stepContext.BeginDialogAsync(nameof(FlightBookingDialog), new BookingDetails(), cancellationToken);
            
            stopwatch.Stop();
            
            var metrics = new Dictionary<string, double> { { "Duration", stopwatch.ElapsedMilliseconds } };
            _telemetryClient.TrackEvent("TempoPesquisaPergunta(LUIS)", properties, metrics);
            
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}