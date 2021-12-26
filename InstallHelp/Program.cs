using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Management;

namespace InstallHelp
{

	public static class SetDnsServerAddress
	{

		static void SetIPv6(string guidID, IPAddress[] dnsIPs)
		{

			var vs = new string[] { "SYSTEM", "ControlSet001", "Services", "Tcpip6", "Parameters", "Interfaces", guidID };
			var path = string.Join(@"\", vs);

			var reg = Registry.LocalMachine.OpenSubKey(path, true);

			reg.SetValue("NameServer", string.Join<IPAddress>(",", dnsIPs));
		}


		static void SetIPv4(string guidID, IPAddress[] dnsIPs)
		{
			ManagementObject[] GetInstances()
			{
				return (new ManagementClass("Win32_NetworkAdapterConfiguration"))
					.GetInstances()
					.Cast<ManagementObject>()
					.Where(mo => mo["SettingID"].ToString().Equals(guidID))
					.ToArray();
			}

			foreach (ManagementObject objMO in GetInstances())
			{
				ManagementBaseObject objdns = objMO.GetMethodParameters("SetDNSServerSearchOrder");

				var dnsvs = dnsIPs.Select(p => p.ToString()).ToArray();

				objdns["DNSServerSearchOrder"] = dnsvs;

				objMO.InvokeMethod("SetDNSServerSearchOrder", objdns, null);
			}
		}




		public static void Set(string[] guidIDs, IPAddress[] dnsIPs)
		{
			var ipv4 = dnsIPs.Where(p => p.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToArray();

			var ipv6 = dnsIPs.Where(p => p.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6).ToArray();



			foreach (var guid in guidIDs)
			{
				SetIPv6(guid, ipv6);

				SetIPv4(guid, ipv4);
			}
		}
	}


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


		static void FlashDnsCache()
		{
			var path = @"C:\Windows\System32\ipconfig.exe";


			Run(path, "/flushdns", string.Empty);
		}
		public static string[] GetActiveEthernetOrWifiNetworkInterface()
		{
			return NetworkInterface.GetAllNetworkInterfaces()
				.Where(a => a.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || a.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
				.Select(p => p.Id)
				.ToArray();
		}

		static void InstallDNSServer()
		{
            GetDNSPath(out string basePath, out string appPath);
            Run(appPath, "UninstallAcrylicService", basePath);
            Run(appPath, "InstallAcrylicService", basePath);
            Run(appPath, "StartAcrylicService", basePath);

			SetDnsServerAddress.Set(GetActiveEthernetOrWifiNetworkInterface(), new IPAddress[] { IPAddress.Loopback, IPAddress.IPv6Loopback });

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


			SetDnsServerAddress.Set(GetActiveEthernetOrWifiNetworkInterface(), new IPAddress[] { });

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

		static IPAddress[] TextFastIP()
		{
            GetTestIPAPPPath(out string dirPath, out string appPath, out string csvPath);

			Console.WriteLine("正在测速IP请耐心等待不要关闭窗口,保持网络链接正常,测速完毕一定要按回车,千万不要点击右上角的X");

			Run(appPath, "-dd -tll 100", dirPath);

			return new IPAddress[] { GetCSV(csvPath) };
		}

		static void SetFastIP(IPAddress[] ip)
        {
			var vs = ip.Select(p => new KeyValuePair<string, string>(p.ToString(), "*.iwara.tv"))
			.Append(new KeyValuePair<string, string>("127.0.0.5", "ajax.googleapis.com"))
			.ToArray();

			SetFastIP(vs);
        }

		static void SetFastIP(KeyValuePair<string, string>[] vs)
		{
            GetDNSPath(out string dirPath, out _);
            string path = Path.Combine(dirPath, "hosts.txt");

			var seq = vs.Select(p => string.Join(" ", new string[] { p.Key, p.Value }));

			string s = string.Join(Environment.NewLine, seq);

			File.WriteAllText(path, s, new UTF8Encoding(false));
		}

		static IPAddress[] GetCloudFlareDns()
        {
			return Dns.GetHostAddresses("workers.cloudflare.com");
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
				"2 假如安装了也重测了都没有效果可以选这个试试",
				newline,
				"3 卸载",
				newline,
				"请输入:"
			}), delegate (string s)
			{
				int i = int.Parse(s);
				if (i > 3)
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
				else if (flag == 2)
                {
					Console.WriteLine("正在获取极大可能有效但是速度可能不快的IP");
					SetFastIP(GetCloudFlareDns());
					ReStartDNSServer();
					Console.WriteLine("IP替换完成可以关闭窗口");
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