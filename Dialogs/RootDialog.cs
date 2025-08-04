// Dialogs/RootDialog.cs

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics; // Adicione esta linha
using System.Collections.Generic; // Adicione esta linha

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class RootDialog : ComponentDialog
    {
        private readonly ILogger _logger;
        private readonly FlightBookingDialog _flightBookingDialog;
        private readonly IBotTelemetryClient _telemetryClient; // Adicione esta linha

        public RootDialog(ILogger<RootDialog> logger, FlightBookingDialog flightBookingDialog, IBotTelemetryClient telemetryClient) // Adicione IBotTelemetryClient telemetryClient
            : base(nameof(RootDialog))
        {
            _logger = logger;
            _flightBookingDialog = flightBookingDialog;
            _telemetryClient = telemetryClient; // Adicione esta linha

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

            // Propriedades para dar contexto ao log
            var properties = new Dictionary<string, string>
            {
                { "UserId", stepContext.Context.Activity.From.Id },
                { "DialogId", Id }
            };

            // ---- Tempo de pesquisa com base na pergunta (LUIS/Orchestrator) ----
            _telemetryClient.TrackTrace("Iniciando reconhecimento do LUIS/Orchestrator", Severity.Information, properties);
            stopwatch.Start();

            // A chamada ao LUIS/Orchestrator já está encapsulada dentro do BeginDialogAsync do FlightBookingDialog
            // Então, medimos o tempo de execução deste diálogo.
            var dialogResult = await stepContext.BeginDialogAsync(nameof(FlightBookingDialog), null, cancellationToken);

            stopwatch.Stop();
            _telemetryClient.TrackMetric("TempoPesquisaPergunta(LUIS)", stopwatch.ElapsedMilliseconds, properties);

            // Se o diálogo filho (FlightBooking) terminou, o RootDialog também pode terminar.
            if (dialogResult.Status == DialogTurnStatus.Complete)
            {
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            else
            {
                // Se o diálogo filho ainda estiver ativo (ex: esperando por uma resposta do usuário),
                // o RootDialog também permanece ativo.
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
    }
}