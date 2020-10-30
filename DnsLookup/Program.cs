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

namespace DnsLookup
{
    class Program
    {
        static void Main(string[] args)
        {
            //CheckDns();

            Dictionary<string, List<string>> mxRecords = new Dictionary<string, List<string>>();

            //mxRecords.Add("cpship", GetMxRecord("cpship.co.uk"));
            //mxRecords.Add("oytsouth", GetMxRecord("oytsouth.org"));
            //mxRecords.Add("Smartcrosby", GetMxRecord("Smartcrosby.com"));

            mxRecords = GetMxRecords();

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

            mxRecords = getTcpInfo(mxRecords);

            //Add existing output to output list to save old records
            List<EmailCheck> emailOutput = new List<EmailCheck>();
            using (var reader = new StreamReader("active emails.csv"))
                using(var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Configuration.HasHeaderRecord = true;
                    csv.Configuration.MissingFieldFound = null;
                    csv.Configuration.RegisterClassMap<EmailCheckMap>();
                    emailOutput.AddRange(csv.GetRecords<EmailCheck>().ToList());
                }

            Console.WriteLine($"\nLoaded {emailOutput.Count} existing records");


            //Load all emails that need to be checked
            List<EmailRead> emailInput = new List<EmailRead>();
            using (var reader = new StreamReader("Emails To Check.csv"))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Configuration.HasHeaderRecord = true;
                    csv.Configuration.MissingFieldFound = null;
                    csv.Configuration.RegisterClassMap<EmailReadMap>();
                    emailInput.AddRange(csv.GetRecords<EmailRead>().ToList());
                }

            //Check emails against domain to see if they match, then run telnet
            Console.WriteLine($"\nChecking 0/{emailInput.Count}");
            foreach (var input in emailInput)
            {
                string email = input.EmailAddress;
                string domain = ReturnDomain(email);
                Console.WriteLine($"Checking {emailInput.IndexOf(input)}/{emailInput.Count}\nEmail: {email}");

                if (!mxRecords.ContainsKey(domain))
                {
                    var templist = GetMxRecord(domain);
                    if(templist.Count > 0)
                        mxRecords.Add(domain, templist);
                }

                if (mxRecords.ContainsKey(domain))
                    foreach (var mxRecord in mxRecords[domain])
                    {
                        Thread.Sleep(20);
                        if (IsEmailAccountValid(mxRecord, email))
                        {
                            emailOutput.Add(new EmailCheck(email, "Success"));
                            break;
                        }
                    }
            }

            Console.WriteLine($"\nDomains for emails found {emailOutput.Count}/{emailInput.Count}\nMissing: {emailInput.Count - emailOutput.Count}");


            Console.WriteLine($"\nSaving {emailOutput.Count} records");
            using (var writer = new StreamWriter("active emails.csv"))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(emailOutput);
            }

            Console.WriteLine("Program ended\nRead key");
            Console.ReadKey(true);
        }

        public static string ReturnDomain(string email)
        {
            if (email.Contains("@"))
            {
                int firstIndex = email.IndexOf("@") + 1;
                int lastIndex = email.Length;
                email = email.Substring(firstIndex, lastIndex - firstIndex);
            }
            return email;
        }

        public static string ReturnNxRecord(string nxRecord)
        {
            if(nxRecord.Contains(" "))
            {

            }


            return nxRecord;
        }

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

        static void CheckDns()
        {
            using (var reader = new StreamReader(@"C:\domains.csv"))
            {
                while (!reader.EndOfStream)
                {
                    var value = reader.ReadLine().Split(";");
                    var dnsVal = value[0];
                    string output = PowerShellCommand($"nslookup {dnsVal}");
                    output = output.Replace("Server:  UnKnown", "");
                    Console.WriteLine($"----------------\n{output}");
                    Thread.Sleep(100);
                }
            }
        }

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

        static public Dictionary<string, List<string>> GetMxRecords()
        {
            var OutputList = new Dictionary<string, List<string>>();
            var InputList = new List<string>();

            using (var reader = new StreamReader(@"C:\Users\sebastianb\OneDrive - Millennium Ltd\Documents\New Domains.csv"))
            {
                while (!reader.EndOfStream)
                {
                    var value = reader.ReadLine().Split(";");
                    InputList.Add(value[0]);
                }
            }

            if(InputList.Contains("Domain"))
                InputList.Remove("Domain");

            foreach (var record in InputList)
            {
                try
                {
                    string rec = ReturnDomain(record);
                    OutputList.Add(rec, GetMxRecord(rec));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            return OutputList;
        }

        private static List<string> GetMxRecord(string val)
        {
            var lookup = new LookupClient();
            try
            {
                Console.WriteLine($"NX Lookup of: {val}");
                var result = lookup.QueryAsync(val, QueryType.ANY).Result;
                List<string> mxRecords = new List<string>();
                foreach (var record in result.AllRecords)
                    if (record.ToString().Contains("MX"))
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
        private static byte[] BytesFromString(string str) => Encoding.ASCII.GetBytes(str);


        private static int GetResponseCode(string ResponseString)
        {
            if (ResponseString != null)
                return int.Parse(ResponseString.Substring(0, 3));
            else
                return 550;
        }

        private T RunTaskWithTimeout<T>(Func<T> TaskAction, int TimeoutSeconds)
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
                var failMessage = ex.Flatten().InnerException.Message);
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

        private static bool IsEmailAccountValid(string tcpClient, string emailAddress)
        {
            try
            {
                tcpClient = CleanRecord(tcpClient);
                TcpClient tClient = new TcpClient(tcpClient, 25);
                string CRLF = "\r\n";
                byte[] dataBuffer;
                string ResponseString;
                NetworkStream netStream = tClient.GetStream();
                StreamReader reader = new StreamReader(netStream);
                ResponseString = reader.ReadLine();
                Console.WriteLine(ResponseString);

                Console.WriteLine($"\nEmail account: {emailAddress}");

                /* Perform HELO to SMTP Server and get Response */
                dataBuffer = BytesFromString("HELO" + CRLF);
                netStream.Write(dataBuffer, 0, dataBuffer.Length);
                ResponseString = reader.ReadLine();
                dataBuffer = BytesFromString("MAIL FROM:<gary.12.x12@gmail.com>" + CRLF);
                netStream.Write(dataBuffer, 0, dataBuffer.Length);
                ResponseString = reader.ReadLine();
                Console.WriteLine(ResponseString);

                /* Read Response of the RCPT TO Message to know from google if it exist or not */
                dataBuffer = BytesFromString($"RCPT TO:<{emailAddress}>" + CRLF);
                netStream.Write(dataBuffer, 0, dataBuffer.Length);
                ResponseString = reader.ReadLine();
                Console.WriteLine(ResponseString);

                var responseCode = GetResponseCode(ResponseString);
                Console.WriteLine(responseCode);

                if (responseCode == 550)
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
    }
}
