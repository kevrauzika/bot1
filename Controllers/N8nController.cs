// Nome do arquivo: Controllers/N8nController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples.Controllers
{
    [Route("api/n8n")]
    [ApiController]
    public class N8nController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly IBot _bot;

        public N8nController(IBotFrameworkHttpAdapter adapter, IBot bot)
        {
            _adapter = adapter;
            _bot = bot;
        }

        [HttpPost("ask")]
        public async Task PostAsync()
        {
            // 1. Ler o corpo da requisição que o n8n enviou
            string requestBody;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            // 2. Extrair a pergunta do JSON
            var jsonBody = JObject.Parse(requestBody);
            var question = jsonBody["question"]?.ToString();

            if (string.IsNullOrEmpty(question))
            {
                // Se a chave "question" não estiver no JSON, retorna um erro claro.
                Response.StatusCode = 400; // Bad Request
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes("O corpo da requisição precisa conter um campo 'question'."));
                return;
            }

            // 3. Criar uma Atividade de Mensagem a partir da pergunta
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = question,
                ChannelId = "n8n-channel",
                From = new ChannelAccount { Id = "n8n-user" },
                Recipient = new ChannelAccount { Id = "bot" },
                ServiceUrl = "urn:n8n" // Um valor fictício, mas necessário
            };

            // 4. Processar a atividade manualmente e obter a resposta
            // Usamos um "TurnContext" e o método "OnTurnAsync" do bot.
            await ((BotAdapter)_adapter).ContinueConversationAsync(
                "appId-ficticio", // App ID, pode ser qualquer valor aqui
                activity,
                async (turnContext, cancellationToken) =>
                {
                    // Executa a lógica principal do bot (o seu EchoBot)
                    await _bot.OnTurnAsync(turnContext, cancellationToken);

                    // A resposta do bot estará em turnContext.TurnState.
                    // Aqui, nós apenas deixamos o bot enviar a resposta.
                    // O BotAdapter vai capturar e enviar como a resposta HTTP.

                }, default(CancellationToken));
        }
    }
}