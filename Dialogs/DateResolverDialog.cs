// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace Microsoft.BotBuilderSamples
{
    public class DateResolverDialog : CancelAndHelpDialog
    {
        private const string PromptMsgText = "When would you like to travel?";
        private const string RepromptMsgText = "I'm sorry, to make your booking please enter a full travel date including Day Month and Year.";

        public DateResolverDialog(string id = null)
            : base(id ?? nameof(DateResolverDialog))
        {
            AddDialog(new DateTimePrompt(nameof(DateTimePrompt), DateTimePromptValidator));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                InitialStepAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> InitialStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var timex = (string)stepContext.Options;

            var promptMessage = MessageFactory.Text(PromptMsgText, PromptMsgText, InputHints.ExpectingInput);
            var repromptMessage = MessageFactory.Text(RepromptMsgText, RepromptMsgText, InputHints.ExpectingInput);

            var options = new PromptOptions
            {
                Prompt = promptMessage,
                RetryPrompt = repromptMessage,
            };

            if (timex == null)
            {
                return await stepContext.PromptAsync(nameof(DateTimePrompt), options, cancellationToken);
            }
            
            // Se um timex foi passado, tentamos resolvê-lo.
            var results = TimexResolver.Resolve(new[] { timex });
            if (results.Values.Count > 0 && TimexResolver.IsDefinite(timex))
            {
                // Se for uma data definitiva, podemos prosseguir.
                var resolution = new List<DateTimeResolution> { new DateTimeResolution { Timex = timex, Value = results.Values[0].Value } };
                return await stepContext.NextAsync(resolution, cancellationToken);
            }
            else
            {
                // Caso contrário, pedimos novamente ao usuário.
                return await stepContext.PromptAsync(nameof(DateTimePrompt), options, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var timex = ((List<DateTimeResolution>)stepContext.Result)[0].Timex;
            return await stepContext.EndDialogAsync(timex, cancellationToken);
        }

        private static Task<bool> DateTimePromptValidator(PromptValidatorContext<IList<DateTimeResolution>> promptContext, CancellationToken cancellationToken)
        {
            if (promptContext.Recognized.Succeeded)
            {
                var timex = promptContext.Recognized.Value[0].Timex;
                return Task.FromResult(TimexResolver.IsDefinite(timex));
            }

            return Task.FromResult(false);
        }
    }
}