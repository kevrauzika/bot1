// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
// Remova a referência ao KnowledgeBaseService se houver e adicione esta:
// (Seu RAGService já está no namespace correto, então talvez não precise do using)

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class RootDialog : ComponentDialog
    {
        private readonly FlightBookingDialog _flightBookingDialog;
        private readonly IBotTelemetryClient _telemetryClient;
        private readonly ILogger _logger;
        private readonly RAGService _ragService; // Alterado para RAGService

        // Injeta o RAGService em vez do KnowledgeBaseService
        public RootDialog(ILogger<RootDialog> logger, FlightBookingDialog flightBookingDialog, IBotTelemetryClient telemetryClient, RAGService ragService)
            : base(nameof(RootDialog))
        {
            _flightBookingDialog = flightBookingDialog;
            _telemetryClient = telemetryClient;
            _logger = logger;
            _ragService = ragService; // Alterado para RAGService

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(_flightBookingDialog);

            var waterfallSteps = new WaterfallStep[]
            {
                DispatchStepAsync,
            };

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> DispatchStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userInput = stepContext.Context.Activity.Text.ToLowerInvariant();

            // Lógica para desviar para o diálogo de voos (exemplo)
            if (userInput.Contains("book flight") || userInput.Contains("travel") || userInput.Contains("reservar voo"))
            {
                _logger.LogInformation("Intenção 'BookFlight' detectada. Iniciando FlightBookingDialog.");
                return await stepContext.BeginDialogAsync(nameof(FlightBookingDialog), new BookingDetails(), cancellationToken);
            }
            else
            {
                // Para todas as outras perguntas, usamos o SEU RAGService
                _logger.LogInformation("Nenhuma intenção conhecida detectada. Chamando RAGService.");
                
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                // Chama o método GetAnswerAsync do seu RAGService
                var response = await _ragService.GetAnswerAsync(stepContext.Context.Activity.Text);
                
                stopwatch.Stop();
                var metrics = new Dictionary<string, double> { { "Duration", stopwatch.ElapsedMilliseconds } };
                _telemetryClient.TrackEvent("TempoBuscaRAG", properties: null, metrics: metrics);

                await stepContext.Context.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
                
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
    }
}