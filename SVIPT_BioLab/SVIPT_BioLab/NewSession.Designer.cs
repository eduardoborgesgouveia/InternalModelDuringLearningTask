﻿namespace SVIPT_BioLab
{
    partial class NewSession
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
			this.label1 = new System.Windows.Forms.Label();
			this.textBox_IdSession = new System.Windows.Forms.TextBox();
			this.textBox_IdVolunteer = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button_ok = new System.Windows.Forms.Button();
			this.button_Cancel = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.label1.Location = new System.Drawing.Point(12, 27);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(181, 20);
			this.label1.TabIndex = 0;
			this.label1.Text = "Identificador da Sessão:";
			// 
			// textBox_IdSession
			// 
			this.textBox_IdSession.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.textBox_IdSession.Location = new System.Drawing.Point(192, 25);
			this.textBox_IdSession.Name = "textBox_IdSession";
			this.textBox_IdSession.Size = new System.Drawing.Size(286, 24);
			this.textBox_IdSession.TabIndex = 1;
			this.textBox_IdSession.Text = "Nononono";
			// 
			// textBox_IdVolunteer
			// 
			this.textBox_IdVolunteer.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.textBox_IdVolunteer.Location = new System.Drawing.Point(192, 55);
			this.textBox_IdVolunteer.Name = "textBox_IdVolunteer";
			this.textBox_IdVolunteer.Size = new System.Drawing.Size(286, 24);
			this.textBox_IdVolunteer.TabIndex = 3;
			this.textBox_IdVolunteer.Text = "Nononono";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.label2.Location = new System.Drawing.Point(12, 57);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(108, 20);
			this.label2.TabIndex = 2;
			this.label2.Text = "Voluntário(a): ";
			// 
			// button_ok
			// 
			this.button_ok.Location = new System.Drawing.Point(381, 108);
			this.button_ok.Name = "button_ok";
			this.button_ok.Size = new System.Drawing.Size(121, 34);
			this.button_ok.TabIndex = 4;
			this.button_ok.Text = "Criar nova sessão";
			this.button_ok.UseVisualStyleBackColor = true;
			this.button_ok.Click += new System.EventHandler(this.button_ok_Click);
			// 
			// button_Cancel
			// 
			this.button_Cancel.Location = new System.Drawing.Point(266, 108);
			this.button_Cancel.Name = "button_Cancel";
			this.button_Cancel.Size = new System.Drawing.Size(87, 34);
			this.button_Cancel.TabIndex = 5;
			this.button_Cancel.Text = "Cancelar";
			this.button_Cancel.UseVisualStyleBackColor = true;
			this.button_Cancel.Click += new System.EventHandler(this.button_Cancel_Click);
			// 
			// NewSession
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(514, 166);
			this.Controls.Add(this.button_Cancel);
			this.Controls.Add(this.button_ok);
			this.Controls.Add(this.textBox_IdVolunteer);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.textBox_IdSession);
			this.Controls.Add(this.label1);
			this.Name = "NewSession";
			this.Text = "NewSession";
			this.Load += new System.EventHandler(this.NewSession_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox_IdSession;
        private System.Windows.Forms.TextBox textBox_IdVolunteer;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button button_ok;
        private System.Windows.Forms.Button button_Cancel;
    }
}