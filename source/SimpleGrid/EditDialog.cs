using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SimpleGrid;

public class EditDialog : Form
{
	private IContainer components;

	private Label lbName;

	private TextBox tbName;

	private Button btnOk;

	private Button btnCancel;

	public string NewName => tbName.Text;

	public EditDialog(string oldName)
	{
		InitializeComponent();
		tbName.Text = oldName;
	}

	private void btnOk_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.OK;
	}

	private void btnCancel_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.Cancel;
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
		System.ComponentModel.ComponentResourceManager componentResourceManager = new System.ComponentModel.ComponentResourceManager(typeof(SimpleGrid.EditDialog));
		this.lbName = new System.Windows.Forms.Label();
		this.tbName = new System.Windows.Forms.TextBox();
		this.btnOk = new System.Windows.Forms.Button();
		this.btnCancel = new System.Windows.Forms.Button();
		base.SuspendLayout();
		componentResourceManager.ApplyResources(this.lbName, "lbName");
		this.lbName.Name = "lbName";
		componentResourceManager.ApplyResources(this.tbName, "tbName");
		this.tbName.Name = "tbName";
		componentResourceManager.ApplyResources(this.btnOk, "btnOk");
		this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
		this.btnOk.Name = "btnOk";
		this.btnOk.UseVisualStyleBackColor = true;
		this.btnOk.Click += new System.EventHandler(btnOk_Click);
		componentResourceManager.ApplyResources(this.btnCancel, "btnCancel");
		this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.btnCancel.Name = "btnCancel";
		this.btnCancel.UseVisualStyleBackColor = true;
		this.btnCancel.Click += new System.EventHandler(btnCancel_Click);
		base.AcceptButton = this.btnOk;
		componentResourceManager.ApplyResources(this, "$this");
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.White;
		base.CancelButton = this.btnCancel;
		base.Controls.Add(this.btnCancel);
		base.Controls.Add(this.btnOk);
		base.Controls.Add(this.tbName);
		base.Controls.Add(this.lbName);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "EditDialog";
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
