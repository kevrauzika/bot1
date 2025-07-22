// Bots/EchoBot.cs
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System.Collections.Generic;
using System.Globalization; // Usado para capitalizar a primeira letra
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples.Bots
{
    public class EchoBot : ActivityHandler
    {
        private readonly RAGService _ragService;
        private readonly ConversationState _conversationState;
        private readonly UserState _userState;

        public EchoBot(RAGService ragService, ConversationState conversationState, UserState userState)
        {
            _ragService = ragService;
            _conversationState = conversationState;
            _userState = userState;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var conversationDataAccessor = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await conversationDataAccessor.GetAsync(turnContext, () => new ConversationData(), cancellationToken);

            string userQuestion = turnContext.Activity.Text;
            string questionForRag = userQuestion;
            bool isFollowUpQuestion = false;

            // Regex para pronomes e palavras de acompanhamento
            string[] followUpKeywords = { "ele", "ela", "dele", "dela", "disso", "isso", "nele", "nela", "quem", "qual", "como", "onde", "quando" };
            var followUpRegex = new Regex($@"\b({string.Join("|", followUpKeywords)})\b", RegexOptions.IgnoreCase);

            if (followUpRegex.IsMatch(userQuestion) && !string.IsNullOrEmpty(conversationData.LastMainTopic))
            {
                isFollowUpQuestion = true;
                // Substituição mais robusta
                questionForRag = Regex.Replace(userQuestion, @"\bele\b", $"o {conversationData.LastMainTopic}", RegexOptions.IgnoreCase);
                // Adicione outras substituições se necessário
                // questionForRag = Regex.Replace(questionForRag, @"\bela\b", $"a {conversationData.LastMainTopic}", RegexOptions.IgnoreCase);

                await turnContext.SendActivityAsync(MessageFactory.Text($"Continuando sobre '{conversationData.LastMainTopic}'..."), cancellationToken);
            }

            var answer = await _ragService.GetAnswerAsync(questionForRag);
            await turnContext.SendActivityAsync(MessageFactory.Text(answer, answer), cancellationToken);

            // --- LÓGICA DE ATUALIZAÇÃO DE CONTEXTO CORRIGIDA ---
            if (isFollowUpQuestion)
            {
                // Se foi uma pergunta de acompanhamento, NÃO mudamos o tópico para manter o contexto.
            }
            else
            {
                // Se for uma nova pergunta, tentamos extrair um novo tópico
                var whatIsMatch = Regex.Match(userQuestion, @"(O que é|O que sao|Quem é)\s+([^\?]+)\??", RegexOptions.IgnoreCase);
                if (whatIsMatch.Success && whatIsMatch.Groups.Count > 2)
                {
                    // Pega o tópico (ex: "clearsale") e capitaliza a primeira letra para "Clearsale"
                    string topic = whatIsMatch.Groups[2].Value.Trim();
                    conversationData.LastMainTopic = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(topic);
                }
                else
                {
                    // Fallback: se não for um padrão conhecido, limpamos o tópico para evitar erros.
                    conversationData.LastMainTopic = null;
                }
            }

            // Se o bot não soube responder, sempre limpa o tópico para o próximo turno.
            if (answer.StartsWith("Não encontrei informação"))
            {
                conversationData.LastMainTopic = null;
            }

            // Salva as mudanças na memória
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);

            // --- LINHA CORRIGIDA ---
            await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Olá! Sou o bot de suporte técnico. Faça uma pergunta sobre nossos procedimentos.";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}