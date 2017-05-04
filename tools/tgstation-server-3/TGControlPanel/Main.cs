﻿using System.Windows.Forms;

namespace TGControlPanel
{
	public partial class Main : Form
	{
		public Main()
		{
			InitializeComponent();
			Panels.SelectedIndexChanged += Panels_SelectedIndexChanged;
			Panels.SelectedIndex += Properties.Settings.Default.LastPageIndex;
			InitRepoPage();
			InitBYONDPage();
			InitServerPage();
		}

		private void Panels_SelectedIndexChanged(object sender, System.EventArgs e)
		{
			Properties.Settings.Default.LastPageIndex = Panels.SelectedIndex;
		}
	}
}
