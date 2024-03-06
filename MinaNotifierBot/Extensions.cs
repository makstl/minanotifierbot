using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace MinaNotifierBot
{
	public static class Extensions
	{
		public static string AsString(this IReplyMarkup keyboard)
		{
			if (keyboard is InlineKeyboardMarkup)
				return "I:" + string.Join('\n', ((InlineKeyboardMarkup)keyboard).InlineKeyboard.Select(o => string.Join(';', o.Select(k => k.Text + "|" + k.CallbackData))));
			else
				return "R:" + string.Join('\n', ((ReplyKeyboardMarkup)keyboard).Keyboard.Select(o => string.Join(';', o.Select(k => k.Text))));
		}

		public static IReplyMarkup? ReplyMarkupFromString(string? keyboard)
		{
			if (keyboard == null)
				return null;
			if (keyboard.StartsWith("I:"))
				return new InlineKeyboardMarkup(keyboard.Split('\n').Select(o => o.Split(';').Select(b => new InlineKeyboardButton(b.Substring(0, b.IndexOf('|'))) { CallbackData = b.Substring(b.IndexOf('|') + 1) })));
			else
				return new ReplyKeyboardMarkup(keyboard.Split('\n').Select(o => o.Split(';').Select(b => new KeyboardButton(b))));
		}
		public static string ShortAddr(this string addr)
		{
			return addr.Substring(0, 6) + "…" + addr.Substring(50);
		}
		public static string HashTag(this string addr)
		{
			return " #" + addr.Substring(0, 6) + addr.Substring(50);
		}

		public static string Link(this Telegram.Bot.Types.User u)
		{
			return $"[{(u.FirstName + " " + u.LastName).Trim()}](tg://user?id={u.Id}) [[{u.Id}]]";
		}

		public static string MinaToString(this decimal amount)
		{
			return amount.ToString("###,###,###,###,##0.####", CultureInfo.InvariantCulture).Trim() + " MINA";
		}

		public static string MinaToBtc(this decimal amount, MarketData md)
			=> (amount * md.price_btc).ToString("#,###,##0.####", CultureInfo.InvariantCulture).Trim();

		public static string MinaToCurrency(this decimal amount, MarketData md, Currency currency)
		{
			var code = currency.ToString().ToUpper();
			return (amount * md.CurrencyRate(currency))
				.ToString($"###,###,###,###,##0.00 {code}", CultureInfo.InvariantCulture).Trim();
		}
	}
}
