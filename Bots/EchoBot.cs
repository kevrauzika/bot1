// Bots/EchoBot.cs
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples.Bots
{
    public class EchoBot : ActivityHandler
    {
        private readonly RAGService _ragService;

        public EchoBot(RAGService ragService)
        {
            _ragService = ragService;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var userQuestion = turnContext.Activity.Text;
            await turnContext.SendActivityAsync(MessageFactory.Text("Pesquisando na base de conhecimento..."), cancellationToken);
            var answer = await _ragService.GetAnswerAsync(userQuestion);
            await turnContext.SendActivityAsync(MessageFactory.Text(answer), cancellationToken);
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