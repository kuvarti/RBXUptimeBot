#pragma warning disable CS4014

using System.Drawing;
//using BrightIdeasSoftware;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RBXUptimeBot.Classes;
//using RBX_Alt_Manager.Properties;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection.Metadata;

namespace RBXUptimeBot.Classes
{
	public class ServerList
	{
		public ServerList()
		{
			RobloxClient = new RestClient("https://roblox.com/");
			ThumbClient = new RestClient("https://thumbnails.roblox.com/");
			GamesClient = new RestClient("https://games.roblox.com/");
			DevelopClient = new RestClient("https://develop.roblox.com/");

			//if (!AccountManager.Watcher.Exists("VerifyDataModel")) AccountManager.Watcher.Set("VerifyDataModel", "true");
			//if (!AccountManager.Watcher.Exists("IgnoreExistingProcesses")) AccountManager.Watcher.Set("IgnoreExistingProcesses", "true");
			//if (!AccountManager.Watcher.Exists("ExpectedWindowTitle")) AccountManager.Watcher.Set("ExpectedWindowTitle", "Roblox");

			// form components
			// RobloxScannerCB.Checked = AccountManager.Watcher.Get<bool>("Enabled");
			// ExitIfBetaDetectedCB.Checked = AccountManager.Watcher.Get<bool>("ExitOnBeta");
			// ExitIfNoConnectionCB.Checked = AccountManager.Watcher.Get<bool>("ExitIfNoConnection");
			// SaveWindowPositionsCB.Checked = AccountManager.Watcher.Get<bool>("SaveWindowPositions");
			// VerifyDataModelCB.Checked = AccountManager.Watcher.Get<bool>("VerifyDataModel");
			// IgnoreExistingProcesses.Checked = AccountManager.Watcher.Get<bool>("IgnoreExistingProcesses");
			// CloseRbxWindowTitleCB.Checked = AccountManager.Watcher.Get<bool>("CloseRbxWindowTitle");
			// RbxMemoryCB.Checked = AccountManager.Watcher.Get<bool>("CloseRbxMemory");
			// RbxWindowNameTB.Text = AccountManager.Watcher.Get<string>("ExpectedWindowTitle");
			// RbxMemoryLTNum.Value = AccountManager.Watcher.Exists("MemoryLowValue") ? Utilities.Clamp(AccountManager.Watcher.Get<decimal>("MemoryLowValue"), RbxMemoryLTNum.Minimum, RbxMemoryLTNum.Maximum) : 200;
			// TimeoutNum.Value = AccountManager.Watcher.Exists("NoConnectionTimeout") ? Utilities.Clamp(AccountManager.Watcher.Get<decimal>("NoConnectionTimeout"), TimeoutNum.Minimum, TimeoutNum.Maximum) : 60;
			// ScanIntervalN.Value = AccountManager.Watcher.Exists("ScanInterval") ? AccountManager.Watcher.Get<int>("ScanInterval") : 6;
			// ReadIntervalN.Value = AccountManager.Watcher.Exists("ReadInterval") ? AccountManager.Watcher.Get<int>("ReadInterval") : 250;
		}
		public static RestClient RobloxClient;
		public static RestClient ThumbClient;
		public static RestClient DevelopClient;
		public static RestClient GamesClient;
	}
}
