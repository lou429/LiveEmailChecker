using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DnsLookup
{
    public struct Data
    {
        Dictionary<string, List<string>> MxRecords;
        List<Email> EmailInput;
        List<EmailOutput> EmailOutput;
        List<IgnoredEmails> EmailsToIgnore;

        public Data(Dictionary<string, List<string>> mxRecords, List<EmailOutput> emailOutput, List<Email> emailInput, List<IgnoredEmails> emailsToIgnore)
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
            DomainName = email.ReturnDomain();
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
    }

    public class EmailOutputMap : ClassMap<EmailOutput>
    {
        public EmailOutputMap()
        {
            Map(x => x.EmailAddress);
            Map(x => x.CompanyName);
            Map(x => x.FirstName);
            Map(x => x.LastName);
        }
    }

    public class EmailOutput
    {
        public string EmailAddress { get; set; }

        public string CompanyName { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public EmailOutput() { }

        public EmailOutput(string email)
        {
            EmailAddress = email;
            CompanyName = email.ReturnDomain().GetTitle().ClearTitleResult();
            FirstName = "";
            LastName = "";
        }

        public EmailOutput(string email, string domainName, string firstName, string lastName)
        {
            EmailAddress = email;
            CompanyName = domainName;
            FirstName = firstName;
            LastName = lastName;
        }

        public EmailOutput(Email email)
        {
            EmailAddress = email.EmailAddress;
            CompanyName = email.DomainName.GetTitle().ClearTitleResult();
            FirstName = email.FirstName;
            LastName = email.LastName;
        }
    }

    public static class GetHtml
    {
        public static string GetTitle(this string url)
        {
            try
            {
                var html = new HtmlWeb();
                if (!url.StartsWith("www."))
                    url = $"www.{url}";
                return html.Load(url).DocumentNode.SelectSingleNode("html/head/title").InnerText.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Console.WriteLine($"Failed to retrieve title for {url}");
                return "";
            }
        }
    }

    public static class ReturnText
    {
        public static string ReturnDomain(this string emailAddress) => emailAddress.Substring(emailAddress.IndexOf("@") + 1);

        public static string ClearTitleResult(this string title)
        {
            return title;
        }
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

    public static class CsvParser
    {
        public static List<Email> LoadEmail(string fileName)
        {
            if (!fileName.EndsWith(".csv"))
                fileName += ".csv";
            try
            {
                using (var reader = new StreamReader($"CSV/{fileName}"))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        csv.Configuration.HasHeaderRecord = true;
                        csv.Configuration.MissingFieldFound = null;
                        csv.Configuration.RegisterClassMap<EmailReadMap>();
                        return csv.GetRecords<Email>().ToList();
                    }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null; 
            }
        }

        public static List<EmailOutput> LoadEmailOutput(string fileName)
        {
            if (!fileName.EndsWith(".csv"))
                fileName += ".csv";
            try
            {
                using (var reader = new StreamReader($"CSV/{fileName}"))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Configuration.HasHeaderRecord = true;
                    csv.Configuration.MissingFieldFound = null;
                    csv.Configuration.RegisterClassMap<EmailOutputMap>();
                    return csv.GetRecords<EmailOutput>().ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public static List<IgnoredEmails> LoadDomain(string fileName)
        {
            if (!fileName.EndsWith(".csv"))
                fileName += ".csv";
            try
            {
                using (var reader = new StreamReader($"CSV/{fileName}"))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Configuration.HasHeaderRecord = true;
                    csv.Configuration.MissingFieldFound = null;
                    csv.Configuration.RegisterClassMap<IgnoredEmailsMap>();
                    return csv.GetRecords<IgnoredEmails>().ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        public static void SaveToCsv(this List<EmailOutput> emailList, string fileName)
        {
            if (!fileName.EndsWith(".csv"))
                fileName += ".csv";
            try
            {
                using (var writer = new StreamWriter($"CSV/{fileName}"))
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                        csv.WriteRecords(emailList);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}
