using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using LeiKaiFeng.X509Certificates;
using Microsoft.Win32;

namespace InstallHelp
{
    class Program
    {

        static T GetFromConsole<T, TE>(string message, Func<string, T> func)where TE: Exception
        {
            while (true)
            {
                Console.WriteLine(message);
                try
                {
                    return func(Console.ReadLine());
                }
                catch (TE)
                {
                    Console.WriteLine("输入错误请重新输入");
                }
            }
        }

        

        const string CA_SAVE_FOLDER_NAME = "cauninstallfolder";

        static void InstallCert()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;

            X509Certificate2 MovePrivateKey(X509Certificate2 cert)
            {
                var raw = cert.Export(X509ContentType.Cert);

                return new X509Certificate2(raw);
            }

            void InstallAndSaveCa(X509Certificate2 cert)
            {
                var folder_path = Path.Combine(basePath, CA_SAVE_FOLDER_NAME);


                Directory.CreateDirectory(folder_path);


                var file_name = Path.GetRandomFileName();

                var save_path = Path.Combine(folder_path, file_name);


                File.WriteAllBytes(save_path, cert.Export(X509ContentType.Cert));

                using (X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(cert); 
                }
            }

            void InstallTls(byte[] pem, byte[] key)
            {
                var folder_path = Path.Combine(basePath, "nginx", "conf");

                Directory.CreateDirectory(folder_path);

                var pem_path = Path.Combine(folder_path, "cert.pem");

                var key_path = Path.Combine(folder_path, "cert.key");


                File.WriteAllBytes(pem_path, pem);

                File.WriteAllBytes(key_path, key);        
            }


            var caCert = TLSBouncyCastleHelper.GenerateCA(
               "LEIKAIFENG CA ROOT",
               2048,
               30000);
           
           
            var tlsCert = TLSBouncyCastleHelper.GenerateTls(
                CaPack.Create(caCert),
                "NGINX HTTPS CERT",
                2048,
                30000,
                new string[] { "*.iwara.tv" });

            var tls_pem = TLSBouncyCastleHelper.CreatePem.AsPem(tlsCert);

            var tls_key = TLSBouncyCastleHelper.CreatePem.AsKey(tlsCert);

            InstallAndSaveCa(MovePrivateKey(caCert));

            InstallTls(tls_pem, tls_key);
        }

        static void UnInstallCert()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;

            var folder_path = Path.Combine(basePath, CA_SAVE_FOLDER_NAME);


            foreach (var item in Directory.GetFiles(folder_path))
            {
                using (X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadWrite);
                    store.Remove(new X509Certificate2(item)); 
                }
            }
        }

        const string SERIRCE_NAME = "A_NGINX_e85312f8-002c-43e8-ae90-a4254bffae04";
        
        static string GetNssmPath()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;

            string win;

            if (Environment.Is64BitOperatingSystem)
            {
                win = "win64";
            }
            else
            {
                win = "win32";
            }

            var appPath = Path.Combine(basePath, "nssm", win, "nssm.exe");

            return appPath;
        }

        static void InstallAndStartNginx(string targetPath)
        {
            UnInstallNginx();

            var appPath = GetNssmPath();

            //install <servicename> <program>
            using (var p = Process.Start(appPath, $"install {SERIRCE_NAME} \"{targetPath}\""))
            {
                p.WaitForExit();
            }


            using (var p = Process.Start(appPath, $"start {SERIRCE_NAME}"))
            {
                p.WaitForExit();
            }
        }


        static void UnInstallNginx()
        {
            using (var p = Process.Start(GetNssmPath(), $"stop {SERIRCE_NAME}"))
            {
                p.WaitForExit();
            }


            using (var p = Process.Start(GetNssmPath(), $"remove {SERIRCE_NAME} confirm"))
            {
                p.WaitForExit();
            }

        }

        

        static string GetNginxPath()
        {
            var basePaath = AppDomain.CurrentDomain.BaseDirectory;

            return Path.Combine(basePaath, "nginx", "nginx.exe");
        }

       

        static void Run(string appPath, string arguments, string workingDirectory)
        {


            using (var p = Process.Start(new ProcessStartInfo { FileName = appPath, Arguments = arguments, UseShellExecute = false, WorkingDirectory = workingDirectory }))
            {
                p.WaitForExit();
            }
        }


        static string CreateRegistryPath(bool isIPv4, string id)
        {
            string ip = isIPv4 ? "Tcpip" : "Tcpip6";

            var vs = new string[] { "SYSTEM", "ControlSet001", "Services", ip, "Parameters", "Interfaces", id };


            return string.Join(@"\", vs);

        }

        static void SetDnsServerAddress(bool isIPv4, string ip)
        {



            var vs = NetworkInterface.GetAllNetworkInterfaces()
                .Where(p => p.NetworkInterfaceType == NetworkInterfaceType.Ethernet || p.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .Select(p => p.Id)
                .Select(p => CreateRegistryPath(isIPv4, p))
                .Select(p => Registry.LocalMachine.OpenSubKey(p, true))
                .ToArray();

            Array.ForEach(vs, e => e.SetValue("NameServer", ip));
        }



        static void SetDnsServerAddress()
        {
            SetDnsServerAddress(true, "127.0.0.1");
            SetDnsServerAddress(false, "::1");
        }

        static void ResetDnsServerAddress()
        {
            SetDnsServerAddress(true, "");
            SetDnsServerAddress(false, "");
        }

        static void FlashDnsCache()
        {
            var path = @"C:\Windows\System32\ipconfig.exe";


            Run(path, "/flushdns", string.Empty);
        }


        static void GetDNSPath(out string workerPath, out string appPath)
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;

            workerPath = Path.Combine(basePath, "Acrylic");

            appPath = Path.Combine(workerPath, "AcrylicUI.exe");
        }


        static void InstallDNSServer()
        {

            GetDNSPath(out var basePath, out var appPath);


            Run(appPath, "UninstallAcrylicService", basePath);
           
            Run(appPath, "InstallAcrylicService", basePath);
            
            Run(appPath, "StartAcrylicService", basePath);

            SetDnsServerAddress();

            FlashDnsCache();


        }


        static void UnInstallDNSServer()
        {
            GetDNSPath(out var basePath, out var appPath);

            ResetDnsServerAddress();

            FlashDnsCache();
         

            Run(appPath, "UninstallAcrylicService", basePath);
        }

        static void Main(string[] args)
        {
            //Environment.Is64BitOperatingSystem

            var newline = Environment.NewLine;
            int flag = GetFromConsole<int, FormatException>($"输入数字:{newline}0 安装{newline}1 卸载{newline}请输入:",
                (s) =>
                {
                    int n = int.Parse(s);
                    if (((uint)n) > 1)
                    {
                        throw new FormatException();
                    }
                    else
                    {
                        return n;
                    }
                });



            try
            {
                if (flag == 0)
                {
                    Console.WriteLine("正在安装");

                    InstallCert();

                    InstallAndStartNginx(GetNginxPath());

                    InstallDNSServer();

                    Console.WriteLine("安装完成可以关闭窗口");
                }
                else
                {
                    Console.WriteLine("正在卸载");

                    UnInstallDNSServer();

                    UnInstallNginx();

                    UnInstallCert();

                    Console.WriteLine("卸载完成可以关闭窗口");
                }


            }
            catch (Exception e)
            {
                Console.WriteLine("出现了未知错误");
                Console.WriteLine(e);
            }

            Console.ReadLine();


        }
    }
}
