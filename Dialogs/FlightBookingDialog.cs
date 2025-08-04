// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace Microsoft.BotBuilderSamples
{
    public class FlightBookingDialog : CancelAndHelpDialog
    {
        private readonly IBotTelemetryClient _telemetryClient;

        public FlightBookingDialog(IBotTelemetryClient telemetryClient)
            : base(nameof(FlightBookingDialog))
        {
            _telemetryClient = telemetryClient;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new DateResolverDialog());
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                DestinationStepAsync,
                OriginStepAsync,
                TravelDateStepAsync,
                ConfirmStepAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }
        
        // ... (Os métodos DestinationStepAsync, OriginStepAsync, TravelDateStepAsync, ConfirmStepAsync permanecem os mesmos)
        private async Task<DialogTurnResult> DestinationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var bookingDetails = (BookingDetails)stepContext.Options;
            if (bookingDetails.Destination == null)
            {
                var promptMessage = MessageFactory.Text("Where would you like to travel to?", inputHint: InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
            return await stepContext.NextAsync(bookingDetails.Destination, cancellationToken);
        }

        private async Task<DialogTurnResult> OriginStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var bookingDetails = (BookingDetails)stepContext.Options;
            bookingDetails.Destination = (string)stepContext.Result;
            if (bookingDetails.Origin == null)
            {
                var promptMessage = MessageFactory.Text("Where are you traveling from?", inputHint: InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
            return await stepContext.NextAsync(bookingDetails.Origin, cancellationToken);
        }

        private async Task<DialogTurnResult> TravelDateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var bookingDetails = (BookingDetails)stepContext.Options;
            bookingDetails.Origin = (string)stepContext.Result;
            if (bookingDetails.TravelDate == null || IsAmbiguous(bookingDetails.TravelDate))
            {
                return await stepContext.BeginDialogAsync(nameof(DateResolverDialog), bookingDetails.TravelDate, cancellationToken);
            }
            return await stepContext.NextAsync(bookingDetails.TravelDate, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var bookingDetails = (BookingDetails)stepContext.Options;
            bookingDetails.TravelDate = (string)stepContext.Result;
            var messageText = $"Please confirm, I have you traveling to: {bookingDetails.Destination} from: {bookingDetails.Origin} on: {bookingDetails.TravelDate}. Is this correct?";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }


        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var stopwatch = new Stopwatch();
            var properties = new Dictionary<string, string> { { "UserId", stepContext.Context.Activity.From.Id } };
            var bookingDetails = (BookingDetails)stepContext.Options; // Movido para fora do if

            if ((bool)stepContext.Result)
            {
                stopwatch.Start();
                await Task.Delay(150); // Simulação de chamada a serviço externo (QnA, etc.)
                stopwatch.Stop();
                var qnaMetrics = new Dictionary<string, double> { { "Duration", stopwatch.ElapsedMilliseconds } };
                _telemetryClient.TrackEvent("TempoPesquisaConhecimento(QnA)", properties, qnaMetrics);

                stopwatch.Restart();
                var resultMessageText = "I have you booked to " + bookingDetails.Destination;
                var resultMessage = MessageFactory.Text(resultMessageText, resultMessageText, InputHints.IgnoringInput);
                stopwatch.Stop();
                var formatMetrics = new Dictionary<string, double> { { "Duration", stopwatch.ElapsedMilliseconds } };
                _telemetryClient.TrackEvent("TempoFormatacaoResposta", properties, formatMetrics);

                await stepContext.Context.SendActivityAsync(resultMessage, cancellationToken);
                
                // Corrigido: o retorno deve estar dentro do if/else
                return await stepContext.EndDialogAsync(bookingDetails, cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thank you."), cancellationToken);
                // Corrigido: o retorno deve estar dentro do if/else
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
        
        private static bool IsAmbiguous(string timex)
        {
            var timexProperty = new TimexProperty(timex);
            return !timexProperty.Types.Contains(Constants.TimexTypes.Definite);
        }
    }
}