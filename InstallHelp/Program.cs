using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using Microsoft.Win32;
using System.Collections.Generic;


namespace InstallHelp
{
    internal class Program
	{
		static T GetFromConsole<T, TE>(string message, Func<string, T> func) where TE : Exception
		{
			T result;
			for (; ; )
			{
				Console.WriteLine(message);
				try
				{
					result = func(Console.ReadLine());
				}
				catch (TE)
				{
					Console.WriteLine("输入错误请重新输入");
					continue;
				}
				break;
			}
			return result;
		}

        static void Run(string appPath, string arguments, string workingDirectory)
		{
			using (Process p = Process.Start(new ProcessStartInfo
			{
				FileName = appPath,
				Arguments = arguments,
				UseShellExecute = false,
				WorkingDirectory = workingDirectory
			}))
			{
				p.WaitForExit();
			}
		}

		static void GetDNSPath(out string workerPath, out string appPath)
		{
			string basePath = AppDomain.CurrentDomain.BaseDirectory;
			workerPath = Path.Combine(basePath, "Acrylic");
			appPath = Path.Combine(workerPath, "AcrylicUI.exe");
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

		static void InstallDNSServer()
		{
            GetDNSPath(out string basePath, out string appPath);
            Run(appPath, "UninstallAcrylicService", basePath);
            Run(appPath, "InstallAcrylicService", basePath);
            Run(appPath, "StartAcrylicService", basePath);
            SetDnsServerAddress();
            FlashDnsCache();
		}

		static void ReStartDNSServer()
		{
            GetDNSPath(out string basePath, out string appPath);
            Run(appPath, "PurgeAcrylicCacheData", basePath);
            Run(appPath, "RestartAcrylicService", basePath);
            FlashDnsCache();
		}

		static void UnInstallDNSServer()
		{
            GetDNSPath(out string basePath, out string appPath);
            ResetDnsServerAddress();
            FlashDnsCache();
            Run(appPath, "UninstallAcrylicService", basePath);
		}

		static void GetTestIPAPPPath(out string dirPath, out string appPath, out string csvPath)
		{
			string basePath = AppDomain.CurrentDomain.BaseDirectory;
			dirPath = Path.Combine(basePath, "CloudflareST_windows_386");
			appPath = Path.Combine(dirPath, "CloudflareST.exe");
			csvPath = Path.Combine(dirPath, "result.csv");
		}

		static IPAddress GetCSV(string path)
		{
			return IPAddress.Parse(File.ReadAllLines(path, Encoding.UTF8).Skip(1).First<string>().Split(new char[]
			{
				','
			}, StringSplitOptions.RemoveEmptyEntries).First<string>());
		}

		static IPAddress TextFastIP()
		{
            GetTestIPAPPPath(out string dirPath, out string appPath, out string csvPath);
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                WorkingDirectory = dirPath,
                FileName = appPath,
                UseShellExecute = false,
                Arguments = "-dd"
            };
            Console.WriteLine("正在测速IP请耐心等待不要关闭窗口,保持网络链接正常");
			using (Process p = Process.Start(processStartInfo))
			{
				p.WaitForExit();
			}
			return GetCSV(csvPath);
		}

		static void SetFastIP(IPAddress ip)
		{
            GetDNSPath(out string dirPath, out _);
            string path = Path.Combine(dirPath, "hosts.txt");
			string s = "127.0.0.5 ajax.googleapis.com" + Environment.NewLine + string.Format("{0} *.iwara.tv", ip);
			File.WriteAllText(path, s, Encoding.UTF8);
		}

		static void InstallMain()
        {
			string newline = Environment.NewLine;
			int flag = GetFromConsole<int, FormatException>(string.Concat(new string[]
			{
				"输入数字:",
				newline,
				"0 安装",
				newline,
				"1 重新测速IP",
				newline,
				"2 卸载",
				newline,
				"请输入:"
			}), delegate (string s)
			{
				int i = int.Parse(s);
				if (i > 2)
				{
					throw new FormatException();
				}
				return i;
			});
			try
			{
				if (flag == 0)
				{
					Console.WriteLine("正在安装");
					SetFastIP(TextFastIP());
					InstallDNSServer();
					Console.WriteLine("安装完成可以关闭窗口");
				}
				else if (flag == 1)
				{
					Console.WriteLine("正在测速");
					SetFastIP(TextFastIP());
					ReStartDNSServer();
					Console.WriteLine("测速完成可以关闭窗口");
				}
				else
				{
					Console.WriteLine("正在卸载");
					UnInstallDNSServer();
					Console.WriteLine("卸载完成可以关闭窗口");
				}
			}
			catch (Exception value)
			{
				Console.WriteLine("出现了未知错误");
				Console.WriteLine(value);
			}
			Console.ReadLine();
		}


		

		static void Main(string[] args)
		{

			InstallMain();

		}

	}
}