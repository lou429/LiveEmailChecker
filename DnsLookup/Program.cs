using DnsClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CsvHelper;
using System.Globalization;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace DnsLookup
{
    class Program
    {
        public static Data Data;
        static void Main(string[] args)
        {
            //CheckDns();
            try
            {
                Data = new JsonParser().Load;
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            Dictionary<string, List<string>> mxRecords = new Dictionary<string, List<string>>();

            //Add existing output to output list to save old records
            List<EmailOutput> checkedEmails = new List<EmailOutput>();
            var list = CsvParser.LoadEmailOutput("Checked Emails");
            if (list != null)
                checkedEmails = list;

            Console.WriteLine($"\nLoaded {checkedEmails.Count} existing records");

            //Load all emails that need to be checked
            List<Email> emailsToCheck = new List<Email>();
            var tempEmailList = CsvParser.LoadEmail("Email list");
            if (tempEmailList == null)
            {
                Console.WriteLine("Cannot find 'Email list.csv'\nPlease add it to the CSV folder");
                Console.ReadKey();
            }
            else
                emailsToCheck.AddRange(tempEmailList);

            var tempList = new List<Email>();
            foreach (var email in emailsToCheck)
                if (email.EmailAddress != "" && email.DomainName == "" && email.FirstName == "" && email.LastName == "")
                    tempList.Add(new Email(email.EmailAddress));
                else
                    tempList.Add(email);

            emailsToCheck = tempList;


            List<IgnoredEmails> ignorePrefix = new List<IgnoredEmails>();
            try
            {
                ignorePrefix.AddRange(CsvParser.LoadDomain("Prefix - Common.csv"));

                if (ignorePrefix.Count > 0)
                {
                    tempList = emailsToCheck;
                    foreach (var email in emailsToCheck.ToList())
                        foreach (var prefix in ignorePrefix)
                            if (returnName(email.EmailAddress) == prefix.Name)
                                tempList.Remove(email);

                    emailsToCheck = tempList;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            //Remove emails that already have been checked
            try
            {
                var tempCheckList = checkedEmails;
                foreach (var existingEmail in tempCheckList)
                    foreach (var email in emailsToCheck)
                        if (Compare(existingEmail, email))
                            Console.WriteLine($"Removed {email.EmailAddress} : x{checkedEmails.RemoveAll(x => x.EmailAddress == email.EmailAddress)}");
                checkedEmails.Distinct().ToList();
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            mxRecords = GetMxRecords(ref emailsToCheck);

            foreach (var record in mxRecords)   
            {
                Console.WriteLine(record.Key);
                foreach (var mxRecord in record.Value)
                {
                    if (mxRecord.Contains("no record"))
                        mxRecords[record.Key].Remove(mxRecord);
                    else
                        Console.WriteLine(mxRecord);
                }
            }
            var newlyAddedCounter = 0;
            mxRecords = getTcpInfo(mxRecords);
            //Check emails against domain to see if they match, then run telnet
            //Console.WriteLine($"\nChecking 0/{emailsToCheck.Count}");
            foreach(var email in emailsToCheck)
            {
                Console.WriteLine($"Checking {emailsToCheck.IndexOf(email) + 1}/{emailsToCheck.Count}\nEmail: {email.EmailAddress}");

                try
                {
                    foreach (var tcpRecord in mxRecords[email.DomainName])
                    {
                        Func<bool> telnetCheck = new Func<bool>(() => IsEmailAccountValid(new TCPClient(tcpRecord, email.EmailAddress)));
                        //if (RunTaskWithTimeout(telnetCheck, 10))
                        if(true)
                        {
                            checkedEmails.Add(new EmailOutput(email));
                            newlyAddedCounter++;
                            Thread.Sleep(20);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Skipped domain {email.DomainName}\nNot in domain list");
                    Debug.WriteLine(ex.Message);
                }
            }

            Console.WriteLine($"\nDomains for emails found {checkedEmails.Count}/{emailsToCheck.Count}\nMissing: {emailsToCheck.Count - newlyAddedCounter}");

            checkedEmails = checkedEmails.Distinct().ToList();

            checkedEmails.SaveToCsv("Live emails");

            Console.WriteLine($"\nSaved {checkedEmails.Count} records");
            Console.WriteLine("Program ended\nRead key");

            Data = new Data(mxRecords, checkedEmails, emailsToCheck, ignorePrefix);
            new JsonParser().Save(Data);
            Console.ReadKey(true);
        }

        public static bool Compare(EmailOutput emailOutput, Email email)
        {
            return emailOutput.EmailAddress == email.EmailAddress && emailOutput.FirstName == email.FirstName && emailOutput.LastName == email.LastName;
        }

        private static string returnName(string emailAddress)
        {
            try
            {
                return emailAddress.Substring(0,emailAddress.IndexOf("@"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return "";
            }
        }

        /// <summary>
        /// Return the domain of an email address
        /// </summary>
        /// <param name="email">Email address</param>
        /// <returns></returns>
        public static string ReturnDomain(string email)
        {
            try
            {
                return email.Substring(email.IndexOf("@") + 1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return "";
            }
        }

        /// <summary>
        /// Return TCP info from MX Records
        /// </summary>
        /// <param name="mxRecords">Mx Record list</param>
        /// <returns></returns>
        public static Dictionary<string, List<string>> getTcpInfo(Dictionary<string, List<string>> mxRecords)
        {
            var result = new Dictionary<string, List<string>>();

            foreach (var mxList in mxRecords)
            {
                var recordList = new List<string>();

                foreach (var record in mxList.Value)
                {
                    try
                    {
                        int firstIndex = record.LastIndexOf(mxList.Key);
                        int lastIndex = record.LastIndexOf('.');
                        recordList.Add(record.Substring(firstIndex, lastIndex - firstIndex));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }

                if(recordList.Count > 0)
                    result.Add(mxList.Key, recordList);
            }

            return result;
        }

        /// <summary>
        /// Return DNS records of certain domain
        /// </summary>
        static void CheckDns()
        {
            using (var reader = new StreamReader(@"domains.csv"))
            {
                while (!reader.EndOfStream)
                {
                    var value = reader.ReadLine().Split(";");
                    var dnsVal = value[0];
                    string output = PowerShellCommand($"nslookup {dnsVal}");
                    output = output.Replace("Server:  UnKnown", "");
                    Console.WriteLine($"\n----------------\n{output}");
                    Thread.Sleep(100);
                }
            }
        }

        /// <summary>
        /// Run a hidden powershell window
        /// </summary>
        /// <param name="dns">Address of DNS</param>
        /// <returns>Return Powershell output</returns>
        static string PowerShellCommand(string dns)
        {
            var psCommandBase64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(dns));

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy unrestricted -EncodedCommand {psCommandBase64}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            proc.Start();

            string output = "";

            while(!proc.StandardOutput.EndOfStream)
                output += proc.StandardOutput.ReadLine() + "\n";          

            return output;
        }

        /// <summary>
        /// Return a list of MX records
        /// </summary>
        /// <param name="emailList">List of eamils to check MX records from</param>
        /// <returns></returns>
        static public Dictionary<string, List<string>> GetMxRecords(ref List<Email> emailList)
        {
            var OutputList = new Dictionary<string, List<string>>();
            var ExcludedDomainsList = new List<IgnoredEmails>();

            var list = CsvParser.LoadDomain("Domains - Customers");
            if(list != null)
                ExcludedDomainsList.AddRange(list);
            list = CsvParser.LoadDomain("Domains - Common");
            if (list != null)
                ExcludedDomainsList.AddRange(list);
            list = CsvParser.LoadDomain("Domains - AV Blacklist");
            if (list != null)
                ExcludedDomainsList.AddRange(list);
            list = CsvParser.LoadDomain("Domains - Supplier list");
            if (list != null)
                ExcludedDomainsList.AddRange(list);

            var domainList = new List<string>();

            foreach (var email in emailList)
                domainList.Add(ReturnDomain(email.EmailAddress));

            domainList = domainList.Distinct().ToList();

            //Remove domains that are in the excluded list
            var tempDomainList = domainList;
            var removedDomainsList = new List<string>();

            if (ExcludedDomainsList != null)
                foreach (var excludedDomain in ExcludedDomainsList)
                    foreach (var domain in domainList.ToList().Where(domain => excludedDomain.Name == domain))
                    {
                        tempDomainList.Remove(domain);
                        removedDomainsList.Add(domain);
                    }

            Console.WriteLine($"Removed {domainList.Count - tempDomainList.Count} domains");
            domainList = tempDomainList;

            var tempEmailList = emailList;

            foreach (var email in emailList.ToList())
                foreach (var removedDomain in removedDomainsList)
                    if (ReturnDomain(email.EmailAddress) == removedDomain)
                        tempEmailList.Remove(email);

            Console.WriteLine($"Removed {emailList.Count - tempEmailList.Count} emails");
            emailList = tempEmailList;

            foreach (var record in domainList)
            {
                try
                {
                    //string rec = ReturnDomain(record);
                    Func<List<string>> getMxRecords = new Func<List<string>>(() => GetMxRecord(record));
                    OutputList.Add(record, RunTaskWithTimeout(getMxRecords, 10) ?? new List<string>() { "No records" });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            return OutputList;
        }

        /// <summary>
        /// Get a single MX record
        /// </summary>
        /// <param name="domain">Domain name</param>
        /// <returns>List of MX records for domain</returns>
        private static List<string> GetMxRecord(string domain)
        {
            var lookup = new LookupClient();
            try
            {
                Console.WriteLine($"\n-------------------------\nMX Lookup of: {domain}");
                var result = lookup.QueryAsync(domain, QueryType.ANY).Result;
                List<string> mxRecords = new List<string>();
                foreach (var record in result.AllRecords)
                    if (record.ToString().Contains(" MX "))
                    {
                        mxRecords.Add(record.ToString());
                        Console.WriteLine($"{record.ToString()}");
                    }
                    else
                        Console.WriteLine($"{record} {{no MX record}}");
                return mxRecords;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Return Bytes from a string
        /// </summary>
        /// <param name="str">String to return bytes</param>
        /// <returns>Bytes</returns>
        private static byte[] BytesFromString(string str) => Encoding.ASCII.GetBytes(str);


        /// <summary>
        /// Parse response code from telnet commands
        /// </summary>
        /// <param name="ResponseString">Response code</param>
        /// <returns>Output of response code</returns>
        private static int GetResponseCode(string ResponseString)
        {
            if (ResponseString != null)
                return int.Parse(ResponseString.Substring(0, 3));
            else
                return 550;
        }

        /// <summary>
        /// Run any method with a time out
        /// </summary>
        /// <typeparam name="T">Data type to return</typeparam>
        /// <param name="TaskAction">Delegate of function to return</param>
        /// <param name="TimeoutSeconds">Number of seconds to wait before timeout</param>
        /// <returns></returns>
        static private T RunTaskWithTimeout<T>(Func<T> TaskAction, int TimeoutSeconds)
        {
            Task<T> backgroundTask;

            try
            {
                backgroundTask = Task.Factory.StartNew(TaskAction);
                backgroundTask.Wait(new TimeSpan(0, 0, TimeoutSeconds));
            }
            catch (AggregateException ex)
            {
                // task failed
                var failMessage = ex.Flatten().InnerException.Message;
                return default(T);
            }
            catch (Exception ex)
            {
                // task failed
                var failMessage = ex.Message;
                return default(T);
            }

            if (!backgroundTask.IsCompleted)
            {
                // task timed out
                return default(T);
            }

            // task succeeded
            return backgroundTask.Result;
        }

        /// <summary>
        /// Clean up record to get a clean MX record to parse with TCP client
        /// </summary>
        /// <param name="record">MX Record</param>
        /// <returns>MX record address</returns>
        private static string CleanRecord(string record)
        {
            var charArr = record.ToCharArray();
            var secondToLastChar = charArr[charArr.Length - 2].ToString();

            int indexOfDomain;
            if (!int.TryParse(secondToLastChar, out int _))
                indexOfDomain = record.LastIndexOf(' ');
            else
                indexOfDomain = record.IndexOf(' ');

            record = record.Substring(indexOfDomain, record.Length - indexOfDomain);
            if (record.ToCharArray()[0].Equals(' '))
                record = record.Substring(1);

            return record;
        }

        /// <summary>
        /// Telnet to verify if client exists
        /// </summary>
        /// <param name="tcpClient">TCP Client</param>
        /// <returns>If account is alive</returns>
        private static bool IsEmailAccountValid(TCPClient tcpClient)
        {
            try
            {
                tcpClient.TcpInfo = CleanRecord(tcpClient.TcpInfo);
                TcpClient tClient = new TcpClient(tcpClient.TcpInfo, 25);
                string CRLF = "\r\n";
                byte[] dataBuffer;
                string ResponseString;
                NetworkStream netStream = tClient.GetStream();
                StreamReader reader = new StreamReader(netStream);
                ResponseString = reader.ReadLine();
                Console.WriteLine(ResponseString);

                Console.WriteLine($"\nEmail account: {tcpClient.EmailAddress}");

                /* Perform HELO to SMTP Server and get Response */
                dataBuffer = BytesFromString("HELO" + CRLF);
                netStream.Write(dataBuffer, 0, dataBuffer.Length);
                ResponseString = reader.ReadLine();
                Random rnd = new Random();
                string randomEmail = GetRandomEmail(rnd.Next(16));
                dataBuffer = BytesFromString($"MAIL FROM:<{randomEmail}>" + CRLF);
                netStream.Write(dataBuffer, 0, dataBuffer.Length);
                ResponseString = reader.ReadLine();
                Console.WriteLine(ResponseString);

                /* Read Response of the RCPT TO Message to know from google if it exist or not */
                dataBuffer = BytesFromString($"RCPT TO:<{tcpClient.EmailAddress}>" + CRLF);
                netStream.Write(dataBuffer, 0, dataBuffer.Length);
                ResponseString = reader.ReadLine();
                Console.WriteLine(ResponseString);

                var responseCode = GetResponseCode(ResponseString);
                Console.WriteLine(responseCode);

                if (responseCode > 420)
                    return false;

                /* QUIT CONNECTION */
                dataBuffer = BytesFromString("QUIT" + CRLF);
                netStream.Write(dataBuffer, 0, dataBuffer.Length);
                tClient.Close();
                return true;
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false; 
            }
        }

        private static string GetRandomEmail(int length)
        {
            Random rnd = new Random();
            var byteList = new byte[3];
            rnd.NextBytes(byteList);
            string numbers = "";
            foreach (var byteVal in byteList)
                numbers += byteVal;

            //List of alphabet to select random char
            char[] letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();

            // Make a word.
            string word = "";
            for (int j = 1; j <= length; j++)
            {
                //Pick a random number to select from char array then append to the word
                int letter_num = rnd.Next(0, letters.Length - 1);
                word += letters[letter_num];
            }

            return word + numbers + "@gmail.com";
        }
    }
}
