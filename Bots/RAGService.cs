// Bots/RAGService.cs
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples.Bots
{
    public class RAGService
    {
        private readonly OpenAIClient _embeddingClient;
        private readonly OpenAIClient _chatClient;
        private readonly SearchClient _searchClient;
        private readonly string _embeddingDeploymentName;
        private readonly string _chatDeploymentName;

        public RAGService(IConfiguration configuration)
        {
            _embeddingClient = new OpenAIClient(new Uri(configuration["EmbeddingService:Endpoint"]), new AzureKeyCredential(configuration["EmbeddingService:ApiKey"]));
            _embeddingDeploymentName = configuration["EmbeddingService:DeploymentName"];
            _chatClient = new OpenAIClient(new Uri(configuration["ChatService:Endpoint"]), new AzureKeyCredential(configuration["ChatService:ApiKey"]));
            _chatDeploymentName = configuration["ChatService:DeploymentName"];
            _searchClient = new SearchClient(new Uri(configuration["AzureAiSearch:Endpoint"]), configuration["AzureAiSearch:IndexName"], new AzureKeyCredential(configuration["AzureAiSearch:ApiKey"]));
        }

        public async Task<string> GetAnswerAsync(string userQuestion)
        {
            var questionEmbeddingResponse = await _embeddingClient.GetEmbeddingsAsync(new EmbeddingsOptions(_embeddingDeploymentName, new[] { userQuestion }));
            ReadOnlyMemory<float> questionEmbedding = questionEmbeddingResponse.Value.Data[0].Embedding;

            var searchOptions = new SearchOptions
            {
                VectorSearch = new() { Queries = { new VectorizedQuery(questionEmbedding) { KNearestNeighborsCount = 3, Fields = { "content_vector" } } } },
                Size = 3,
                // --- CORRE��O APLICADA AQUI ---
                Select = { "content", "source" }
            };

            SearchResults<SearchDocument> response = await _searchClient.SearchAsync<SearchDocument>(null, searchOptions);

            var retrievedDocuments = new List<string>();
            await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
            {
                // --- CORRE��O APLICADA AQUI ---
                retrievedDocuments.Add($"Fonte: {result.Document["source"]}\nConte�do: {result.Document["content"]}\n");
            }

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Voc� � um assistente de IA para a equipe de suporte t�cnico.");
            promptBuilder.AppendLine("Responda � pergunta do usu�rio baseado estritamente nas fontes de informa��o fornecidas abaixo.");
            promptBuilder.AppendLine("Se a informa��o n�o estiver nos documentos, responda 'N�o encontrei informa��o sobre isso nos meus documentos.'.");
            promptBuilder.AppendLine("\n--- FONTES DE INFORMA��O ---");
            foreach (var doc in retrievedDocuments) { promptBuilder.AppendLine(doc); }
            promptBuilder.AppendLine("\n--- PERGUNTA DO USU�RIO ---");
            promptBuilder.AppendLine(userQuestion);
            promptBuilder.AppendLine("\n--- RESPOSTA ---");

            var chatCompletionsOptions = new ChatCompletionsOptions()
            {
                DeploymentName = _chatDeploymentName,
                Messages = { new ChatRequestSystemMessage(promptBuilder.ToString()) }
            };
            var chatResponse = await _chatClient.GetChatCompletionsAsync(chatCompletionsOptions);
            return chatResponse.Value.Choices[0].Message.Content;
        }
    }
}