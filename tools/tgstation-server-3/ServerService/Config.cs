﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TGServiceInterface;
using System.Threading;

namespace TGServerService
{
	partial class TGStationServer : ITGConfig
	{
		const string AdminRanksConfig = StaticConfigDir + "/admin_ranks.txt";
		const string AdminConfig = StaticConfigDir + "/admins.txt";
		const string NudgeConfig = StaticConfigDir + "/nudge_port.txt";


		object configLock = new object();

		public string AddEntry(TGStringConfig type, string entry)
		{
			var currentEntries = GetEntries(type, out string error);
			if (currentEntries == null)
				return error;

			if (currentEntries.Contains(entry))
				return null;

			lock (configLock) {
				try
				{
					using (var f = File.AppendText(StringConfigToPath(type)))
					{
						f.WriteLine(entry);
					}
					return null;
				}
				catch (Exception e)
				{
					return e.ToString();
				}
			}
		}

		string StringConfigToPath(TGStringConfig type)
		{
			var result = StaticConfigDir + "/";
			switch (type)
			{
				case TGStringConfig.Admin_NickNames:
					result += "Admin_NickNames";
					break;
				case TGStringConfig.Silicon_Laws:
					result += "Silicon_Laws";
					break;
				case TGStringConfig.SillyTips:
					result += "SillyTips";
					break;
				case TGStringConfig.Whitelist:
					result += "Whitelist";
					break;
			}
			return result + ".txt";
		}

		string WriteMins(IDictionary<string, string> current_mins)
		{ 
			string outText = "";
			foreach (var I in current_mins)
				outText += I.Key + " = " + I.Value + "\r\n";

			try
			{
				lock (configLock)
				{
					File.WriteAllText(AdminConfig, outText);
				}
				return null;
			}
			catch (Exception e)
			{
				return e.ToString();
			}
		}

		public string Addmin(string ckey, string rank)
		{
			var Aranks = AdminRanks(out string error);
			if (Aranks != null)
			{
				if (Aranks.Keys.Contains(rank))
				{
					var current_mins = Admins(out error);
					if (current_mins != null)
					{
						current_mins[ckey] = rank;
						return WriteMins(current_mins);
					}
					return error;
				}
				return "Rank " + rank + " does not exist";
			}
			return error;
		}

		public IDictionary<string, IList<TGPermissions>> AdminRanks(out string error)
		{

			List<string> fileLines;
			lock (configLock)
			{
				try
				{
					fileLines = new List<string>(File.ReadAllLines(AdminRanksConfig));
				}
				catch (Exception e)
				{
					error = e.ToString();
					return null;
				}
			}

			var result = new Dictionary<string, IList<TGPermissions>>();
			IList<TGPermissions> previousPermissions = new List<TGPermissions>();
			foreach (var L in fileLines)
			{
				if (L.Length > 0 && L[0] == '#')
					continue;

				var splits = L.Split('=');

				if (splits.Length < 2)  //???
					continue;

				var rank = splits[0].Trim();

				var asList = new List<string>(splits);
				asList.RemoveAt(0);

				var perms = ProcessPermissions(asList, previousPermissions);
				result.Add(rank, perms);
				previousPermissions = perms;
			}
			error = null;
			return result;
		}

		IList<TGPermissions> ProcessPermissions(IList<string> text, IList<TGPermissions> previousPermissions)
		{
			IList<TGPermissions> permissions = new List<TGPermissions>();
			foreach(var E in text)
			{
				var trimmed = E.Trim();
				bool removing;
				switch (trimmed[0])
				{
					case '-':
						removing = true;
						break;
					case '+':
					default:
						removing = false;
						break;
				}
				trimmed = trimmed.Substring(1).ToLower();

				if (trimmed.Length == 0)
					continue;

				var perms = StringToPermission(trimmed, previousPermissions);

				if(perms != null)
					foreach(var perm in perms)
					{
						if (removing)
							permissions.Remove(perm);
						else if (!permissions.Contains(perm))
							permissions.Add(perm);
					}

			}
			return permissions;
		}

		IList<TGPermissions> StringToPermission(string permstring, IList<TGPermissions> oldpermissions)
		{
			TGPermissions perm;
			switch (permstring)
			{
				case "@":
				case "prev":
					return oldpermissions;
				case "buildmode":
				case "build":
					perm = TGPermissions.BUILD;
					break;
				case "admin":
					perm = TGPermissions.ADMIN;
					break;
				case "ban":
					perm = TGPermissions.BAN;
					break;
				case "fun":
					perm = TGPermissions.FUN;
					break;
				case "server":
					perm = TGPermissions.SERVER;
					break;
				case "debug":
					perm = TGPermissions.DEBUG;
					break;
				case "permissions":
				case "rights":
					perm = TGPermissions.RIGHTS;
					break;
				case "possess":
					perm = TGPermissions.POSSESS;
					break;
				case "stealth":
					perm = TGPermissions.STEALTH;
					break;
				case "rejuv":
				case "rejuvinate":
					perm = TGPermissions.REJUV;
					break;
				case "varedit":
					perm = TGPermissions.VAREDIT;
					break;
				case "everything":
				case "host":
				case "all":
					return new List<TGPermissions> {
						TGPermissions.ADMIN,
						TGPermissions.SPAWN,
						TGPermissions.FUN,
						TGPermissions.BAN,
						TGPermissions.STEALTH,
						TGPermissions.POSSESS,
						TGPermissions.REJUV,
						TGPermissions.BUILD,
						TGPermissions.SERVER,
						TGPermissions.DEBUG,
						TGPermissions.VAREDIT,
						TGPermissions.RIGHTS,
						TGPermissions.SOUND,
					};
				case "sound":
				case "sounds":
					perm = TGPermissions.SOUND;
					break;
				case "spawn":
				case "create":
					perm = TGPermissions.SPAWN;
					break;
				default:
					return null;
			}
			return new List<TGPermissions> { perm };
		}

		public IDictionary<string, string> Admins(out string error)
		{

			List<string> fileLines;
			lock (configLock)
			{
				try
				{
					fileLines = new List<string>(File.ReadAllLines(AdminConfig));
				}
				catch (Exception e)
				{
					error = e.ToString();
					return null;
				}
			}

			var mins = new Dictionary<string, string>();
			foreach(var L in fileLines)
			{
				var trimmed = L.Trim();
				if (L.Length == 0 || L[0] == '#')
					continue;

				var splits = L.Split('=');

				if (splits.Length != 2)
					continue;

				mins.Add(splits[0].Trim(), splits[1].Trim());
			}
			error = null;
			return mins;
		}

		public string Deadmin(string admin)
		{
			var current_mins = Admins(out string error);
			if (current_mins != null)
			{
				if (current_mins.ContainsKey(admin))
				{
					current_mins.Remove(admin);
					return WriteMins(current_mins);
				}
				return null;
			}
			return error;
		}

		public IList<string> GetEntries(TGStringConfig type, out string error)
		{
			try
			{
				IList<string> result;
				lock (configLock)
				{
					result = new List<string>(File.ReadAllLines(StringConfigToPath(type)));
				}
				result = result.Select(x => x.Trim()).ToList();
				result.Remove(result.Single(x => x.Length == 0 || x[0] == '#'));
				error = null;
				return result;
			}
			catch (Exception e)
			{
				error = e.ToString();
				return null;
			}
		}

		public IList<JobSetting> Jobs(out string error)
		{
			throw new NotImplementedException();
		}

		public IList<MapEnabled> Maps(TGMapListType type, out string error)
		{
			throw new NotImplementedException();
		}

		public string MoveServer(string new_location)
		{
			if (!Monitor.TryEnter(RepoLock))
				return "Repo locked!";
			try
			{
				if (RepoBusy)
					return "Repo busy!";
				if (!Monitor.TryEnter(ByondLock))
					return "BYOND locked";
				try
				{
					if (updateStat != TGByondStatus.Idle)
						return "BYOND busy!";
					if (!Monitor.TryEnter(CompilerLock))
						return "Compiler locked!";

					try
					{
						if (compilerCurrentStatus != TGCompilerStatus.Uninitialized || compilerCurrentStatus != TGCompilerStatus.Initialized)
							return "Compiler busy!";
						if (!Monitor.TryEnter(watchdogLock))
							return "Watchdog locked!";
						try
						{
							if (currentStatus != TGDreamDaemonStatus.Offline)
								return "Watchdog running!";
							var Config = Properties.Settings.Default;
							lock (configLock)
							{
								Directory.CreateDirectory(new_location);
								Environment.CurrentDirectory = new_location;
								Directory.Move(Config.ServerDirectory, new_location);
								Config.ServerDirectory = new_location;
								return null;
							}
						}
						finally
						{
							Monitor.Enter(watchdogLock);
						}
					}
					finally
					{
						Monitor.Exit(CompilerLock);
					}
				}
				finally
				{
					Monitor.Exit(ByondLock);
				}
			}
			catch (Exception e)
			{
				return e.ToString();
			}
			finally
			{
				Monitor.Exit(RepoLock);
			}
		}

		public ushort NudgePort(out string error)
		{
			throw new NotImplementedException();
		}

		public string RemoveEntry(TGStringConfig type, string entry)
		{
			throw new NotImplementedException();
		}

		public IList<ConfigSetting> Retrieve(TGConfigType type, out string error)
		{
			throw new NotImplementedException();
		}

		public string ServerDirectory()
		{
			return Environment.CurrentDirectory;
		}

		public string SetItem(TGConfigType type, string newValue)
		{
			throw new NotImplementedException();
		}

		public string SetJob(JobSetting job)
		{
			throw new NotImplementedException();
		}

		public string SetMap(TGMapListType type, MapEnabled mapfile)
		{
			throw new NotImplementedException();
		}

		public string SetNudgePort(ushort port)
		{
			try
			{
				lock (configLock) {
					File.WriteAllText(NudgeConfig, port.ToString());
				}
				return null;
			}catch(Exception e)
			{
				return e.ToString();
			}
		}
	}
}
