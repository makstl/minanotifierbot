using Microsoft.EntityFrameworkCore;
using MinaNotifierBot.MinaExplorer;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Microsoft.Extensions.Logging;

namespace MinaNotifierBot
{
	partial class BotService
	{
		async Task HandleCallbackQuery(User u, BotDbContext db, MinaExplorerClient me, string callbackData, int messageId)
		{
            if (callbackData.StartsWith("deleteaddress"))
            {
                var ua = db.UserAddress.Include(o => o.User).SingleOrDefault(x => x.Id == int.Parse(callbackData.Substring("deleteaddress ".Length)));
                string text = @$"🚫 Address doesn't exists";
                if (ua != null)
                {
                    db.UserAddress.Remove(ua);
                    db.SaveChanges();
                    text = $"Address {ua.Address.ShortAddr() + (ua.Title != ua.Address.ShortAddr() ? " <b>" + ua.Title + "</b>" : "")} deleted";
                    if (!ua.User.HideHashTags)
                        text += "\n\n#deleted" + ua.HashTag();
                }
                else
                    await messageSender.EditMessage(u.Id, messageId, text, null);
            }
            if (callbackData.StartsWith("set_whalealert"))
            {
                await messageSender.EditMessage(u.Id, messageId, @$"Choose whale transactions average threshold", ReplyKeyboards.WhaleAlertSettings(u));
            }
            else if (callbackData == "set_explorer")
                await messageSender.EditMessage(u.Id, messageId, "Choose blockchain explorer", ReplyKeyboards.ExplorerSettings(u));
            else if (callbackData.StartsWith("set_explorer")) 
            {
                int exp = int.Parse(callbackData.Substring("set_explorer_".Length));
                u.Explorer = exp;
                db.SaveChanges();
                await messageSender.EditMessage(u.Id, messageId, "Settings", ReplyKeyboards.Settings(u));
            }
            else if (callbackData == "togglehashtags")
			{
                u.HideHashTags = !u.HideHashTags;
                db.SaveChanges();
                await messageSender.EditMessage(u.Id, messageId, $"Settings", ReplyKeyboards.Settings(u));
            }
            else if (callbackData == "togglereleases")
            {
                u.ReleaseNotify = !u.ReleaseNotify;
                db.SaveChanges();
                await messageSender.EditMessage(u.Id, messageId, $"Settings", ReplyKeyboards.Settings(u));
            }
            else if (callbackData.StartsWith("set_wa_"))
            {
                if (callbackData.Substring("set_wa_".Length).Length > 0)
                {
                    int wat = int.Parse(callbackData.Substring("set_wa_".Length));
                    u.WhaleAlertThreshold = wat * 1000;
                    db.SaveChanges();
                }
                await messageSender.EditMessage(u.Id, messageId, "Settings", ReplyKeyboards.Settings(u));
            }
            else if (callbackData == "pinprice")
            {
                if (u.PinnedMessageId > 0)
                {
                    try
                    {
                        await telegramBotClient.UnpinAllChatMessages(u.Id);
                    }
                    catch { }
                }
                var message = await chartService.GetMessage();
                if (message.Item1 != null)
                {
                    var msg = await telegramBotClient.SendPhotoAsync(u.Id, Telegram.Bot.Types.InputFile.FromStream(message.Item1), caption: message.Item2, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                    await telegramBotClient.PinChatMessageAsync(u.Id, msg.MessageId);
                    u.PinnedMessageId = msg.MessageId;
                    logger.LogInformation($"PinnedMessageId: {msg.MessageId}");

                    db.SaveChanges();
                }
            }
            else if (callbackData.StartsWith("tuneaddress"))
            {
                var ua = db.UserAddress.Include(o => o.User).SingleOrDefault(x => x.Id == int.Parse(callbackData.Substring("tuneaddress ".Length)));
                string text = @$"🚫 Address doesn't exists";
                if (ua != null)
                {
                    text = getAddressText(ua, me, false, true);
                    await messageSender.EditMessage(u.Id, messageId, text, ReplyKeyboards.AddressMenu(ua));
                }
                else
                    await messageSender.EditMessage(u.Id, messageId, text, null);
            }
            else if (callbackData.StartsWith("tran") || callbackData.StartsWith("dlg"))
            {
                var addrId = int.Parse(callbackData.Substring(callbackData.IndexOf(" ") + 1));
                var ua = db.UserAddress.SingleOrDefault(o => o.Id == addrId);
                string text = @$"🚫 Address doesn't exists";
                if (ua != null)
                {
                    if (callbackData.StartsWith("tranon"))
                        ua.NotifyTransactions = true;
                    if (callbackData.StartsWith("tranoff"))
                        ua.NotifyTransactions = false;
                    if (callbackData.StartsWith("dlgon"))
                        ua.NotifyDelegations = true;
                    if (callbackData.StartsWith("dlgoff"))
                        ua.NotifyDelegations = false;
                    db.SaveChanges();
                    text = getAddressText(ua, me, false, true);
                    await messageSender.EditMessage(u.Id, messageId, text, ReplyKeyboards.AddressMenu(ua));
                }
                else
                    await messageSender.EditMessage(u.Id, messageId, text, null);
            }


        }
    }
}
