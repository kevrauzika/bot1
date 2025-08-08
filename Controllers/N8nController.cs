// Nome do arquivo: Controllers/N8nController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples.Controllers
{
    [Route("api/n8n")]
    [ApiController]
    public class N8nController : ControllerBase
    {
        // Use a classe concreta 'BotFrameworkHttpAdapter' em vez da interface 'IBotFrameworkHttpAdapter'
        private readonly BotFrameworkHttpAdapter _adapter; // <-- ALTERAÇÃO AQUI
        private readonly IBot _bot;

        // O construtor agora pede a classe concreta.
        public N8nController(BotFrameworkHttpAdapter adapter, IBot bot) // <-- ALTERAÇÃO AQUI
        {
            _adapter = adapter;
            _bot = bot;
        }

        [HttpPost("ask")]
        public async Task PostAsync()
        {
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var requestBody = await reader.ReadToEndAsync();
                
                // Extrai a pergunta do JSON enviado pelo n8n
                var jsonBody = JObject.Parse(requestBody);
                var question = jsonBody["question"]?.ToString();

                // Simula uma atividade de mensagem para o bot
                var activity = new Microsoft.Bot.Schema.Activity
                {
                    Type = Microsoft.Bot.Schema.ActivityTypes.Message,
                    From = new Microsoft.Bot.Schema.ChannelAccount { Id = "n8n-user" },
                    Recipient = new Microsoft.Bot.Schema.ChannelAccount { Id = "bot" },
                    ServiceUrl = "https://smba.trafficmanager.net/amer/", // URL de serviço padrão
                    ChannelId = "emulator", // Simula ser do emulador
                    Text = question
                };
                
                // Usa a nova atividade para processar
                await _adapter.ProcessActivityAsync(Request, Response, _bot, default(CancellationToken));
            }
        }
    }
}