namespace Utilities.TelegramService.Contracts
{
    public interface ITelegramService
    {
        Task SendMessageAsync(string chatId, string message);
    }
}
