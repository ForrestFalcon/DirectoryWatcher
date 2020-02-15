using System;
using System.Collections.Generic;
using System.Text;

namespace DirectoryWatcher.Worker.Config
{
    class MailConfig
    {
        public string FromAddress { get; set; }

        public string ToAddress { get; set; }

        public string Subject { get; set; }

        public string Body { get; set; }

        public string Host { get; set; }

        public int Port { get; set; }

        public bool UseSSL { get; set; }

        public string User { get; set; }

        public string Password { get; set; }
    }
}
