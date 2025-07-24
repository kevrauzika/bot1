using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples
{
    public class RAGService
    {
        private readonly OpenAIClient _openAiClient;
        private readonly SearchClient _searchClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RAGService> _logger;

        public RAGService(IConfiguration configuration, ILogger<RAGService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            try
            {
                // Configurar Azure OpenAI
                var openAiEndpoint = configuration["AzureOpenAi:Endpoint"];
                var openAiKey = configuration["AzureOpenAi:ApiKey"];

                _logger.LogInformation("Configurando Azure OpenAI: {Endpoint}", openAiEndpoint);
                _openAiClient = new OpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));

                // Configurar Azure AI Search
                var searchEndpoint = configuration["AzureAiSearch:Endpoint"];
                var searchKey = configuration["AzureAiSearch:ApiKey"];
                var indexName = configuration["AzureAiSearch:IndexName"];

                _logger.LogInformation("Configurando Azure AI Search: {Endpoint}, Index: {IndexName}", searchEndpoint, indexName);
                _searchClient = new SearchClient(new Uri(searchEndpoint), indexName, new AzureKeyCredential(searchKey));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao configurar RAGService");
                throw;
            }
        }

        public async Task<string> GetAnswerAsync(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return "Por favor, faça uma pergunta válida.";
            }

            try
            {
                _logger.LogInformation("Processando pergunta: {Question}", question);

                // Passo 1: Gerar embedding da pergunta
                var questionEmbedding = await GenerateEmbeddingAsync(question);
                if (questionEmbedding == null)
                {
                    return "Não foi possível processar sua pergunta no momento.";
                }

                // Passo 2: Buscar documentos relevantes
                var relevantDocs = await SearchRelevantDocumentsAsync(question, questionEmbedding);
                if (!relevantDocs.Any())
                {
                    return "Não encontrei informação relevante sobre sua pergunta em nossa base de conhecimento.";
                }

                // Passo 3: Gerar resposta usando contexto
                var answer = await GenerateAnswerAsync(question, relevantDocs);

                _logger.LogInformation("Resposta gerada com sucesso para: {Question}", question);
                return answer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar pergunta: {Question}", question);
                return "Desculpe, ocorreu um erro ao processar sua pergunta. Tente novamente em alguns instantes.";
            }
        }

        private async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            try
            {
                var embeddingDeployment = _configuration["AzureOpenAi:EmbeddingDeploymentName"];
                _logger.LogDebug("Gerando embedding usando deployment: {Deployment}", embeddingDeployment);

                var embeddingResponse = await _openAiClient.GetEmbeddingsAsync(
                    new EmbeddingsOptions(embeddingDeployment, new[] { text }));

                return embeddingResponse.Value.Data[0].Embedding.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar embedding para: {Text}", text);
                return null;
            }
        }

        private async Task<List<SearchDocument>> SearchRelevantDocumentsAsync(string question, float[] questionEmbedding)
        {
            try
            {
                var searchOptions = new SearchOptions
                {
                    Size = 3,
                    Select = { "content", "source" }
                };

                searchOptions.VectorSearch = new()
                {
                    Queries = {
                        new VectorizedQuery(questionEmbedding)
                        {
                            KNearestNeighborsCount = 3,
                            Fields = { "content_vector" }
                        }
                    }
                };

                _logger.LogDebug("Executando busca vetorial para: {Question}", question);
                var searchResults = await _searchClient.SearchAsync<SearchDocument>(question, searchOptions);

                var documents = new List<SearchDocument>();
                await foreach (var result in searchResults.Value.GetResultsAsync())
                {
                    documents.Add(result.Document);
                }

                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar documentos relevantes");
                return new List<SearchDocument>();
            }
        }

        private async Task<string> GenerateAnswerAsync(string question, List<SearchDocument> relevantDocs)
        {
            try
            {
                var context = string.Join("\n\n", relevantDocs.Select((doc, index) =>
                    $"Documento {index + 1}:\n{(doc.ContainsKey("content") ? doc["content"].ToString() : "")}"));

                if (string.IsNullOrWhiteSpace(context))
                {
                    return "Não encontrei informação suficiente para responder sua pergunta.";
                }

                var chatDeployment = _configuration["AzureOpenAi:ChatDeploymentName"];

                var systemPrompt = @"Você é um assistente de suporte técnico. 
Use apenas as informações do contexto fornecido para responder.
Contexto: " + context;

                var chatOptions = new ChatCompletionsOptions()
                {
                    DeploymentName = chatDeployment,
                    Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(question)
            },
                    MaxTokens = 500,
                    Temperature = 0.3f
                };

                var response = await _openAiClient.GetChatCompletionsAsync(chatOptions);
                var answer = response.Value.Choices[0].Message.Content;

                return answer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar resposta");
                return "Desculpe, não consegui gerar uma resposta adequada no momento.";
            }
        }
    }
}