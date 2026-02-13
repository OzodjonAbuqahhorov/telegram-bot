using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

class Program
{
    private static readonly TelegramBotClient bot;
    static Dictionary<long, string> userLang = new();
    static Dictionary<long, string> userPhone = new();

    static Program()
    {
        // Read bot token from environment variable
        const string tokenEnvVarName = "TELEGRAM_BOT_TOKEN";
        var token = Environment.GetEnvironmentVariable(tokenEnvVarName);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                $"Environment variable '{tokenEnvVarName}' is not set. Please set it to your Telegram bot token.");
        }

        bot = new TelegramBotClient(token);
    }

    static async Task Main()
    {
        using CancellationTokenSource cts = new();
        ReceiverOptions receiverOptions = new() { AllowedUpdates = { } };

        bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);
        Console.WriteLine("Bot ishga tushdi...");
        await Task.Delay(-1);
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message != null)
        {
            var message = update.Message;
            var chatId = message.Chat.Id;

            if (message.Text == "/start")
            {
                userLang.Remove(chatId);
                userPhone.Remove(chatId);
                var keyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { new KeyboardButton("🇺🇿 UZ") },
                    new KeyboardButton[] { new KeyboardButton("🇬🇧 EN") },
                    new KeyboardButton[] { new KeyboardButton("🇷🇺 RU") }
                })
                { ResizeKeyboard = true };

                await botClient.SendTextMessageAsync(chatId, "🌐 Tilni tanlang / Choose language / Выберите язык", replyMarkup: keyboard);
                return;
            }

            if (message.Text == "🇺🇿 UZ" || message.Text == "🇬🇧 EN" || message.Text == "🇷🇺 RU")
            {
                string lang = message.Text[^2..];
                userLang[chatId] = lang;

                var contactKeyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { new KeyboardButton("📞 Telefon raqamni yuborish") { RequestContact = true } }
                })
                { ResizeKeyboard = true };

                string text = lang switch
                {
                    "UZ" => "📱 Iltimos, telefon raqamingizni yuboring.",
                    "EN" => "📱 Please share your phone number.",
                    "RU" => "📱 Пожалуйста, отправьте номер телефона.",
                    _ => ""
                };

                await botClient.SendTextMessageAsync(chatId, text, replyMarkup: contactKeyboard);
                return;
            }

            // Telefon qabul qilish (contact yoki qo‘lda yozilgan)
            if (message.Contact != null || message.Text != null)
            {
                string phone = message.Contact != null
                ? message.Contact.PhoneNumber
                : message.Text;

                phone = phone.Replace(" ", "")
                 .Replace("-", "")
                 .Replace("(", "")
                 .Replace(")", "");

                if (!Regex.IsMatch(phone, @"^\+?\d{10,15}$"))
                {
                    await botClient.SendTextMessageAsync(chatId, "❌ Telefon raqam noto‘g‘ri formatda.");
                    return;
                }

                userPhone[chatId] = phone;

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ Kanalga obuna bo‘ldim", "check_sub")
                });

                await botClient.SendTextMessageAsync(chatId,
                "Kanalga obuna bo‘ling va tasdiqlang:\n@samtexsockss",
                replyMarkup: inlineKeyboard);

                return;
            }

        }

        if (update.Type == UpdateType.CallbackQuery)
        {
            var callback = update.CallbackQuery;
            var chatId = callback.Message.Chat.Id;

            if (callback.Data == "check_sub")
            {
                var member = await botClient.GetChatMemberAsync("@samtexsockss", chatId);

                if (member.Status == ChatMemberStatus.Member ||
                    member.Status == ChatMemberStatus.Administrator ||
                    member.Status == ChatMemberStatus.Creator)
                {
                    if (!userLang.ContainsKey(chatId))
                    {
                        await botClient.SendTextMessageAsync(chatId, "Iltimos avval /start bosing");
                        return;
                    }
                    string lang = userLang[chatId];

                    string videoText = lang switch
                    {
                        "UZ" => "🎥 Video tez orada yuklanadi.",
                        "EN" => "🎥 Video will be uploaded soon.",
                        "RU" => "🎥 Видео будет загружено в ближайшее время.",
                        _ => ""
                    };

                    await botClient.SendTextMessageAsync(chatId, videoText);

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(10));

                        var buttons = new InlineKeyboardMarkup(new[]
                        {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("👍 Ha", "yes"),
                                InlineKeyboardButton.WithCallbackData("👎 Yo‘q", "no")
                            }
                        });

                        string question = lang switch
                        {
                            "UZ" => "Sizga ko‘rsatmamiz ma’qul keldimi?",
                            "EN" => "Did you like the presentation?",
                            "RU" => "Вам понравилась презентация?",
                            _ => ""
                        };

                        await botClient.SendTextMessageAsync(chatId, question, replyMarkup: buttons);
                    });
                }
                else
                {
                    await botClient.AnswerCallbackQueryAsync(callback.Id, "Avval kanalga obuna bo‘ling!", true);
                }
            }

            if (callback.Data == "yes" || callback.Data == "no")
            {
                await botClient.SendTextMessageAsync(chatId, "Ajoyib! 😊 Tez orada siz bilan aloqaga chiqamiz.");
            }
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine(exception.Message);
        return Task.CompletedTask;
    }

    static async Task WriteToSheet(string userId, string name, string phone)
    {
        var json = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS");

        var credential = GoogleCredential
        .FromJson(json)
        .CreateScoped(SheetsService.Scope.Spreadsheets);

        var service = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "TelegramBot"
        });

        var range = "Sheet1!A:D";

        var valueRange = new ValueRange()
        {
            Values = new List<IList<object>>
            {
                new List<object> { userId, name, phone, DateTime.Now.ToString() }
            }
        };

        var appendRequest = service.Spreadsheets.Values.Append(valueRange,"1br6NU-Hz1jr6Ctl-EfREovx79a8CM3Et1On0bE3I_Qw",range);

        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

        await appendRequest.ExecuteAsync();
    }

}
