﻿using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace TGControlPanel
{
	partial class Main
	{
		enum RepoWorkerAction
		{
			Discover,
			Load,
			MergePR,
			UpdateOrigin,
			RevertToSha,
		}
		RepoWorkerAction RWA;

		//string RepoError;
	
		//int PRToMerge;
		//string ShaToRevert;

		private void InitRepoPage()
		{
			RWA = RepoWorkerAction.Discover;
			RepoBGW.ProgressChanged += RepoBGW_ProgressChanged;
			RepoBGW.RunWorkerCompleted += RepoBGW_RunWorkerCompleted;
			RepoBGW.RunWorkerAsync();
		}

		private void RepoBGW_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			PopulateRepoFields();
			RepoProgressBar.Visible = false;
			RemoteNameTitle.Visible = true;
			RepoRemoteTextBox.Visible = true;
			BranchNameTitle.Visible = true;
			RepoBranchTextBox.Visible = true;

			/*
			if (Repo != null)
			{
				//repo unavailable
				RepoProgressBarLabel.Text = "Unable to locate repository";
				CloneRepositoryButton.Visible = true;
			}
			else
			{
				RepoProgressBarLabel.Visible = false;

				CurrentRevisionLabel.Visible = true;
				CurrentRevisionTitle.Visible = true;
				IdentityLabel.Visible = true;
				CommiterNameTitle.Visible = true;
				CommitterEmailTitle.Visible = true;
				RepoCommitterNameTextBox.Visible = true;
				RepoEmailTextBox.Visible = true;
				TestMergeButton.Visible = true;
				TestMergeListLabel.Visible = true;
				TestMergeListTitle.Visible = true;
				UpdateRepoButton.Visible = true;
				UpdateToShaButton.Visible = true;
				RepoApplyButton.Visible = true;
			}
			*/
		}

		private void PopulateRepoFields()
		{
			var Config = Properties.Settings.Default;
			//CurrentRevisionLabel.Text = Repo != null ? Repo.GetCurrentSha() : "Unknown";
			RepoRemoteTextBox.Text = Config.RepoURL;
			RepoBranchTextBox.Text = Config.RepoBranch;
			RepoCommitterNameTextBox.Text = Config.CommitterName;
			RepoEmailTextBox.Text = Config.CommitterEmail;
		}

		private void RepoBGW_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			var percenttouse = e.ProgressPercentage;
			if (e.ProgressPercentage == 50)
			{
				RepoProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
				RepoProgressBarLabel.Text = "Indexing Objects...";
			}
			else if (e.ProgressPercentage > 50 && (RWA == RepoWorkerAction.Discover || RWA == RepoWorkerAction.Load))
				RepoProgressBarLabel.Text = "Checking out files...";
			percenttouse -= 50;

			if (percenttouse > 50)
				RepoProgressBar.Value = percenttouse;
		}
		/*
		private void RepoBGW_DoWork(object sender, DoWorkEventArgs e)
		{
			RepoError = null;
			switch (RWA) {
				case RepoWorkerAction.Discover:
					if (!Git.Exists())
						return;
					goto case RepoWorkerAction.Load;
				case RepoWorkerAction.Load:
					var Config = Properties.Settings.Default;
					//otherwise, clone the repo
					DisposeRepo();
					try
					{
						Repo = new Git(Config.RepoURL, Config.RepoBranch, Config.CommitterName, Config.CommitterEmail, RepoBGW);
					}
					catch(Exception E)
					{
						RepoError = E.ToString();
					}
					break;
				case RepoWorkerAction.MergePR:
					RepoError = Repo.MergePullRequest(PRToMerge);
					break;
				case RepoWorkerAction.RevertToSha:
					RepoError = Repo.ResetToSha(ShaToRevert);
					break;
				case RepoWorkerAction.UpdateOrigin:
					RepoError = Repo.UpdateToOrigin();
					break;
			};
		}
		*/
		private void CloneRepositoryButton_Click(object sender, EventArgs e)
		{
			RWA = RepoWorkerAction.Load;
			var Config = Properties.Settings.Default;

			Config.RepoURL = RepoRemoteTextBox.Text;
			RepoProgressBarLabel.Text = String.Format("Cloning into {0}", Config.RepoURL);
			Config.RepoBranch = RepoBranchTextBox.Text;

			CloneRepositoryButton.Visible = false;
			RemoteNameTitle.Visible = false;
			RepoRemoteTextBox.Visible = false;
			BranchNameTitle.Visible = false;
			RepoBranchTextBox.Visible = false;
			RepoProgressBar.Visible = true;
			RepoProgressBar.Value = 0;
			RepoProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
			RepoBGW.RunWorkerAsync();
		}
		private void DisposeRepo()
		{
			/*
			if (Repo != null)
			{
				Repo.Dispose();
				Repo = null;
			}
			*/
		}
		private void RepoApplyButton_Click(object sender, EventArgs e)
		{
			var Config = Properties.Settings.Default;
			var Reclone = Config.RepoURL != RepoRemoteTextBox.Text;
			if (Reclone)
			{
				var DialogResult = MessageBox.Show("Changing the remote URL requires a re-cloning of the repository. Continue?", "Confim", MessageBoxButtons.YesNo);
				if (DialogResult == DialogResult.No)
					return;
			}
			Config.RepoURL = RepoRemoteTextBox.Text;
			Config.RepoBranch = RepoBranchTextBox.Text;
			Config.CommitterName = RepoCommitterNameTextBox.Text;
			Config.CommitterEmail = RepoEmailTextBox.Text;

			Config.Save();

			DisposeRepo();

			//if (Reclone)
			//	Git.Delete();

			PopulateRepoFields();
		}
	}
}
