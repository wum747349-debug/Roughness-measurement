using System;

namespace ConfocalMeter
{
    partial class MeasurementForm
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
            this.SuspendLayout();
            // 
            // MeasurementForm
            // 
            this.ClientSize = new System.Drawing.Size(278, 244);
            this.Name = "MeasurementForm";
            this.Load += new System.EventHandler(this.MeasurementForm_Load);
            this.ResumeLayout(false);

        }


        private void MeasurementForm_Load(object sender, EventArgs e)
        {

        }



        #endregion
    }
}