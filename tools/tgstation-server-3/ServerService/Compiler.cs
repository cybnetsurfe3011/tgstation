﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using TGServiceInterface;

namespace TGServerService
{
	partial class TGStationServer : ITGCompiler
	{
		#region Win32 Shit
		[DllImport("kernel32.dll")]
		static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);
		enum SymbolicLink
		{
			File = 0,
			Directory = 1
		}
		#endregion

		const string StaticDirs = "Static";
		const string StaticDataDir = StaticDirs + "/data";
		const string StaticConfigDir = StaticDirs + "/config";
		const string StaticLogDir = StaticDirs + "/logs";
		const string StaticBackupDir = "Static_BACKUP";

		const string LibMySQLFile = "/libmysql.dll";

		const string GameDir = "Game";
		const string GameDirA = GameDir + "/A";
		const string GameDirB = GameDir + "/B";
		const string GameDirLive = GameDir + "/Live";

		const string LiveFile = "/TestLive.lk";
		const string ADirTest = GameDirA + LiveFile;
		const string BDirTest = GameDirB + LiveFile;
		const string LiveDirTest = GameDirLive + LiveFile;


		List<string> copyExcludeList = new List<string> { ".git", "data", "config", "libmysql.dll" };   //shit we handle

		object CompilerLock = new object();
		TGCompilerStatus compilerCurrentStatus;
		string lastCompilerError;
		
		Thread CompilerThread;
	
		void InitCompiler()
		{
			if(File.Exists(LiveDirTest))
				File.Delete(LiveDirTest);
			compilerCurrentStatus = IsInitialized();
		}

		public TGCompilerStatus GetStatus()
		{
			lock (CompilerLock)
			{
				return compilerCurrentStatus;
			}
		}

		public string CompileError()
		{
			lock (CompilerLock)
			{
				var err = lastCompilerError;
				lastCompilerError = null;
				return err;
			}
		}

		void DisposeCompiler()
		{
			lock (CompilerLock)
			{
				if (CompilerThread == null || !CompilerThread.IsAlive)
					return;
				CompilerThread.Abort(); //this will safely kill dm
			}
		}

		void CreateSymlink(string link, string target)
		{
			if (!CreateSymbolicLink(new DirectoryInfo(link).FullName, new DirectoryInfo(target).FullName, File.Exists(target) ? SymbolicLink.File : SymbolicLink.Directory))
				throw new Exception(String.Format("Failed to create symlink from {0} to {1}!", target, link));
		}

		public bool Initialize()
		{
			lock (CompilerLock)
			{
				if (compilerCurrentStatus == TGCompilerStatus.Initializing || compilerCurrentStatus == TGCompilerStatus.Compiling)
					return false;
				lastCompilerError = null;
				compilerCurrentStatus = TGCompilerStatus.Initializing;
				CompilerThread = new Thread(new ThreadStart(InitializeImpl));
				CompilerThread.Start();
				return true;
			}
		}

		TGCompilerStatus IsInitialized()
		{
			if (File.Exists(GameDirB + LibMySQLFile))	//its a good tell, jim
				return TGCompilerStatus.Initialized;
			return TGCompilerStatus.Uninitialized;
		}

		public void InitializeImpl()
		{
			try
			{
				if (DaemonStatus() != TGDreamDaemonStatus.Offline)
				{
					lock (CompilerLock)
					{
						lastCompilerError = "Dream daemon must not be running";
						compilerCurrentStatus = IsInitialized();
						return;
					}
				}

				if (!Exists()) //repo
				{
					lock (CompilerLock)
					{
						lastCompilerError = "Repository is not setup!";
						compilerCurrentStatus = IsInitialized();
						return;
					}
				}
				try
				{
					SendMessage("DM: Setting up symlinks...");

					if (Directory.Exists(GameDirB + LibMySQLFile))
						Directory.Delete(GameDirB + LibMySQLFile);

					if (Directory.Exists(GameDirLive))
						Directory.Delete(GameDirLive);

					if (Directory.Exists(GameDirA + "/data"))
						Directory.Delete(GameDirA + "/data");

					if (Directory.Exists(GameDirA + "/config"))
						Directory.Delete(GameDirA + "/config");

					if (Directory.Exists(GameDirA + LibMySQLFile))
						Directory.Delete(GameDirA + LibMySQLFile);

					if (Directory.Exists(GameDirB + "/data"))
						Directory.Delete(GameDirB + "/data");

					if (Directory.Exists(GameDirB + "/config"))
						Directory.Delete(GameDirB + "/config");

					Program.DeleteDirectory(GameDir);

					Directory.CreateDirectory(GameDirA + "/.git/logs");

					if (!Monitor.TryEnter(RepoLock))
						lock (CompilerLock)
						{
							lastCompilerError = "Unable to lock repository!";
							compilerCurrentStatus = TGCompilerStatus.Uninitialized;
							return;
						}
					try
					{
						Program.CopyDirectory(RepoPath, GameDirA, copyExcludeList);
						//just the tip
						const string HeadFile = "/.git/logs/HEAD";
						File.Copy(RepoPath + HeadFile, GameDirA + HeadFile);
					}
					finally
					{
						Monitor.Exit(RepoLock);
					}

					Program.CopyDirectory(GameDirA, GameDirB);

					CreateSymlink(GameDirA + "/data", StaticDataDir);
					CreateSymlink(GameDirB + "/data", StaticDataDir);

					CreateSymlink(GameDirA + "/config", StaticConfigDir);
					CreateSymlink(GameDirB + "/config", StaticConfigDir);

					CreateSymlink(GameDirA + LibMySQLFile, StaticDirs + LibMySQLFile);
					CreateSymlink(GameDirB + LibMySQLFile, StaticDirs + LibMySQLFile);

					Program.Shell("pip install PyYaml");
					Program.Shell("pip install beautifulsoup4");

					SendMessage("DM: Symlinks set up!");
					lock (CompilerLock)
					{
						compilerCurrentStatus = TGCompilerStatus.Initialized;
					}
				}
				catch (ThreadAbortException)
				{
					return;
				}
				catch (Exception e)
				{
					lock (CompilerLock)
					{
						SendMessage("DM: Setup failed!");
						lastCompilerError = e.ToString();
						compilerCurrentStatus = TGCompilerStatus.Uninitialized;
						return;
					}
				}
			}
			catch (ThreadAbortException)
			{
				return;
			}
		}		

		string GetStagingDir()
		{
			string TheDir;
			if (!Directory.Exists(GameDirLive))
				TheDir = GameDirA;
			else
			{
				File.Create(LiveDirTest).Close();
				try
				{
					if (File.Exists(ADirTest))
						TheDir = GameDirA;
					else if (File.Exists(BDirTest))
						TheDir = GameDirB;
					else
						throw new Exception("Unable to determine current live directory!");
				}
				finally
				{
					File.Delete(LiveDirTest);
				}


				TheDir = InvertDirectory(TheDir);

			}
			//So TheDir is what the Live folder is NOT pointing to
			//Now we need to check if DD is running that folder and swap it if necessary

			var rsclock = TheDir + "/" + Properties.Settings.Default.ProjectName + ".rsc.lk";
			if (File.Exists(rsclock))
			{
				try
				{
					File.Delete(rsclock);
				}
				catch	//held open by byond
				{
					return InvertDirectory(TheDir);
				}
			}
			return TheDir;
		}
		string InvertDirectory(string gameDirectory)
		{
			if (gameDirectory == GameDirA)
				return GameDirB;
			else
				return GameDirA;
		}

		void CompileImpl()
		{
			try
			{
				SendMessage("DM: Updating from repository...");
				var resurrectee = GetStagingDir();

				//clear out the syms first
				if (Directory.Exists(resurrectee + "/data"))
					Directory.Delete(resurrectee + "/data");

				if (Directory.Exists(resurrectee + "/config"))
					Directory.Delete(resurrectee + "/config");

				if (File.Exists(resurrectee + LibMySQLFile))
					File.Delete(resurrectee + LibMySQLFile);

				Program.DeleteDirectory(resurrectee);

				Directory.CreateDirectory(resurrectee + "/.git/logs");

				CreateSymlink(resurrectee + "/data", StaticDataDir);
				CreateSymlink(resurrectee + "/config", StaticConfigDir);
				CreateSymlink(resurrectee + LibMySQLFile, StaticDirs + LibMySQLFile);

				if (!Monitor.TryEnter(RepoLock))
				{
					SendMessage("DM: Copy aborted, repo locked!");
					lock (CompilerLock)
					{
						lastCompilerError = "The repo could not be locked for copying";
						compilerCurrentStatus = TGCompilerStatus.Initialized;	//still fairly valid
						return;
					}
				}
				try
				{
					Program.CopyDirectory(RepoPath, resurrectee, copyExcludeList);
					//just the tip
					const string HeadFile = "/.git/logs/HEAD";
					File.Copy(RepoPath + HeadFile, resurrectee + HeadFile);
				}
				finally
				{
					Monitor.Exit(RepoLock);
				}

				SendMessage("DM: Repo copy complete, compiling...");

				using (var DM = new Process())  //will kill the process if the thread is terminated
				{
					DM.StartInfo.FileName = ByondDirectory + "/bin/dm.exe";
					DM.StartInfo.Arguments = resurrectee + "/" + Properties.Settings.Default.ProjectName + ".dme";
					DM.StartInfo.UseShellExecute = false;
					DM.Start();
					DM.WaitForExit();

					if (DM.ExitCode == 0)
					{
						//these two lines should be atomic but this is the best we can do
						if (Directory.Exists(GameDirLive))
							Directory.Delete(GameDirLive);
						CreateSymlink(GameDirLive, resurrectee);

						SendMessage("DM: Compile complete, server will update next round!");
					}
					else
						SendMessage("DM: Compile failed!"); //Also happens for warnings
				}

			}
			catch (ThreadAbortException)
			{
				return;
			}
			catch (Exception e)
			{
				SendMessage("DM: Compiler thread crashed!");
				TGServerService.ActiveService.EventLog.WriteEntry("Compile manager errror: " + e.ToString(), EventLogEntryType.Error);
				lock (CompilerLock)
				{
					lastCompilerError = e.ToString();
					compilerCurrentStatus = TGCompilerStatus.Initialized;   //still fairly valid
				}
			}
		}

		public bool Compile()
		{
			lock (CompilerLock)
			{
				if (GetVersion(false) == null || compilerCurrentStatus == TGCompilerStatus.Initializing || compilerCurrentStatus == TGCompilerStatus.Compiling)
					return false;
				lastCompilerError = null;
				compilerCurrentStatus = TGCompilerStatus.Compiling;
				CompilerThread = new Thread(new ThreadStart(CompileImpl));
				CompilerThread.Start();
			}
			return true;
		}
	}
}
