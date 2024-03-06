using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinaNotifierBot.MinaExplorer;
using Model;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MinaNotifierBot
{
	public partial class BotService : BackgroundService
    {
        readonly IServiceProvider serviceProvider;
        readonly ILogger logger;
        readonly TelegramBotClient telegramBotClient;
        readonly IFormatProvider formatProvider;
        readonly MessageSender messageSender;
        readonly MinaChartService chartService;
        readonly long adminChannel;
        readonly long supportGroup;

        public BotService(IServiceProvider serviceProvider, ILogger<BotService> logger, TelegramBotClient telegramBotClient, IFormatProvider formatProvider, MessageSender messageSender, MinaChartService chartService)
		{
			this.serviceProvider = serviceProvider;
			this.telegramBotClient = telegramBotClient;
			this.formatProvider = formatProvider;
			this.logger = logger;
			this.messageSender = messageSender;
			var to = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
			this.adminChannel = to.AdminChannel;
			this.supportGroup = to.SupportGroup;
			this.chartService = chartService;
		}
		bool start = true;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            telegramBotClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, null, stoppingToken);

            while (stoppingToken.IsCancellationRequested is false)
            {
                try
                {
                    await Run(stoppingToken);
                }
                catch (TaskCanceledException tce)
                {
                    logger.LogError(tce, tce.Message);
                }
                catch (Exception e)
                {
                    logger.LogError(e, e.Message);
                    await messageSender.SendAdminMessage("🛑 " + e.GetType().Name + ": " + e.Message);
                    Thread.Sleep(5000);
                }
            }
        }

        MarketData md = new MarketData();
        bool lastBlockChanged = false;
        int lbMessageId;
        DateTime lastMDreceived;
        async Task Run(CancellationToken stoppingToken)
        {
            using var scope = serviceProvider.CreateScope();
            var provider = scope.ServiceProvider;
            using var db = provider.GetRequiredService<BotDbContext>();
            var me = provider.GetRequiredService<MinaExplorerClient>();
            try
            {
                if (DateTime.Now.Subtract(lastMDreceived).TotalMinutes > 20)
                {
                    var md1 = provider.GetRequiredService<CryptoCompare.CryptoCompareClient>().GetMarketData();
                    if (md1.price_btc > 0)
                        md = md1;
                    lastMDreceived = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Market data not received: " + ex.Message);
                await messageSender.SendAdminMessage("😬 Market data not received: " + ex.Message);
                lastMDreceived = DateTime.Now;
            }
            var lb = db.LastBlock.SingleOrDefault();
            lbMessageId = lb?.MessageId ?? 0;
            int lastBlockHeight = lb?.Height ?? 0;
            if (start)
            {
                var bot = await telegramBotClient.GetMeAsync();
                await messageSender.SendAdminMessage($"@{bot.Username} v.{GetType().Assembly.GetName().Version?.ToString(3)} started. {lb?.MessageText}");
                start = false;
            }
            Block? block;
            lastBlockHeight = 291608;
            try
            {
                block = me.GetBlock(lastBlockHeight + 1);
            }
            catch
			{
                try
                {
                    block = me.GetBlock(lastBlockHeight + 2);
                    await messageSender.SendAdminMessage($"Skipped block {lastBlockHeight + 1}");
                }
                catch
				{
                    try
                    {
                        block = me.GetBlock(lastBlockHeight + 3);
                        await messageSender.SendAdminMessage($"Skipped block {lastBlockHeight + 2}");
                    }
                    catch
                    {
                        block = me.GetBlock(lastBlockHeight + 4);
                        await messageSender.SendAdminMessage($"Skipped blocks {lastBlockHeight + 1}, {lastBlockHeight + 2}, {lastBlockHeight + 3}");
                    }
                }
            }
            if (block == null)
                return;
            if (block.blockHeight <= lastBlockHeight)
            {
                Thread.Sleep(15000);
                return;
            }

            logger.LogInformation($"Processing block {block.blockHeight}");
            if (block.transactions.coinbaseReceiverAccount != null)
                WriteCoinBase(db, block.transactions.coinbaseReceiverAccount.publicKey, block.creatorAccount.publicKey);

            await ProcessBlock(block, db, me);
            lastBlockHeight = block.blockHeight;
            if (lastBlockChanged)
			{
                lastBlockChanged = false;
                logger.LogInformation($"Last processed block changed");
                return;
            }
            if (lb != null)
                db.LastBlock.Remove(lb);
            lb = new LastBlock { Height = lastBlockHeight, ProcessedDate = DateTime.Now, MessageId = lbMessageId };
            db.LastBlock.Add(lb);
            db.SaveChanges();
            //if (lbMessageId != 0)
            //{
            //    try
            //    {
            //        await messageSender.EditAdminMessage(lbMessageId, lb.MessageText);
            //    }
            //    catch (Exception e)
            //    {
            //        logger.LogError(e, e.Message);
            //    }
            //}
            logger.LogInformation($"Processed block {block.blockHeight}");
            if (block.last)
                Thread.Sleep(15000);
        }

        async Task ProcessBlock(Block block, BotDbContext db, MinaExplorerClient me)
		{

            var fromList = block.transactions.userCommands.Where(o => o.kind == "PAYMENT").GroupBy(o => o.to).ToList();
            foreach (var to in fromList)
            {
                var to_addr = db.UserAddress.Include(x => x.User).Where(x => x.Address == to.Key && x.NotifyTransactions && !x.User.Inactive).ToList();
                if (to_addr.Count == 0)
                    continue;
                var a = me.GetAccount(to.Key);
                if (a != null)
                    foreach (var ua in to_addr)
                    {
                        foreach (var from in to)
                        {
                            var fromName = (db.UserAddress.FirstOrDefault(o => o.Address == from.from && o.UserId == ua.UserId)?.Title ?? db.PublicAddress.SingleOrDefault(o => o.Address == from.from)?.Title);
                            string fromTag;
                            if (fromName == null)
                            {
                                fromName = from.from.ShortAddr();
                                fromTag = from.from.HashTag();
                            }
                            else
                                fromTag = " #" + System.Text.RegularExpressions.Regex.Replace(fromName.ToLower(), "[^a-zа-я0-9]", "");
                            var payoutDelegateAddr = db.Coinbase.FirstOrDefault(o => o.CoinbaseReceiverAccount == from.from)?.CreatorAccount;
                            var payoutDelegateName = payoutDelegateAddr != null ? db.PublicAddress.SingleOrDefault(o => o.Address == payoutDelegateAddr)?.Title ?? payoutDelegateAddr.ShortAddr() : null;
                            string result = from.from != a.@delegate && payoutDelegateAddr == null ?
                                $"✅ Incoming <a href='{Explorer.FromId(ua.User.Explorer).op(from.hash)}'>transfer</a> of <b>{(from.amount / 1000000000M).MinaToString()}</b> ({(from.amount / 1000000000M).MinaToCurrency(md, Currency.Usd)}) to {ua.Link} from <a href='{Explorer.FromId(ua.User.Explorer).account(from.from)}'>{fromName}</a>" :
                                $"🤑 New <a href='{Explorer.FromId(ua.User.Explorer).op(from.hash)}'>payout</a> of <b>{(from.amount / 1000000000M).MinaToString()}</b> ({(from.amount / 1000000000M).MinaToCurrency(md, Currency.Usd)}) to {ua.Link} from <a href='{Explorer.FromId(ua.User.Explorer).account(from.from)}'>{fromName}</a>";
                            string validatorTag = "";
                            if (payoutDelegateAddr != null)
                            {
                                if (payoutDelegateName == null)
                                {
                                    payoutDelegateName = payoutDelegateAddr.ShortAddr();
                                    validatorTag = payoutDelegateAddr.HashTag();
                                }
                                else
                                {
                                    validatorTag = " #" + System.Text.RegularExpressions.Regex.Replace(payoutDelegateName.ToLower(), "[^a-zа-я0-9]", "");
                                }
                                result += $" payout address related to <a href='{Explorer.FromId(ua.User.Explorer).account(payoutDelegateAddr)}'>{payoutDelegateName}</a> validator";
                            }
                            result += "\n\n<b>Balance</b>: " + a.balance.total.MinaToString();
                            if (!ua.User.HideHashTags)
                                result += (from.from != a.@delegate ? "\n\n#incoming" : "\n\n#payout") + ua.HashTag() + fromTag + validatorTag;
                            await messageSender.SendMessage(ua.UserId, result, ReplyKeyboards.MainMenu);
                        }
                    }
                else
                    return;
            }

            var toList = block.transactions.userCommands.Where(o => o.kind == "PAYMENT").GroupBy(o => o.from).ToList();
            foreach (var from in toList)
            {
                var from_addr = db.UserAddress.Include(x => x.User).Where(x => x.Address == from.Key && x.NotifyTransactions && !x.User.Inactive).ToList();
                if (from_addr.Count == 0)
                    continue;
                var a = me.GetAccount(from.Key);
                if (a != null)
                    foreach (var ua in from_addr)
                    {
                        foreach (var to in from)
                        {
                            var toName = (db.UserAddress.FirstOrDefault(o => o.Address == to.to && o.UserId == ua.UserId)?.Title ?? db.PublicAddress.SingleOrDefault(o => o.Address == to.to)?.Title);
                            string toTag;
                            if (toName == null)
                            {
                                toName = to.to.ShortAddr();
                                toTag = to.to.HashTag();
                            }
                            else
                                toTag = " #" + System.Text.RegularExpressions.Regex.Replace(toName.ToLower(), "[^a-zа-я0-9]", "");
                            string result = $"❎ Outgoing <a href='{Explorer.FromId(ua.User.Explorer).op(to.hash)}'>transfer</a> of <b>{(to.amount / 1000000000M).MinaToString()}</b> ({(to.amount / 1000000000M).MinaToCurrency(md, Currency.Usd)}) from {ua.Link} to <a href='{Explorer.FromId(ua.User.Explorer).account(to.to)}'>{toName}</a>";
                            result += "\n\n<b>Balance</b>: " + a.balance.total.MinaToString();
                            if (!ua.User.HideHashTags)
                                result += "\n\n#outgoing" + ua.HashTag() + toTag;
                            await messageSender.SendMessage(ua.UserId, result, ReplyKeyboards.MainMenu);
                        }
                    }
                else
                    return;
            }

            foreach (var c in block.transactions.userCommands)
            {
                var amount = c.amount / 1000000000M;
                if (c.kind == "PAYMENT" && amount >= 250000M)
                {
                    foreach (var u in db.User.Where(o => o.WhaleAlertThreshold > 0 && o.WhaleAlertThreshold < amount && !o.Inactive))
                    {
                        var fromName = (db.UserAddress.FirstOrDefault(o => o.Address == c.from && o.UserId == u.Id)?.Title ?? db.PublicAddress.SingleOrDefault(o => o.Address == c.from)?.Title);
                        string fromTag;
                        if (fromName == null)
                        {
                            fromName = c.from.ShortAddr();
                            fromTag = c.from.HashTag();
                        }
                        else
                            fromTag = " #" + System.Text.RegularExpressions.Regex.Replace(fromName.ToLower(), "[^a-zа-я0-9]", "");
                        var toName = (db.UserAddress.FirstOrDefault(o => o.Address == c.to && o.UserId == u.Id)?.Title ?? db.PublicAddress.SingleOrDefault(o => o.Address == c.to)?.Title);
                        string toTag;
                        if (toName == null)
                        {
                            toName = c.to.ShortAddr();
                            toTag = c.to.HashTag();
                        }
                        else
                            toTag = " #" + System.Text.RegularExpressions.Regex.Replace(toName.ToLower(), "[^a-zа-я0-9]", "");

                        string result = $"🐋 Whale <a href='{Explorer.FromId(u.Explorer).op(c.hash)}'>transfer</a> of <b>{amount.MinaToString()}</b> ({amount.MinaToCurrency(md, Currency.Usd)}) from <a href='{Explorer.FromId(u.Explorer).account(c.from)}'>{fromName}</a> to <a href='{Explorer.FromId(u.Explorer).account(c.to)}'>{toName}</a>";

                        if (!u.HideHashTags)
                            result += "\n\n#whale" + fromTag + toTag;
                        await messageSender.SendMessage(u.Id, result, ReplyKeyboards.MainMenu);
                    }

                }
                if (c.kind == "STAKE_DELEGATION")
                {
                    var dlg_addr = db.UserAddress.Include(x => x.User).Where(x => x.Address == c.to && x.NotifyDelegations && !x.User.Inactive).ToList();
                    foreach (var ua in dlg_addr)
                    {
                        var f = me.GetAccount(c.from);
                        var t = me.GetAccount(c.to);
                        if (f != null && t != null)
                        {
                            var fromName = (db.UserAddress.FirstOrDefault(o => o.Address == c.from && o.UserId == ua.UserId)?.Title ?? db.PublicAddress.SingleOrDefault(o => o.Address == c.from)?.Title);
                            string fromTag;
                            if (fromName == null)
                            {
                                fromName = c.from.ShortAddr();
                                fromTag = c.from.HashTag();
                            }
                            else
                                fromTag = " #" + System.Text.RegularExpressions.Regex.Replace(fromName.ToLower(), "[^a-zа-я0-9]", "");
                            string result = $"🤝 New <a href='{Explorer.FromId(ua.User.Explorer).op(c.hash)}'>delegation</a> of <b>{f.balance.total.MinaToString()}</b> to {ua.Link} from <a href='{Explorer.FromId(ua.User.Explorer).account(c.from)}'>{fromName}</a>";
                            result += $"\n\n<b>Next epoch staking balance:</b> {t.nextEpochTotalStakingBalance.MinaToString()}";
                            if (!ua.User.HideHashTags)
                                result += "\n\n#delegation" + ua.HashTag() + fromTag;
                            await messageSender.SendMessage(ua.UserId, result, ReplyKeyboards.MainMenu);
                        }
                        else
                            return;
                    }
                    //var user_addr = db.UserAddress.Include(x => x.User).Where(x => x.Address == c.from && x.NotifyDelegations).ToList();
                    //foreach (var ua in user_addr)
                    //{
                    //    var f = me.GetAccount(c.to);
                    //    if (f != null)
                    //    {
                    //        string result = $"🤝 New <a href='{Explorer.FromId(ua.User.Explorer).op(c.hash)}'>delegation</a> of <b>{f.balance.total.MinaToString()}</b> from {ua.Link} to <a href='{Explorer.FromId(ua.User.Explorer).account(c.to)}'>{c.to.ShortAddr()}</a>";
                    //        if (!ua.User.HideHashTags)
                    //            result += "\n\ndelegation" + ua.HashTag() + c.from.HashTag();
                    //        await messageSender.SendMessage(ua.UserId, result, ReplyKeyboards.MainMenu);
                    //    }
                    //    else
                    //        return;
                    //}
                }
            }
        }

		private void WriteCoinBase(BotDbContext db, string coinbaseReceiverAccount, string creatorAccount)
		{
            if (coinbaseReceiverAccount == creatorAccount)
                return;
            if (db.Coinbase.Any(o => o.CoinbaseReceiverAccount == coinbaseReceiverAccount && o.CreatorAccount == creatorAccount))
                return;
            db.Coinbase.Add(new Coinbase { CoinbaseReceiverAccount = coinbaseReceiverAccount, CreatorAccount = creatorAccount });
            db.SaveChanges();
		}

		async Task OnSql(System.Data.Common.DbConnection conn, string sql)
        {
            try
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                var result = new List<string[]>();
                try
                {
                    conn.Open();
                    using var reader = cmd.ExecuteReader();

                    if (reader.HasRows is false)
                    {
                        result.Add(new[] { $"{reader.RecordsAffected} records affected" });
                    }
                    else
                    {
                        var data = new string[reader.FieldCount];
                        for (var i = 0; i < data.Length; i++)
                            data[i] = reader.GetName(i);

                        result.Add(data);
                        while (reader.Read())
                        {
                            data = new string[reader.FieldCount];
                            for (var i = 0; i < data.Length; i++)
                                data[i] = reader.GetValue(i)?.ToString() ?? "NULL";
                            result.Add(data);
                        }
                    }
                }
                finally
                {
                    conn.Close();
                }
                string allData = String.Join("\r\n", result.Select(o => String.Join(';', o)).ToArray());
                if (result[0].Length <= 3 && result.Count <= 20)
                    await messageSender.SendAdminMessage(allData.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("`", "\\`"));
                else
                {
                    string fileName = "result.txt";
                    if (allData.Length > 100000)
                    {
                        using (var zip = new MemoryStream())
                        {
                            using (var archive = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Create, true))
                            {
                                var entry = archive.CreateEntry("result.txt");
                                using (StreamWriter writer = new StreamWriter(entry.Open()))
                                    writer.Write(allData);
                            }
                            zip.Seek(0, SeekOrigin.Begin);
                            var f = Telegram.Bot.Types.InputFile.FromStream(zip, fileName + ".zip");
                            await telegramBotClient.SendDocumentAsync(adminChannel, f);
                        }
                    }
					else
					{
                        var f = Telegram.Bot.Types.InputFile.FromStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(allData)), fileName);
                        await telegramBotClient.SendDocumentAsync(adminChannel, f);
                    }

                }
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
                await messageSender.SendAdminMessage(e.Message);
            }
        }
        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type != UpdateType.CallbackQuery &&
                update.Type != UpdateType.ChannelPost &&
                update.Type != UpdateType.Message)
                return;
            try
            {
                using var scope = serviceProvider.CreateScope();
                var provider = scope.ServiceProvider;
                using var db = provider.GetRequiredService<BotDbContext>();
                var me = provider.GetRequiredService<MinaExplorerClient>();
                Model.User u;

                if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null && update.CallbackQuery.Message != null)
                {
                    u = db.User.Single(x => x.Id == update.CallbackQuery.From.Id);
                    var callbackData = update.CallbackQuery.Data ?? "";
                    logger.LogInformation("Callback from " + update.CallbackQuery.From.FirstName + " " + update.CallbackQuery.From.LastName + ": " + callbackData);
                    await HandleCallbackQuery(u, db, me, callbackData, update.CallbackQuery.Message.MessageId);
                    await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                    return;
                }

                if (update.Type == UpdateType.ChannelPost &&
                    update.ChannelPost != null &&
                    update.ChannelPost?.Chat.Id == adminChannel &&
                    update.ChannelPost.Text != null)
                {
                    var text = update.ChannelPost.Text;
                    if (text.StartsWith("/sql"))
                    {
                        await OnSql(db.Database.GetDbConnection(), text.Substring("/sql".Length));
                    }
                    if (text == "/block")
                    {
                        var lb = db.LastBlock.SingleOrDefault();
                        if (lb != null)
                        {
                            var msg = await messageSender.SendAdminMessage(lb.MessageText);
                            lb.MessageId = msg.MessageId;
                            lbMessageId = msg.MessageId;
                            db.SaveChanges();
                        }
                    }
                    if (text.StartsWith("/setblock"))
					{
                        var height = int.Parse(text.Substring("/setblock".Length).Trim());
                        var lb = db.LastBlock.SingleOrDefault();
                        if (lb != null)
                            db.LastBlock.Remove(lb);
                        lb = new LastBlock { Height = height - 1, ProcessedDate = DateTime.Now, MessageId = lb?.MessageId ?? 0 };
                        db.LastBlock.Add(lb);
                        db.SaveChanges();
                        lastBlockChanged = true;
                        await messageSender.SendAdminMessage($"Last processed block changed to {height - 1}");
                    }
                    if (text == "/stat")
                    {
                        string result = "";
                        result += $"Total users: {db.User.Count()}\n";
                        result += $"Active users: {db.User.Count(o => !o.Inactive)}\n";
                        result += $"Users with 🐋: {db.User.Count(o => !o.Inactive && o.WhaleAlertThreshold > 0)}\n";
                        result += $"Monitored addresses: {db.UserAddress.Count(o => !o.User.Inactive)}\n";
                        await messageSender.SendAdminMessage(result);
                    }
                }

                if (update.Message?.Text == "/chatid")
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"id: {update.Message.Chat.Id}");

                if (update.Type != UpdateType.Message)
                    return;

                if (update.Message!.Type != MessageType.Text)
                    return;

                if (update.Message.Chat.Id == supportGroup &&
                    update.Message.ReplyToMessage != null &&
                    update.Message.ReplyToMessage.Entities?.Length > 0 &&
                    update.Message.ReplyToMessage.Entities[0].User != null &&
                    update.Message.Text != null)
				{
                    await messageSender.SendMessage(update.Message.ReplyToMessage.Entities[0].User?.Id ?? 0, "📩 Message from support:\n\n" + update.Message.Text, ReplyKeyboards.MainMenu);
                    await botClient.SendTextMessageAsync(supportGroup, "📤 Message delivered", parseMode: ParseMode.Markdown, disableWebPagePreview: true);
                }
                if (update.Message.Chat.Type != ChatType.Private || update.Message.From == null)
                    return;
                var chatId = update.Message.Chat.Id;
                var messageText = update.Message.Text ?? "";
                logger.LogInformation($"{update.Message.From.FirstName} {update.Message.From.LastName} {update.Message.From.Username}: {messageText}");
                u = db.User.SingleOrDefault(x => x.Id == chatId) ?? new Model.User {
                    CreateDate = DateTime.Now,
                    Firstname = update.Message.Chat.FirstName ?? "",
                    Lastname = update.Message.Chat.LastName ?? "",
                    Title = update.Message.Chat.Title ?? "",
                    Username = update.Message.Chat.Username ?? "",
                    WhaleAlertThreshold = 1000000,
                    ReleaseNotify = true
                };

                async Task newAddr()
                {
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                    string addr = Regex.Matches(messageText, MinaConstants.AddressPattern).First().Value;
                    var name = messageText.Replace(addr, "").Trim();
                    if (name == String.Empty)
                        name = addr.ShortAddr();

                    var a = me.GetAccount(addr);
                    if (a == null)
                        await messageSender.SendMessage(chatId, @$"😮 No account found for that public key: {addr}", ReplyKeyboards.MainMenu);
                    else
                    {
                        if (addr.ShortAddr() == name && a.username != null && a.username != "Unknown")
                        {
                            var pa = db.PublicAddress.SingleOrDefault(o => o.Address == addr);
                            if (pa != null)
                                name = pa.Title;
                            else
                                name = a.username;
                        }

                        var ua = db.UserAddress.SingleOrDefault(x => x.UserId == u.Id && x.Address == addr);
                        if (ua == null)
                        {
                            ua = new UserAddress { Address = addr, User = u, UserId = u.Id, NotifyDelegations = true, NotifyTransactions = true };
                            db.UserAddress.Add(ua);
                        }
                        ua.Title = name;
                        db.SaveChanges();
                        string text = "✅ New address added!\n" + getAddressText(ua, me, true, true) + "\n\nYou will receive notifications on any events.";
                        if (!u.HideHashTags)
                            text += "\n\n#added" + ua.HashTag();
                        await messageSender.SendMessage(chatId, text, ReplyKeyboards.ViewAddressMenu(ua));

                        await messageSender.SendAdminMessage($"➕ user {update.Message.From.Link()} added {ua.Address}");
                    }
                }

                bool start = false;
                if (u.Id == 0)
                {
                    u.Id = chatId;
                    u.Explorer = 2;
                    db.User.Add(u);
                    db.SaveChanges();
                    await messageSender.SendAdminMessage("✨ New user " + update.Message.From.Link());
                    start = true;
                }

                if (start || messageText.StartsWith("/start"))
                {
                    if (start || !Regex.IsMatch(messageText, MinaConstants.AddressPattern))
                    {
                        u.UserState = UserState.Default;
                    db.SaveChanges();
                    await messageSender.SendMessage(chatId, @$"💚 Welcome {(u.Firstname + " " + u.Lastname).Trim()}!

With Mina Notifier Bot you can easily monitor various events in Mina blockchain, like transactions, delegations, governance, etc.


💡 <b>First steps</b>:
 - click the ✳️ <b>New address</b> button and type the Mina address you want to follow. Use the ♏ <b>My Addresses</b> button to manage address list and special settings.
 - or simply do nothing and you will be notified about 🐋 <b>whale transactions</b>, which you can disable or configure using ⚙️ <b>Settings</b> button",
     ReplyKeyboards.MainMenu);

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
                    if (Regex.IsMatch(messageText, MinaConstants.AddressPattern))
                    {
                        messageText = messageText.Replace("/start ", "").Trim();
                        await newAddr();
                    }
                }
                else if (messageText == "/unpin")
				{
                    await telegramBotClient.UnpinAllChatMessages(u.Id, cancellationToken);
                    if (u.PinnedMessageId > 0)
                        await telegramBotClient.DeleteMessageAsync(u.Id, u.PinnedMessageId);
                    u.PinnedMessageId = 0;
                    db.SaveChanges();
                }
                else if (messageText.StartsWith("/sql"))
                {
                    await OnSql(db.Database.GetDbConnection(), messageText.Substring("/sql".Length));
                }
                else if (messageText == "/info")
				{
                    await messageSender.SendMessage(chatId, $"1 MINA = {md.price_usd}$", ReplyKeyboards.MainMenu);
                }
                else if (messageText == ReplyKeyboards.CmdNewAddress)
                {
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                    await messageSender.SendMessage(chatId, @"Send me your Mina address you want to monitor and the title for this address (optional). For example:

<i>B62qm7vP2JPj1d8XDmGUiv3GtwAfzuaxrdNsiXdWmZ7QqXZtzpVyGPG Gate</i>", ReplyKeyboards.MainMenu);
                }
                else if (messageText == ReplyKeyboards.CmdMyAddresses)
                {
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                    var addresses = db.UserAddress.Where(x => x.UserId == u.Id).ToList();
                    if (addresses.Count == 0)
                        await messageSender.SendMessage(chatId, @$"You have no addresses", ReplyKeyboards.MainMenu);
                    else
                    {
                        await botClient.SendChatActionAsync(chatId, ChatAction.Typing);
                        foreach (var ua in addresses)
                            await messageSender.SendMessage(chatId, getAddressText(ua, me, false, false), ReplyKeyboards.ViewAddressMenu(ua));
                    }
                }
                else if (messageText == ReplyKeyboards.CmdContactUs)
                {
                    u.UserState = UserState.Support;
                    db.SaveChanges();
                    await messageSender.SendMessage(u.Id, "Please, write here your message", ReplyKeyboards.BackMenu);
                }
                else if (messageText == ReplyKeyboards.CmdSettings)
                {
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                    await botClient.SendTextMessageAsync(chatId, @$"Settings", parseMode: ParseMode.Html, replyMarkup: ReplyKeyboards.Settings(u), cancellationToken: cancellationToken);
                }
                else if (Regex.IsMatch(messageText, MinaConstants.AddressPattern))
                {
                    await newAddr();
                }
                else if (messageText == ReplyKeyboards.CmdBack)
				{
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                    await messageSender.SendMessage(chatId, "🙋 Ok, see you later", ReplyKeyboards.MainMenu);
                }
                else if (u.UserState == UserState.Support)
                {
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                    
                    var message = "💌 Message from " + update.Message.From.Link() + ":\n" + messageText
                        .Replace("_", "__")
                        .Replace("`", "'").Replace("*", "**").Replace("[", "(").Replace("]", ")");

                    await botClient.SendTextMessageAsync(supportGroup, message, parseMode: ParseMode.Markdown, disableWebPagePreview: true);

                    await messageSender.SendMessage(chatId, "Message sent. Thanks for contacting 💛", ReplyKeyboards.MainMenu);
                }
                else if (messageText == "/releases_off")
				{
                    u.ReleaseNotify = false;
                    db.SaveChanges();
                    await messageSender.SendMessage(chatId, $"Software release notifications are turned off. You can turn it back on in the <b>{ReplyKeyboards.CmdSettings}</b> menu.", ReplyKeyboards.MainMenu);
                }
                else
				{
                    await messageSender.SendMessage(chatId, "🙈 Command not recognized", ReplyKeyboards.MainMenu);
                }
            }
            catch(Exception ex)
			{
                logger.LogError(ex, ex.Message);
                await messageSender.SendAdminMessage("🛑 " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        string getAddressText(UserAddress ua, MinaExplorerClient me, bool disableHashTags, bool detailed)
        {
            var a = me.GetAccount(ua.Address);
            string result = "";
            if (a?.@delegate == ua.Address)
            {
                result += "👑 ";
                ua.IsDelegate = true;
            }
            if (ua.Address.ShortAddr() != ua.Title)
                result += $"<b>{ua.Title}</b>\n";
            result += @$"<a href='{Explorer.FromId(ua.User.Explorer).account(ua.Address)}'>{ua.Address}</a>

<b>Balance</b>: {a?.balance.total.MinaToString()} ({a?.balance.total.MinaToCurrency(md, Currency.Usd)} / {a?.balance.total.MinaToBtc(md)} BTC)";
            //if (a?.balance.lockedBalance != null)
            //    result += $"\n<b>Locked</b>: {a?.balance.lockedBalance.Value.MinaToString()}";
            if (a?.@delegate != ua.Address && a?.@delegate != null)
            {
                var d = me.GetAccount(a.@delegate) ?? new Account { username = a.@delegate };
                result += $"\n<b>Delegate:</b> <a href='{Explorer.FromId(ua.User.Explorer).account(a.@delegate)}'>{(d.username != null && d.username != "Unknown" ? d.username : a.@delegate.ShortAddr())}</a>";
            }
			else if (a?.@delegate == ua.Address)
			{
				result += $"\n<b>Staking Balance</b>: {a?.epochTotalStakingBalance.MinaToString()} ({a?.epochTotalStakingBalance.MinaToCurrency(md, Currency.Usd)} / {a?.epochTotalStakingBalance.MinaToBtc(md)} BTC)";
                result += $"\n<b>Delegators</b>: {a?.epochDelegators?.Count}";
            }
            else if (a?.@delegate != ua.Address)
                result += "\n<b>Delegate:</b> not delegated";
            result += "\n\nEvents: ";
            string events = "";
            if (!detailed)
            {
                if (ua.NotifyTransactions)
                    events += "✅❎";
                if (ua.IsDelegate && ua.NotifyDelegations)
                    events += "🤝";
            }
			else
			{
                events += "\n✅❎ Transactions: " + (ua.NotifyTransactions ? "🔔 on" : "🔕 off");
                if (ua.IsDelegate)
                    events += "\n🤝 Delegations: " + (ua.NotifyDelegations ? "🔔 on" : "🔕 off");
            }
            if (events == "")
                events = "none";
            result += events;
            if (!disableHashTags && !ua.User.HideHashTags)
                result += "\n\n" + ua.HashTag();
            return result;
        }

        Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            logger.LogError(exception, ErrorMessage);
            return Task.CompletedTask;
        }
    }
}
