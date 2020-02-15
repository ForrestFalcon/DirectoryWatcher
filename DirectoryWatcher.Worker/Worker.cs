using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DirectoryWatcher.Worker.Config;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace DirectoryWatcher.Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        private readonly DirectoryWatcherConfig directoryWatcherConfig;

        private readonly MailConfig mailConfig;

        private FileSystemWatcher watcher;

        public Worker(ILogger<Worker> logger, IConfiguration config)
        {
            _logger = logger;
            directoryWatcherConfig = config.GetSection("directoryWatcher").Get<DirectoryWatcherConfig>();
            mailConfig = config.GetSection("mailConfig").Get<MailConfig>();

            if(mailConfig == null)
            {
                throw new ArgumentException("MailConfig is null");
            }
        }

        public override async Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DirectoryWatcher service is starting.");

            watcher = new FileSystemWatcher();
            // Watch for changes in LastAccess and LastWrite times, and
            // the renaming of files or directories.
            watcher.NotifyFilter = NotifyFilters.FileName
                                    | NotifyFilters.LastAccess
                                    | NotifyFilters.LastWrite;

            // Only watch text files.
            watcher.Filter = directoryWatcherConfig.Filter;

            watcher.Path = directoryWatcherConfig.Path;

            watcher.Created += OnCreated;

            _logger.LogInformation("Lookup {0} ({1}) for changes.", watcher.Path, watcher.Filter);

            // Begin watching.
            watcher.EnableRaisingEvents = true;

            await base.StopAsync(stoppingToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
             }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DirectoryWatcher service is stopping.");

            watcher?.Dispose();

            await base.StopAsync(stoppingToken);
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation("New File found! Path: {0}", e.FullPath);
            try
            {
                sendMail(e.FullPath);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error sending mail!");
            }
        }

        private void sendMail(string path)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(mailConfig.FromAddress));
            mailConfig.ToAddress.Split(";").ToList().ForEach((address) => {
                message.To.Add(new MailboxAddress(address));
            });

            var builder = new BodyBuilder();
            builder.TextBody = mailConfig.Body;
            builder.Attachments.Add(path);

            message.Subject = mailConfig.Subject;
            message.Body = builder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                // Accept all SSL certificates (in case the server supports STARTTLS)
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                client.Connect(mailConfig.Host, mailConfig.Port, mailConfig.UseSSL);
                client.Authenticate(mailConfig.User, mailConfig.Password);

                client.Send(message);
                client.Disconnect(true);
            }

            _logger.LogInformation("E-Mail sent!");
        }
    }
}
