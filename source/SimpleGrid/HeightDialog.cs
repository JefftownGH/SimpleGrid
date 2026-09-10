using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SimpleGrid;

public class HeightDialog : Form
{
	private IContainer components;

	private Button btnCancel;

	private Button btnOk;

	private TrackBar trackBarTitleHeight;

	private Label labelTitleHeight;

	private Button btnRestore;

	public int TitleHeight => trackBarTitleHeight.Value;

	public HeightDialog(int val)
	{
		InitializeComponent();
		trackBarTitleHeight.Value = val;
		UpdateText();
	}

	private void UpdateText()
	{
		labelTitleHeight.Text = trackBarTitleHeight.Value + "px";
	}

	private void trackBarTitleHeight_Scroll(object sender, EventArgs e)
	{
		UpdateText();
	}

	private void btnRestore_Click(object sender, EventArgs e)
	{
		trackBarTitleHeight.Value = 35;
		UpdateText();
	}

	private void HeightDialog_Load(object sender, EventArgs e)
	{
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		System.ComponentModel.ComponentResourceManager componentResourceManager = new System.ComponentModel.ComponentResourceManager(typeof(SimpleGrid.HeightDialog));
		this.btnCancel = new System.Windows.Forms.Button();
		this.btnOk = new System.Windows.Forms.Button();
		this.trackBarTitleHeight = new System.Windows.Forms.TrackBar();
		this.labelTitleHeight = new System.Windows.Forms.Label();
		this.btnRestore = new System.Windows.Forms.Button();
		((System.ComponentModel.ISupportInitialize)this.trackBarTitleHeight).BeginInit();
		base.SuspendLayout();
		componentResourceManager.ApplyResources(this.btnCancel, "btnCancel");
		this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.btnCancel.Name = "btnCancel";
		this.btnCancel.UseVisualStyleBackColor = true;
		componentResourceManager.ApplyResources(this.btnOk, "btnOk");
		this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
		this.btnOk.Name = "btnOk";
		this.btnOk.UseVisualStyleBackColor = true;
		componentResourceManager.ApplyResources(this.trackBarTitleHeight, "trackBarTitleHeight");
		this.trackBarTitleHeight.Maximum = 100;
		this.trackBarTitleHeight.Minimum = 20;
		this.trackBarTitleHeight.Name = "trackBarTitleHeight";
		this.trackBarTitleHeight.Value = 20;
		this.trackBarTitleHeight.Scroll += new System.EventHandler(trackBarTitleHeight_Scroll);
		componentResourceManager.ApplyResources(this.labelTitleHeight, "labelTitleHeight");
		this.labelTitleHeight.Name = "labelTitleHeight";
		componentResourceManager.ApplyResources(this.btnRestore, "btnRestore");
		this.btnRestore.Name = "btnRestore";
		this.btnRestore.UseVisualStyleBackColor = true;
		this.btnRestore.Click += new System.EventHandler(btnRestore_Click);
		componentResourceManager.ApplyResources(this, "$this");
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.White;
		base.Controls.Add(this.btnRestore);
		base.Controls.Add(this.labelTitleHeight);
		base.Controls.Add(this.trackBarTitleHeight);
		base.Controls.Add(this.btnCancel);
		base.Controls.Add(this.btnOk);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "HeightDialog";
		base.Load += new System.EventHandler(HeightDialog_Load);
		((System.ComponentModel.ISupportInitialize)this.trackBarTitleHeight).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
