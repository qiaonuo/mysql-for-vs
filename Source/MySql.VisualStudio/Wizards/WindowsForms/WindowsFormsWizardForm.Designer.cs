﻿namespace MySql.Data.VisualStudio.Wizards.WindowsForms
{
  partial class WindowsFormsWizardForm
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.dataAccessConfig1 = new MySql.Data.VisualStudio.Wizards.WindowsForms.DataAccessConfig();
      this.validationConfig1 = new MySql.Data.VisualStudio.Wizards.WindowsForms.ValidationConfig();
      this.SuspendLayout();
      // 
      // btnFinish
      // 
      this.btnFinish.Location = new System.Drawing.Point(757, 454);
      // 
      // btnNext
      // 
      this.btnNext.Location = new System.Drawing.Point(677, 454);
      // 
      // btnCancel
      // 
      this.btnCancel.Location = new System.Drawing.Point(515, 454);
      // 
      // btnBack
      // 
      this.btnBack.Location = new System.Drawing.Point(596, 454);
      // 
      // dataAccessConfig1
      // 
      this.dataAccessConfig1.Location = new System.Drawing.Point(5, 3);
      this.dataAccessConfig1.Name = "dataAccessConfig1";
      this.dataAccessConfig1.Size = new System.Drawing.Size(858, 474);
      this.dataAccessConfig1.TabIndex = 4;
      // 
      // validationConfig1
      // 
      this.validationConfig1.Location = new System.Drawing.Point(5, 4);
      this.validationConfig1.Name = "validationConfig1";
      this.validationConfig1.Size = new System.Drawing.Size(861, 449);
      this.validationConfig1.TabIndex = 12;
      // 
      // WindowsFormsWizardForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(869, 491);
      this.Controls.Add(this.validationConfig1);
      this.Controls.Add(this.dataAccessConfig1);
      this.Name = "WindowsFormsWizardForm";
      this.Text = "Windows Forms Configuration ";
      this.Load += new System.EventHandler(this.WizardForm_Load);
      this.Controls.SetChildIndex(this.dataAccessConfig1, 0);
      this.Controls.SetChildIndex(this.btnNext, 0);
      this.Controls.SetChildIndex(this.btnBack, 0);
      this.Controls.SetChildIndex(this.btnCancel, 0);
      this.Controls.SetChildIndex(this.btnFinish, 0);
      this.Controls.SetChildIndex(this.validationConfig1, 0);
      this.ResumeLayout(false);

    }

    #endregion

    private DataAccessConfig dataAccessConfig1;
    private ValidationConfig validationConfig1;
  }
}