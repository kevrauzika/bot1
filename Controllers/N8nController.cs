// Nome do arquivo: Controllers/N8nController.cs

using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples.Controllers
{
    // Criamos uma classe simples para representar o JSON que o n8n envia.
    public class N8nRequestPayload
    {
        public string Question { get; set; }
    }

    [Route("api/n8n")]
    [ApiController]
    public class N8nController : ControllerBase
    {
        // Para este endpoint simples, não precisamos mais do IBot ou do Adapter.
        public N8nController()
        {
        }

        [HttpPost("ask")]
        // O [FromBody] diz ao ASP.NET Core para ler o corpo do JSON
        // e convertê-lo automaticamente para o nosso objeto N8nRequestPayload.
        public IActionResult PostAsync([FromBody] N8nRequestPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Question))
            {
                // Se o JSON estiver mal formatado ou a pergunta estiver vazia, retorna um erro.
                return BadRequest("O corpo da requisição precisa conter um campo 'Question'.");
            }

            // A lógica do bot (Echo) é replicada aqui de forma simples para o teste.
            var responseText = $"Echo from Azure: {payload.Question}";

            // Criamos um objeto de resposta.
            var response = new {
                answer = responseText
            };

            // Retornamos a resposta como um JSON com status 200 OK.
            return Ok(response);
        }
    }
}