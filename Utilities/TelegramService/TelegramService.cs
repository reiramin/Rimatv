using M1Mentor.Utilities.Exceptions.Common;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Utilities.TelegramService.Configuration;
using Utilities.TelegramService.Contracts;
using static Utilities.Constants.RegisterMode;

namespace Utilities.TelegramService
{
    public class TelegramService : ITelegramService, IScopedDependency
    {
        private readonly TelegramBotClient _botClient;
        private readonly TelegramBotSettings _telegramSetting;
        public TelegramService(TelegramBotSettings telegramSetting) 
        {
            _telegramSetting = telegramSetting;
            _botClient = new TelegramBotClient(_telegramSetting.BotToken);
        }

        public async Task SendMessageAsync(string chatId, string message)
        {
       
            try
            {
                await _botClient.SendMessage(
              chatId: chatId,
              text: message,
              parseMode: ParseMode.Markdown
            );
            }
            catch (Exception)
            {

                throw new BaseException("error in telegram configuration");
            }

        }
    }
}
