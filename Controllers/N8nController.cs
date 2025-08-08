// Nome do arquivo: Controllers/N8nController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples.Controllers
{
    // Rota para este controller: /api/n8n
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

        // Rota para o método POST: /api/n8n/ask
        // Este método vai receber a chamada do n8n
        [HttpPost("ask")]
        public async Task PostAsync()
        {
            // O código do Bot Framework é projetado para lidar diretamente
            // com o Request e o Response. Nós simplesmente passamos para ele.
            // O adaptador vai ler o corpo da requisição (o JSON com a "question")
            // e vai tratar como uma atividade de mensagem para o IBot.
            // A resposta do bot será escrita diretamente no HttpResponse.
            await _adapter.ProcessAsync(Request, Response, _bot);
        }
    }
}