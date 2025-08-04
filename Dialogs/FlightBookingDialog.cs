// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace Microsoft.BotBuilderSamples
{
    public class FlightBookingDialog : CancelAndHelpDialog
    {
        private const string DestinationStepMsgText = "Where would you like to travel to?";
        private const string OriginStepMsgText = "Where are you traveling from?";

        private readonly IBotTelemetryClient _telemetryClient; // Adicione esta linha

        public FlightBookingDialog(IBotTelemetryClient telemetryClient) // Adicione o IBotTelemetryClient
            : base(nameof(FlightBookingDialog))
        {
            _telemetryClient = telemetryClient; // Adicione esta linha

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

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        // ... (os métodos DestinationStepAsync, OriginStepAsync, TravelDateStepAsync, ConfirmStepAsync continuam os mesmos)
        // Nenhuma alteração necessária neles.

        private async Task<DialogTurnResult> DestinationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var bookingDetails = (BookingDetails)stepContext.Options;

            if (bookingDetails.Destination == null)
            {
                var promptMessage = MessageFactory.Text(DestinationStepMsgText, DestinationStepMsgText, InputHints.ExpectingInput);
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
                var promptMessage = MessageFactory.Text(OriginStepMsgText, OriginStepMsgText, InputHints.ExpectingInput);
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

            if ((bool)stepContext.Result)
            {
                var bookingDetails = (BookingDetails)stepContext.Options;

                // --- Medindo o Tempo de Pesquisa na Base de Conhecimento (simulado) ---
                stopwatch.Start();
                // Se você tivesse uma chamada ao QnA Maker, ela estaria aqui.
                // Exemplo: var qnaResult = await _qnaMaker.GetAnswersAsync(stepContext.Context);
                await Task.Delay(150); // Simulação de uma chamada de serviço externo
                stopwatch.Stop();
                _telemetryClient.TrackMetric("TempoPesquisaConhecimento(QnA)", stopwatch.ElapsedMilliseconds, properties);


                // --- Medindo o Tempo de Formação da Resposta ---
                stopwatch.Restart();
                var resultMessageText = "I have you booked to " + bookingDetails.Destination;
                var resultMessage = MessageFactory.Text(resultMessageText, resultMessageText, InputHints.IgnoringInput);
                stopwatch.Stop();
                _telemetryClient.TrackMetric("TempoFormatacaoResposta", stopwatch.ElapsedMilliseconds, properties);

                await stepContext.Context.SendActivityAsync(resultMessage, cancellationToken);
            }
            else
            {
                var resultMessageText = "Thank you.";
                var resultMessage = MessageFactory.Text(resultMessageText, resultMessageText, InputHints.IgnoringInput);
                await stepContext.Context.SendActivityAsync(resultMessage, cancellationToken);
            }
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }

        private static bool IsAmbiguous(string timex)
        {
            var timexProperty = new TimexProperty(timex);
            return !timexProperty.Types.Contains(Constants.TimexTypes.Definite);
        }
    }
}