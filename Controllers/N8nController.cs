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
    // Define a rota para este controller. As chamadas serão feitas para /api/n8n
    [Route("api/n8n")]
    [ApiController]
    public class N8nController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly IBot _bot;

        // O construtor recebe o "adaptador" e a implementação do "bot" via injeção de dependência.
        // O ASP.NET Core cuida disso para nós.
        public N8nController(IBotFrameworkHttpAdapter adapter, IBot bot)
        {
            _adapter = adapter;
            _bot = bot;
        }

        // Define a rota para o método POST. Ele será acionado por POST /api/n8n/ask
        [HttpPost("ask")]
        public async Task PostAsync()
        {
            // Lê o corpo da requisição que o n8n enviou
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var requestBody = await reader.ReadToEndAsync();
                
                // Extrai a pergunta do JSON enviado pelo n8n
                var jsonBody = JObject.Parse(requestBody);
                var question = jsonBody["question"]?.ToString();

                if (string.IsNullOrEmpty(question))
                {
                    // Se nenhuma pergunta foi enviada, retorna um erro.
                    Response.StatusCode = 400;
                    await Response.Body.WriteAsync(Encoding.UTF8.GetBytes("O campo 'question' é obrigatório."));
                    return;
                }

                // O truque está aqui: Usamos o método ProcessActivityAsync do adaptador para
                // simular uma "conversa" com o bot, passando a pergunta como a mensagem.
                // O resultado será o que o bot teria respondido.
                await _adapter.ProcessActivityAsync(Request, Response, _bot, default(CancellationToken));
            }
        }
    }
}