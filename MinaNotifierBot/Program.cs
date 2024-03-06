using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinaNotifierBot.CryptoCompare;
using MinaNotifierBot.MinaExplorer;
using Model;
using System.Globalization;
using Telegram.Bot;

CreateHostBuilder(args).Build().Run();

IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, builder) => {
                builder.AddJsonFile("Settings.json");
                builder.AddJsonFile($"Settings.{context.HostingEnvironment.EnvironmentName}.json", true);
                builder.AddJsonFile("Settings.Local.json", true);
                builder.AddEnvironmentVariables();
                builder.AddCommandLine(args);
            })
            .ConfigureServices((context, services) => {
                services.Configure<HostOptions>(hostOptions => hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
                services.Configure<TelegramOptions>(context.Configuration.GetSection("Telegram"));
                {
                    var db = new Model.BotDbContext();
                    db.Database.EnsureCreated();
                    db.Database.Migrate();

                    string namesFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mina-names.txt");
                    if (File.Exists(namesFile))
                    {
                        var allNames = System.IO.File.ReadAllLines(namesFile);
                        Dictionary<string, PublicAddress> addrList = db.PublicAddress.ToDictionary(o => o.Address);
                        foreach (var addrName in allNames)
                        {
                            string addr = addrName.Substring(0, 55);
                            string title = addrName.Substring(56);
                            if (addrList.ContainsKey(addr))
                                addrList[addr].Title = title;
                            else
                                addrList[addr] = db.Add(new PublicAddress { Address = addr, Title = title }).Entity;
                        }
                        db.SaveChanges();
                        Console.WriteLine("Total public names: " + addrList.Count.ToString());
                    }
                }
                services.AddDbContext<BotDbContext>();
                services.AddTransient(sp =>
                        new MinaExplorerClient(new HttpClient(), sp.GetRequiredService<ILogger<MinaExplorerClient>>(), context.Configuration));
                services.AddTransient(sp =>
                        new CryptoCompareClient(context.Configuration.GetValue<string>("CryptoCompareToken"), new HttpClient(), sp.GetRequiredService<ILogger<CryptoCompareClient>>()));
                services.AddSingleton<MinaNotifierBot.MinaChartService>();
                services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug).AddConsole().AddFile("Logs/log-{Date}.txt"));
                services.AddSingleton<IFormatProvider>(CultureInfo.GetCultureInfo("en"));
                services.AddSingleton(provider => new TelegramBotClient(provider.GetRequiredService<IOptions<TelegramOptions>>().Value.BotSecret));
                services.AddSingleton<MinaNotifierBot.MessageSender>();
                services.AddHostedService<MinaNotifierBot.BotService>();
                //services.AddHttpClient<MinaNotifierBot.ReleasesClient>();
                services.AddScoped<MinaNotifierBot.ReleasesClient>();
                services.AddHostedService<MinaNotifierBot.ReleasesWorker>();
                services.AddHostedService<MinaNotifierBot.PinnedPriceWorker>();
            });

