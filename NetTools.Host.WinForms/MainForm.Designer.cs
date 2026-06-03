namespace NetTools.Host.WinForms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.listPlugins = new System.Windows.Forms.ListBox();
            this.lblPlugins = new System.Windows.Forms.Label();
            this.panelToolHost = new System.Windows.Forms.Panel();
            this.statusLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.SuspendLayout();
            //
            // splitContainerMain
            //
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainerMain.Location = new System.Drawing.Point(0, 0);
            this.splitContainerMain.Name = "splitContainerMain";
            //
            // splitContainerMain.Panel1
            //
            this.splitContainerMain.Panel1.Controls.Add(this.listPlugins);
            this.splitContainerMain.Panel1.Controls.Add(this.lblPlugins);
            //
            // splitContainerMain.Panel2
            //
            this.splitContainerMain.Panel2.Controls.Add(this.panelToolHost);
            this.splitContainerMain.Size = new System.Drawing.Size(984, 561);
            this.splitContainerMain.SplitterDistance = 220;
            this.splitContainerMain.TabIndex = 0;
            //
            // listPlugins
            //
            this.listPlugins.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listPlugins.FormattingEnabled = true;
            this.listPlugins.IntegralHeight = false;
            this.listPlugins.ItemHeight = 16;
            this.listPlugins.Location = new System.Drawing.Point(0, 23);
            this.listPlugins.Name = "listPlugins";
            this.listPlugins.Size = new System.Drawing.Size(220, 538);
            this.listPlugins.TabIndex = 1;
            this.listPlugins.SelectedIndexChanged += new System.EventHandler(this.listPlugins_SelectedIndexChanged);
            //
            // lblPlugins
            //
            this.lblPlugins.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblPlugins.Location = new System.Drawing.Point(0, 0);
            this.lblPlugins.Name = "lblPlugins";
            this.lblPlugins.Padding = new System.Windows.Forms.Padding(8, 4, 0, 0);
            this.lblPlugins.Size = new System.Drawing.Size(220, 23);
            this.lblPlugins.TabIndex = 0;
            this.lblPlugins.Text = "Network Tools";
            //
            // panelToolHost
            //
            this.panelToolHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelToolHost.Location = new System.Drawing.Point(0, 0);
            this.panelToolHost.Name = "panelToolHost";
            this.panelToolHost.Padding = new System.Windows.Forms.Padding(8);
            this.panelToolHost.Size = new System.Drawing.Size(760, 561);
            this.panelToolHost.TabIndex = 0;
            //
            // statusLabel
            //
            this.statusLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.statusLabel.Location = new System.Drawing.Point(0, 561);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Padding = new System.Windows.Forms.Padding(8, 4, 0, 0);
            this.statusLabel.Size = new System.Drawing.Size(984, 24);
            this.statusLabel.TabIndex = 1;
            this.statusLabel.Text = "Ready";
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 585);
            this.Controls.Add(this.splitContainerMain);
            this.Controls.Add(this.statusLabel);
            this.MinimumSize = new System.Drawing.Size(800, 500);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "NetTools";
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.ListBox listPlugins;
        private System.Windows.Forms.Label lblPlugins;
        private System.Windows.Forms.Panel panelToolHost;
        private System.Windows.Forms.Label statusLabel;
    }
}
