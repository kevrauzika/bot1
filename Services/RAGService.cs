// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
using System.Diagnostics;
using Microsoft.Bot.Builder;

namespace Microsoft.BotBuilderSamples
{
    public class RAGService
    {
        private readonly OpenAIClient _openAiClient;
        private readonly SearchClient _searchClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RAGService> _logger;
        private readonly IBotTelemetryClient _telemetryClient;

        public RAGService(IConfiguration configuration, ILogger<RAGService> logger, IBotTelemetryClient telemetryClient)
        {
            _configuration = configuration;
            _logger = logger;
            _telemetryClient = telemetryClient;

            try
            {
                var openAiEndpoint = configuration["AzureOpenAi:Endpoint"];
                var openAiKey = configuration["AzureOpenAi:ApiKey"];
                _openAiClient = new OpenAIClient(new Uri(openAiEndpoint), new AzureKeyCredential(openAiKey));

                var searchEndpoint = configuration["AzureAiSearch:Endpoint"];
                var searchKey = configuration["AzureAiSearch:ApiKey"];
                var indexName = configuration["AzureAiSearch:IndexName"];
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
            if (string.IsNullOrWhiteSpace(question)) return "Por favor, faça uma pergunta válida.";

            try
            {
                var stopwatch = new Stopwatch();

                // Passo 1: Gerar embedding da pergunta
                stopwatch.Start();
                var questionEmbedding = await GenerateEmbeddingAsync(question);
                stopwatch.Stop();
                _logger.LogInformation("TEMPO DE EXECUÇÃO: Gerar Embedding da Pergunta - {Duration} ms", stopwatch.ElapsedMilliseconds);
                _telemetryClient.TrackEvent("RAG - Gerar Embedding", metrics: new Dictionary<string, double> { { "Duration", stopwatch.ElapsedMilliseconds } });
                if (questionEmbedding == null) return "Não foi possível processar sua pergunta no momento.";

                // Passo 2: Buscar documentos relevantes
                stopwatch.Restart();
                var relevantDocs = await SearchRelevantDocumentsAsync(question, questionEmbedding);
                stopwatch.Stop();
                _logger.LogInformation("TEMPO DE EXECUÇÃO: Buscar na Base de Conhecimento - {Duration} ms", stopwatch.ElapsedMilliseconds);
                _telemetryClient.TrackEvent("RAG - Buscar Documentos", metrics: new Dictionary<string, double> { { "Duration", stopwatch.ElapsedMilliseconds } });
                if (!relevantDocs.Any()) return "Não encontrei informação relevante sobre sua pergunta em nossa base de conhecimento.";

                // Passo 3: Gerar resposta usando contexto
                stopwatch.Restart();
                var answer = await GenerateAnswerAsync(question, relevantDocs);
                stopwatch.Stop();
                _logger.LogInformation("TEMPO DE EXECUÇÃO: Montar Resposta Final (OpenAI) - {Duration} ms", stopwatch.ElapsedMilliseconds);
                _telemetryClient.TrackEvent("RAG - Gerar Resposta Final", metrics: new Dictionary<string, double> { { "Duration", stopwatch.ElapsedMilliseconds } });

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
                    return "Não encontrei informação suficiente para responder sua pergunta em nossa base de conhecimento. Por favor, reformule sua pergunta ou entre em contato com o suporte técnico.";
                }

                var chatDeployment = _configuration["AzureOpenAi:ChatDeploymentName"];
                var systemPrompt = @"Você é um ASSISTENTE DE SUPORTE TÉCNICO especializado e altamente qualificado. Sua função é fornecer suporte técnico preciso e profissional.

## REGRAS FUNDAMENTAIS:
1. 🔍 Use APENAS as informações da base de conhecimento fornecida
2. 📋 Se não tiver a informação, diga claramente que não possui essa informação específica
3. 🎯 Seja preciso, claro e direto nas respostas
4. 💼 Mantenha um tom profissional, mas amigável
5. ⚡ Priorize soluções práticas e acionáveis

## ESTRUTURA DE RESPOSTA:
Para PROBLEMAS TÉCNICOS:
- ✅ Confirmação do problema
- 🔧 Passos de solução numerados
- ⚠️ Alertas ou cuidados importantes
- 📞 Quando escalar para suporte humano

Para PROCESSOS/PROCEDIMENTOS:
- 📝 Objetivo do processo
- 📋 Pré-requisitos (se houver)
- 🔢 Passos detalhados numerados
- ✅ Como validar se foi executado corretamente

Para INFORMAÇÕES GERAIS:
- 📖 Explicação clara e objetiva
- 🔗 Relacionamentos com outros sistemas/processos
- 💡 Dicas adicionais úteis

## ESTILO DE COMUNICAÇÃO:
- Use emojis moderadamente para melhor legibilidade
- Evite jargões técnicos desnecessários
- Numere passos quando for um procedimento
- Destaque informações importantes com **negrito**
- Use listas quando apropriado

## ESCALAÇÃO:
Se a situação requer atenção humana, indique:
'⚠️ **ATENÇÃO**: Esta situação requer análise do suporte técnico especializado. Por favor, abra um ticket ou entre em contato conosco.'

## BASE DE CONHECIMENTO DISPONÍVEL:
" + context + @"

## IMPORTANTE:
- Nunca invente informações que não estão na base de conhecimento
- Se precisar de mais detalhes que não estão disponíveis, peça para o usuário ser mais específico
- Sempre termine com uma pergunta de acompanhamento se apropriado";

                var chatOptions = new ChatCompletionsOptions()
                {
                    DeploymentName = chatDeployment,
                    Messages =
                    {
                        new ChatRequestSystemMessage(systemPrompt),
                        new ChatRequestUserMessage(question)
                    },
                    MaxTokens = 500,
                    Temperature = 0.1f
                };

                var response = await _openAiClient.GetChatCompletionsAsync(chatOptions);
                var answer = response.Value.Choices[0].Message.Content;

                var sources = relevantDocs.Select(doc => doc.ContainsKey("source") ? doc["source"].ToString() : "Unknown").Distinct();
                _logger.LogInformation("Resposta gerada usando {DocumentCount} documentos. Sources: {Sources}",
                    relevantDocs.Count, string.Join(", ", sources));

                return answer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar resposta");
                return "🔧 **Ops!** Estou com dificuldades técnicas no momento. Por favor, tente novamente em alguns instantes ou entre em contato com nosso suporte técnico.";
            }
        }
    }
}