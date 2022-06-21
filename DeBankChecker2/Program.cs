using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Nethereum.Web3;
using System.Numerics;
using Leaf.xNet;
using Nethereum.HdWallet;

namespace DeBankChecker
{
    public class Program
    {
        public static object locker = new object();
        public static bool jobDone = false;

        public static int network = 0;
        public static int addr_count = 0;
        public static int threads = 0;
        public static List<string> seedList = new List<string>();

        public static Dictionary<int, string> networks = new Dictionary<int, string>()
        {
             { 1, "eth" },
             { 2, "bsc" },
             { 3, "avax" },
             { 4, "matic" },
             { 5, "arb" },
             { 6, "ftm" },
             { 7, "op" },
             { 8, "cro" }

        };
       
        static void Main(string[] args)
        {

            Console.Title = "DeBank balance checker";
            Console.WriteLine(@"   ___      ___            __   _______           __          ");
            Console.WriteLine(@"  / _ \___ / _ )___ ____  / /__/ ___/ /  ___ ____/ /_____ ____");
            Console.WriteLine(@" / // / -_) _  / _ `/ _ \/  '_/ /__/ _ \/ -_) __/  '_/ -_) __/");
            Console.WriteLine(@"/____/\__/____/\_,_/_//_/_/\_\\___/_//_/\__/\__/_/\_\\__/_/   ");
            Console.WriteLine(@"                                                              ");
          


            do
            {
                try
                {
                    Console.WriteLine();
                    Console.WriteLine("[*] Select network for check:");
                    Console.WriteLine("[1] ETHEREUM");
                    Console.WriteLine("[2] BSC");
                    Console.WriteLine("[3] AVAX");
                    Console.WriteLine("[4] MATIC");
                    Console.WriteLine("[5] ARBITRUM");
                    Console.WriteLine("[6] FTM");
                    Console.WriteLine("[7] OPTIMISM");
                    Console.WriteLine("[8] CRONOS");
                    Console.WriteLine("[9] All networks");

                    Console.Write(Environment.NewLine + ">");
                    network = int.Parse(Console.ReadLine());

                }
                catch
                { }
            }
            while (network > 9 || network < 1);

            do
            {
                try
                {
                    Console.Write("[*] Addresses count for each seed (1-10): ");
                    //Console.Write(Environment.NewLine + ">");
                    addr_count = int.Parse(Console.ReadLine());
                }
                catch
                { }
            }
            while (addr_count > 10 || addr_count < 1);

            do
            {
                try
                {
                    Console.Write("[*] Number of threads (1-10): ");
                    //Console.Write(Environment.NewLine + ">");
                    threads = int.Parse(Console.ReadLine());
                }
                catch
                { }
            }
            while (threads > 10 || addr_count < 1);


            do
            {
                try
                {
                    Console.Write("[*] List of seeds: ");
                    //Console.Write(Environment.NewLine + ">");
                    string seedlist = Console.ReadLine();
                    List<string> s = new List<string>();
                    if (File.Exists(seedlist))
                    {
                        foreach (var item in File.ReadAllLines(seedlist))
                        {
                            if (item.Trim().Split(' ').Length == 12 || item.Trim().Split(' ').Length == 24)
                                s.Add(item);
                        }
                    }

                    seedList.AddRange(s.Distinct());
                }
                catch
                {
                    Console.WriteLine("[-] Empty file or seeds not valid!");
                }
            }
            while (seedList.Count == 0);

            Console.Clear();
            Console.WriteLine(" Network:\t\t" + (network == 9 ? "all" : networks[network]));
            Console.WriteLine(" Address per seed:\t" + addr_count);
            Console.WriteLine(" Seeds count:\t\t" + seedList.Count);
            Console.WriteLine(" Threads count:\t\t" + threads);
            Console.WriteLine(" Addresses total:\t" + seedList.Count*addr_count);
            Console.WriteLine(" All results will be added to output.txt!");

            for (int i = 0; i < threads; i++)
            {
                Console.WriteLine( " Thread " + i + " started...");
                new System.Threading.Thread(thread).Start();
            }
        }

        static void thread()
        {
            string item = "";
            string cntn = "";
            while (true)
            {
                if (!jobDone && seedList.Count > 0)
                {
                    lock (locker)
                    {
                        item = seedList[0];
                        seedList.RemoveAt(0);

                    }

                    string[] result = Checker.getAddresses(item.Trim());

                    if (result.Length == 0)
                        continue;

                    cntn += '\n';
                    cntn += item + '\n';

                    foreach (var item2 in result)
                        cntn += item2 + '\n';

                    Console.WriteLine(item);

                    lock (locker)
                        File.AppendAllText("output.txt", cntn + '\n');

                    cntn = "";
                }
                else
                    return;

            }

        }


    }

    class Checker
    {
       
        // generate addresses from seed
        public static string[] getAddresses(string phrase)
        {

            List<string> data = new List<string>();
            try
            {
                string[] addresses = new Wallet(phrase, "").GetAddresses(Program.addr_count);

                foreach (var addr in addresses)
                {
                    string val = addr;

                    val += "\n" + deBank(addr) + "\n";
                    data.Add(val);
                }

            }
            catch
            { }

            return data.ToArray();
        }


        // parse json response
        static string parseData(Newtonsoft.Json.Linq.JObject obj, string blockName)
        {
            string info = null;

            if (obj["data"].HasValues)
            {
                foreach (var item in obj["data"])
                {
                    info += item["symbol"] + "\t\t" + item["chain"] + "\t\t";

                    double balance = getBalanceValue(item["balance"].ToString(), item["decimals"].ToString());


                    double usdvalue = getUSDTValue(balance, item["price"].ToString());

                    info += usdvalue.ToString().Replace(",", ".") + " USD\n";
                }
            }
            else
                info += blockName + " Zero balance\n";

            return info;
        }


        static string deBank(string address)
        {
            string info = "-- Classic networks --\n";
            HttpRequest http = new HttpRequest();

            try
            {
                http.ConnectTimeout = 8000;
                string resp = null;
                Newtonsoft.Json.Linq.JObject _jresponse = null;



                if (Program.network == 9)
                {
                    // checl all networks
                    foreach (var item in Program.networks)
                    {
                        // request until we get response
                        while (resp == null)
                        {
                            try
                            {
                                resp = http.Get("https://api.debank.com/token/balance_list?is_all=false&user_addr=" + address + "&chain=" + item.Value).ToString();

                            }
                            catch
                            { }
                        }
                        // parse balance
                        _jresponse = Newtonsoft.Json.Linq.JObject.Parse(resp);

                        // add info to networks
                        info += parseData(_jresponse, item.Value.ToUpper());
                        resp = null;
                        _jresponse = null;
                    }
                }
                else
                {
                    // one network selected
                    while (resp == null)
                    {
                        try
                        {
                            resp = http.Get("https://api.debank.com/token/balance_list?is_all=false&user_addr=" + address + "&chain=" + Program.networks[Program.network]).ToString();

                        }
                        catch
                        { }
                    }
                    _jresponse = Newtonsoft.Json.Linq.JObject.Parse(resp);

                    info += parseData(_jresponse, Program.networks[Program.network].ToUpper());
                }

            }
            catch
            {
                info += "PARSE ERROR";
            }


            http.Dispose();

            return info;
        }

        // help methods to parse data
        static double getBalanceValue(string balance, string decmals)
        {

            try
            {
                double x = (double)Web3.Convert.FromWei(BigInteger.Parse(balance), int.Parse(decmals));

                return Math.Round(x, 5);

            }
            catch
            { }

            return -1;
        }
        static double getUSDTValue(double balance, string price)
        {

            return Math.Round(balance * double.Parse(price), 4);

        }
        static string getDateValue(string unixTimeStamp)
        {
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(double.Parse(unixTimeStamp)).ToLocalTime();
            return dateTime.GetDateTimeFormats()[2];
        }
    }

}
