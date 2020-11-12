using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DnsLookup
{
    public struct Data
    {
        Dictionary<string, List<string>> MxRecords;
        List<Email> EmailOutput;
        List<Email> EmailInput;
        List<IgnoredEmails> EmailsToIgnore;

        public Data(Dictionary<string, List<string>> mxRecords, List<Email> emailOutput, List<Email> emailInput, List<IgnoredEmails> emailsToIgnore)
        {
            MxRecords = mxRecords;
            EmailOutput = emailOutput;
            EmailInput = emailInput;
            EmailsToIgnore = emailsToIgnore;
        }
    }

    public class EmailReadMap : ClassMap<Email>
    {
        public EmailReadMap()
        {
            Map(x => x.EmailAddress).Index(0);
            Map(x => x.DomainName).Index(1);
            Map(x => x.FirstName).Index(2);
            Map(x => x.LastName).Index(3);
        }
    }

    public class Email
    {
        public string EmailAddress { get; set; }

        public string DomainName { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public Email() { }

        public Email(string email)
        {
            EmailAddress = email;
            DomainName = ReturnDomain(EmailAddress);
            FirstName = "";
            LastName = "";
        }

        public Email(string email, string domainName, string firstName, string lastName)
        {
            EmailAddress = email;
            DomainName = domainName;
            FirstName = firstName;
            LastName = lastName;
        }

        private string ReturnDomain(string emailAddress) => emailAddress.Substring(emailAddress.IndexOf("@") + 1);
    }

    public class IgnoredEmails
    {
        public string Name;
        public IgnoredEmails(string name)
        {
            Name = name;
        }
    }

    public class IgnoredEmailsMap : ClassMap<IgnoredEmails>
    {
        public IgnoredEmailsMap()
        {
            Map(x => x.Name).Index(0);
        }
    }

    public class TCPClient
    {
        public string TcpInfo { get; set; }
        public string EmailAddress { get; set; }

        public TCPClient()
        {

        }

        public TCPClient(string tcpInfo, string emailAddress)
        {
            TcpInfo = tcpInfo;
            EmailAddress = emailAddress;
        }
    }

    public class JsonParser
    {
        readonly string FileName = "data.json";

        public JsonParser()
        {
            
        }

        public Data Load => JsonConvert.DeserializeObject<Data>(File.ReadAllText(FileName));

        public bool Save(Data data) {
            try
            {
                File.WriteAllText(FileName, JsonConvert.SerializeObject(value: data));
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }

        }
    }
}
