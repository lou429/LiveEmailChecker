using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace DnsLookup
{
    public class EmailCheckMap : ClassMap<EmailCheck>
    {
        public EmailCheckMap()
        {
            Map(x => x.EmailAddress).Index(0);
            Map(x => x.Output).Index(1);
        }
    }

    public class EmailCheck
    {
        public string EmailAddress { get; set; }
        public string Output { get; set; }

        public EmailCheck() { }

        public EmailCheck(string email, string result)
        {
            EmailAddress = email;
            Output = result;
        }
    }

    public class EmailReadMap : ClassMap<EmailRead>
    {
        public EmailReadMap()
        {
            Map(x => x.EmailAddress).Index(0);
        }
    }

    public class EmailRead
    {
        public string EmailAddress { get; set; }

        public EmailRead() { }

        public EmailRead(string email)
        {
            EmailAddress = email;
        }
    }
}
