namespace AFPv2
{
    partial class Form1
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.ObsStart = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.timerMTmonSend = new System.Windows.Forms.Timer(this.components);
            this.timerSaveTimeOver = new System.Windows.Forms.Timer(this.components);
            this.ObsEndButton = new System.Windows.Forms.Button();
            this.ShowButton = new System.Windows.Forms.Button();
            this.CloseButton = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.timerDisplay = new System.Windows.Forms.Timer(this.components);
            this.timerSave = new System.Windows.Forms.Timer(this.components);
            this.ButtonSaveEnd = new System.Windows.Forms.Button();
            this.numericUpDownStarMin = new System.Windows.Forms.NumericUpDown();
            this.timerSavePost = new System.Windows.Forms.Timer(this.components);
            this.checkBoxObsAuto = new System.Windows.Forms.CheckBox();
            this.timerWaitShutdown = new System.Windows.Forms.Timer(this.components);
            this.checkBoxDispAvg = new System.Windows.Forms.CheckBox();
            this.label_X2Y2 = new System.Windows.Forms.Label();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabelFramerate = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelExposure = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelID = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelFailed = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelGain = new System.Windows.Forms.ToolStripStatusLabel();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownStarMin)).BeginInit();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // ObsStart
            // 
            this.ObsStart.Location = new System.Drawing.Point(12, 12);
            this.ObsStart.Name = "ObsStart";
            this.ObsStart.Size = new System.Drawing.Size(103, 52);
            this.ObsStart.TabIndex = 0;
            this.ObsStart.Text = "ObsStart";
            this.ObsStart.UseVisualStyleBackColor = true;
            this.ObsStart.Click += new System.EventHandler(this.Button5_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.pictureBox1);
            this.panel1.Location = new System.Drawing.Point(250, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1557, 1137);
            this.panel1.TabIndex = 1;
            // 
            // pictureBox1
            // 
            this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(1557, 1137);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // richTextBox1
            // 
            this.richTextBox1.Location = new System.Drawing.Point(250, 1146);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(1557, 182);
            this.richTextBox1.TabIndex = 2;
            this.richTextBox1.Text = "";
            // 
            // ObsEndButton
            // 
            this.ObsEndButton.Location = new System.Drawing.Point(12, 96);
            this.ObsEndButton.Name = "ObsEndButton";
            this.ObsEndButton.Size = new System.Drawing.Size(103, 51);
            this.ObsEndButton.TabIndex = 3;
            this.ObsEndButton.Text = "ObsEnd";
            this.ObsEndButton.UseVisualStyleBackColor = true;
            // 
            // ShowButton
            // 
            this.ShowButton.Location = new System.Drawing.Point(12, 399);
            this.ShowButton.Name = "ShowButton";
            this.ShowButton.Size = new System.Drawing.Size(75, 23);
            this.ShowButton.TabIndex = 4;
            this.ShowButton.Text = "Show";
            this.ShowButton.UseVisualStyleBackColor = true;
            // 
            // CloseButton
            // 
            this.CloseButton.Location = new System.Drawing.Point(12, 450);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(75, 23);
            this.CloseButton.TabIndex = 5;
            this.CloseButton.Text = "Close";
            this.CloseButton.UseVisualStyleBackColor = true;
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(12, 191);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(103, 33);
            this.buttonSave.TabIndex = 6;
            this.buttonSave.Text = "Save";
            this.buttonSave.UseVisualStyleBackColor = true;
            // 
            // timerSave
            // 
            this.timerSave.Interval = 10000;
            // 
            // ButtonSaveEnd
            // 
            this.ButtonSaveEnd.Location = new System.Drawing.Point(12, 260);
            this.ButtonSaveEnd.Name = "ButtonSaveEnd";
            this.ButtonSaveEnd.Size = new System.Drawing.Size(103, 49);
            this.ButtonSaveEnd.TabIndex = 7;
            this.ButtonSaveEnd.Text = "SaveEnd";
            this.ButtonSaveEnd.UseVisualStyleBackColor = true;
            // 
            // numericUpDownStarMin
            // 
            this.numericUpDownStarMin.Location = new System.Drawing.Point(124, 541);
            this.numericUpDownStarMin.Name = "numericUpDownStarMin";
            this.numericUpDownStarMin.Size = new System.Drawing.Size(120, 25);
            this.numericUpDownStarMin.TabIndex = 8;
            // 
            // checkBoxObsAuto
            // 
            this.checkBoxObsAuto.AutoSize = true;
            this.checkBoxObsAuto.Checked = true;
            this.checkBoxObsAuto.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxObsAuto.Location = new System.Drawing.Point(13, 334);
            this.checkBoxObsAuto.Name = "checkBoxObsAuto";
            this.checkBoxObsAuto.Size = new System.Drawing.Size(171, 22);
            this.checkBoxObsAuto.TabIndex = 9;
            this.checkBoxObsAuto.Text = "checkBoxObsAuto";
            this.checkBoxObsAuto.UseVisualStyleBackColor = true;
            // 
            // timerWaitShutdown
            // 
            this.timerWaitShutdown.Interval = 10000;
            // 
            // checkBoxDispAvg
            // 
            this.checkBoxDispAvg.AutoEllipsis = true;
            this.checkBoxDispAvg.AutoSize = true;
            this.checkBoxDispAvg.Location = new System.Drawing.Point(12, 501);
            this.checkBoxDispAvg.Name = "checkBoxDispAvg";
            this.checkBoxDispAvg.Size = new System.Drawing.Size(167, 22);
            this.checkBoxDispAvg.TabIndex = 10;
            this.checkBoxDispAvg.Text = "checkBoxDispAvg";
            this.checkBoxDispAvg.UseVisualStyleBackColor = true;
            // 
            // label_X2Y2
            // 
            this.label_X2Y2.AutoSize = true;
            this.label_X2Y2.Font = new System.Drawing.Font("MS UI Gothic", 11F);
            this.label_X2Y2.Location = new System.Drawing.Point(9, 699);
            this.label_X2Y2.Name = "label_X2Y2";
            this.label_X2Y2.Size = new System.Drawing.Size(107, 22);
            this.label_X2Y2.TabIndex = 11;
            this.label_X2Y2.Text = "label_X2Y2";
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabelFramerate,
            this.toolStripStatusLabelExposure,
            this.toolStripStatusLabelID,
            this.toolStripStatusLabelFailed,
            this.toolStripStatusLabelGain});
            this.statusStrip1.Location = new System.Drawing.Point(0, 1323);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(2744, 30);
            this.statusStrip1.TabIndex = 13;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabelFramerate
            // 
            this.toolStripStatusLabelFramerate.Name = "toolStripStatusLabelFramerate";
            this.toolStripStatusLabelFramerate.Size = new System.Drawing.Size(90, 25);
            this.toolStripStatusLabelFramerate.Text = "Framerate";
            // 
            // toolStripStatusLabelExposure
            // 
            this.toolStripStatusLabelExposure.Name = "toolStripStatusLabelExposure";
            this.toolStripStatusLabelExposure.Size = new System.Drawing.Size(84, 25);
            this.toolStripStatusLabelExposure.Text = "Exposure";
            // 
            // toolStripStatusLabelID
            // 
            this.toolStripStatusLabelID.Name = "toolStripStatusLabelID";
            this.toolStripStatusLabelID.Size = new System.Drawing.Size(78, 25);
            this.toolStripStatusLabelID.Text = "FrameID";
            // 
            // toolStripStatusLabelFailed
            // 
            this.toolStripStatusLabelFailed.Name = "toolStripStatusLabelFailed";
            this.toolStripStatusLabelFailed.Size = new System.Drawing.Size(55, 25);
            this.toolStripStatusLabelFailed.Text = "failed";
            // 
            // toolStripStatusLabelGain
            // 
            this.toolStripStatusLabelGain.Name = "toolStripStatusLabelGain";
            this.toolStripStatusLabelGain.Size = new System.Drawing.Size(53, 25);
            this.toolStripStatusLabelGain.Text = "Gaim";
            // 
            // Form1
            // 
            this.ClientSize = new System.Drawing.Size(2744, 1353);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.label_X2Y2);
            this.Controls.Add(this.checkBoxDispAvg);
            this.Controls.Add(this.checkBoxObsAuto);
            this.Controls.Add(this.numericUpDownStarMin);
            this.Controls.Add(this.ButtonSaveEnd);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.ShowButton);
            this.Controls.Add(this.ObsEndButton);
            this.Controls.Add(this.richTextBox1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.ObsStart);
            this.Name = "Form1";
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownStarMin)).EndInit();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button ObsStart;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Timer timerMTmonSend;
        private System.Windows.Forms.Timer timerSaveTimeOver;
        private System.Windows.Forms.Button ObsEndButton;
        private System.Windows.Forms.Button ShowButton;
        private System.Windows.Forms.Button CloseButton;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Timer timerDisplay;
        private System.Windows.Forms.Timer timerSave;
        private System.Windows.Forms.Button ButtonSaveEnd;
        private System.Windows.Forms.NumericUpDown numericUpDownStarMin;
        private System.Windows.Forms.Timer timerSavePost;
        private System.Windows.Forms.CheckBox checkBoxObsAuto;
        private System.Windows.Forms.Timer timerWaitShutdown;
        private System.Windows.Forms.CheckBox checkBoxDispAvg;
        private System.Windows.Forms.Label label_X2Y2;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelFramerate;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelExposure;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelID;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelFailed;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelGain;
    }
}

