using DnsClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Email_Lookup_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static List<Email> EmailList;
        public static List<Email> EmailBlackListDomain;
        public static List<Email> EmailBlackListPrefix;
        public static List<EmailOutput> EmailOutputList;
        public MainWindow()
        {
            InitializeComponent();
            EmailList = new List<Email>();
            EmailBlackListDomain = new List<Email>();
            EmailBlackListPrefix = new List<Email>();
            EmailOutputList = new List<EmailOutput>();
        }

        private void EmailsCheckButton_Click(object sender, RoutedEventArgs e)
        {
            var data = new List<Email>();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
                data = CsvParser.LoadEmail(openFileDialog.FileName);
            if (data != null)
                EmailList.AddRange(data);
            EmailList.Distinct().ToList();
            if (EmailsCheckLabel.Content.ToString().EndsWith(".csv"))
                EmailsCheckLabel.Content += ",";
            EmailsCheckLabel.Content += " " + openFileDialog.FileName.Substring(openFileDialog.FileName.LastIndexOf('\\') + 1);
        }

        private void EmailsBlacklistDomainButton_Click(object sender, RoutedEventArgs e)
        {
            var data = new List<Email>();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
                data = CsvParser.LoadEmail(openFileDialog.FileName);
            if (data != null)
                EmailBlackListDomain.AddRange(data);
            EmailBlackListDomain.Distinct().ToList();
            if (EmailsBlacklistLabel.Content.ToString().EndsWith(".csv"))
                EmailsBlacklistLabel.Content += ",";
            EmailsBlacklistLabel.Content += " " + openFileDialog.FileName.Substring(openFileDialog.FileName.LastIndexOf('\\') + 1);
        }

        private void EmailsBlacklistPrefixButton_Click(object sender, RoutedEventArgs e)
        {
            var data = new List<Email>();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
                data = CsvParser.LoadEmail(openFileDialog.FileName);
            if (data != null)
                EmailBlackListPrefix.AddRange(data);
            EmailBlackListPrefix.Distinct().ToList();
            if (EmailsBlacklistLabel.Content.ToString().EndsWith(".csv"))
                EmailsBlacklistLabel.Content += ",";
            EmailsBlacklistLabel.Content += " " + openFileDialog.FileName.Substring(openFileDialog.FileName.LastIndexOf('\\') + 1);
        }

        private void CheckEmails_Click(object sender, RoutedEventArgs e)
        {
            var mxRecords = GetMxRecords();
            foreach (var record in mxRecords)
                foreach (var mxRecord in record.Value)
                    if (mxRecord.Contains("no record"))
                        mxRecords[record.Key].Remove(mxRecord);

            if(EmailBlackListDomain.Count > 1)
            {
                var tempList = EmailList;
                foreach (var emailCheck in EmailList)
                    foreach (var emailBlock in EmailBlackListDomain)
                        if (emailCheck.EmailAddress == emailBlock.EmailAddress)
                            tempList.Remove(emailCheck);
                EmailList = tempList;
            }

            if (EmailBlackListPrefix.Count > 1)
            {
                var tempList = EmailList;
                foreach (var email in EmailList)
                    foreach (var prefixEmail in EmailBlackListPrefix)
                        if (email.EmailAddress.ReturnPrefix() == prefixEmail.EmailAddress)
                            tempList.Remove(email);

                EmailList = tempList;
            }

            mxRecords = getTcpInfo(mxRecords);
            
            foreach (var email in EmailList)
            {
                try
                {
                    foreach (var tcpRecord in mxRecords[email.EmailAddress.ReturnDomain()])
                    {
                        Func<bool> telnetCheck = new Func<bool>(() => IsEmailAccountValid(new TCPClient(tcpRecord, email.EmailAddress)));
                        if (RunTaskWithTimeout(telnetCheck, 10))
                        {
                            EmailOutputList.Add(new EmailOutput(email));
                            EmailListBox.Items.Add(email.EmailAddress);
                            Thread.Sleep(20);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }

            if (EmailOutputList.Count > 0)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "CSV file (*.csv)|*.csv| All Files (*.*)|*.*";
                sfd.Title = "Save the output";
                sfd.ShowDialog();

                CsvParser.SaveToCsv(EmailOutputList, sfd.FileName);
                MessageBox.Show($"{EmailOutputList.Count}/{EmailList.Count} Emails are alive");
            }
            else
                MessageBox.Show("No emails are alive");
        }

        /// <summary>
        /// Return a list of MX records
        /// </summary>
        /// <param name="emailList">List of eamils to check MX records from</param>
        /// <returns></returns>
        static public Dictionary<string, List<string>> GetMxRecords()
        {
            var OutputList = new Dictionary<string, List<string>>();
            var ExcludedDomainsList = new List<IgnoredEmails>();

            var domainList = new List<string>();
            
            foreach(var email in EmailList)
            {
                string domainName = email.EmailAddress.ReturnDomain();
                if (!domainList.Contains(domainName))
                    domainList.Add(domainName);
            }

            foreach (var record in domainList)
            {
                try
                {
                    Func<List<string>> getMxRecords = new Func<List<string>>(() => GetMxRecord(record));
                    OutputList.Add(record, RunTaskWithTimeout(getMxRecords, 10) ?? new List<string>() { "No records" });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            return OutputList;
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
                        MessageBox.Show(ex.Message);
                    }
                }

                if (recordList.Count > 0)
                    result.Add(mxList.Key, recordList);
            }

            return result;
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
                MessageBox.Show(ex.Message);
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
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
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
