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
            this.toolStripStatusLabelTemp = new System.Windows.Forms.ToolStripStatusLabel();
            this.buttonMove = new System.Windows.Forms.Button();
            this.numericUpDown_daz = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown_dalt = new System.Windows.Forms.NumericUpDown();
            this.checkBox_DispMode = new System.Windows.Forms.CheckBox();
            this.numericUpDownStarCount = new System.Windows.Forms.NumericUpDown();
            this.button_mask = new System.Windows.Forms.Button();
            this.button_test = new System.Windows.Forms.Button();
            this.timerObsOnOff = new System.Windows.Forms.Timer(this.components);
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownStarMin)).BeginInit();
            this.statusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_daz)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_dalt)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownStarCount)).BeginInit();
            this.SuspendLayout();
            // 
            // ObsStart
            // 
            this.ObsStart.Font = new System.Drawing.Font("MS UI Gothic", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.ObsStart.Location = new System.Drawing.Point(12, 12);
            this.ObsStart.Name = "ObsStart";
            this.ObsStart.Size = new System.Drawing.Size(103, 52);
            this.ObsStart.TabIndex = 0;
            this.ObsStart.Text = "ObsStart";
            this.ObsStart.UseVisualStyleBackColor = true;
            this.ObsStart.Click += new System.EventHandler(this.ObsStart_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.pictureBox1);
            this.panel1.Controls.Add(this.richTextBox1);
            this.panel1.Location = new System.Drawing.Point(250, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1557, 1137);
            this.panel1.TabIndex = 1;
            // 
            // pictureBox1
            // 
            this.pictureBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(1313, 904);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // richTextBox1
            // 
            this.richTextBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.richTextBox1.Location = new System.Drawing.Point(0, 901);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(1313, 182);
            this.richTextBox1.TabIndex = 2;
            this.richTextBox1.Text = "";
            // 
            // timerMTmonSend
            // 
            this.timerMTmonSend.Tick += new System.EventHandler(this.timerMTmonSend_Tick);
            // 
            // timerSaveTimeOver
            // 
            this.timerSaveTimeOver.Tick += new System.EventHandler(this.timerSaveTimeOver_Tick);
            // 
            // ObsEndButton
            // 
            this.ObsEndButton.Font = new System.Drawing.Font("MS UI Gothic", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.ObsEndButton.Location = new System.Drawing.Point(12, 96);
            this.ObsEndButton.Name = "ObsEndButton";
            this.ObsEndButton.Size = new System.Drawing.Size(103, 51);
            this.ObsEndButton.TabIndex = 3;
            this.ObsEndButton.Text = "ObsEnd";
            this.ObsEndButton.UseVisualStyleBackColor = true;
            this.ObsEndButton.Click += new System.EventHandler(this.ObsEndButton_Click);
            // 
            // ShowButton
            // 
            this.ShowButton.Font = new System.Drawing.Font("MS UI Gothic", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.ShowButton.Location = new System.Drawing.Point(12, 399);
            this.ShowButton.Name = "ShowButton";
            this.ShowButton.Size = new System.Drawing.Size(103, 33);
            this.ShowButton.TabIndex = 4;
            this.ShowButton.Text = "Show";
            this.ShowButton.UseVisualStyleBackColor = true;
            this.ShowButton.Click += new System.EventHandler(this.ShowButton_Click);
            // 
            // CloseButton
            // 
            this.CloseButton.Font = new System.Drawing.Font("MS UI Gothic", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.CloseButton.Location = new System.Drawing.Point(12, 450);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(103, 45);
            this.CloseButton.TabIndex = 5;
            this.CloseButton.Text = "Close";
            this.CloseButton.UseVisualStyleBackColor = true;
            this.CloseButton.Click += new System.EventHandler(this.CloseButton_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Font = new System.Drawing.Font("MS UI Gothic", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.buttonSave.Location = new System.Drawing.Point(12, 191);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(103, 45);
            this.buttonSave.TabIndex = 6;
            this.buttonSave.Text = "Save";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.ButtonSave_Click);
            // 
            // timerDisplay
            // 
            this.timerDisplay.Interval = 200;
            this.timerDisplay.Tick += new System.EventHandler(this.timerDisplay_Tick);
            // 
            // timerSave
            // 
            this.timerSave.Interval = 10000;
            this.timerSave.Tick += new System.EventHandler(this.timerSave_Tick);
            // 
            // ButtonSaveEnd
            // 
            this.ButtonSaveEnd.Font = new System.Drawing.Font("MS UI Gothic", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.ButtonSaveEnd.Location = new System.Drawing.Point(12, 260);
            this.ButtonSaveEnd.Name = "ButtonSaveEnd";
            this.ButtonSaveEnd.Size = new System.Drawing.Size(103, 49);
            this.ButtonSaveEnd.TabIndex = 7;
            this.ButtonSaveEnd.Text = "SaveEnd";
            this.ButtonSaveEnd.UseVisualStyleBackColor = true;
            this.ButtonSaveEnd.Click += new System.EventHandler(this.ButtonSaveEnd_Click);
            // 
            // numericUpDownStarMin
            // 
            this.numericUpDownStarMin.Location = new System.Drawing.Point(124, 541);
            this.numericUpDownStarMin.Name = "numericUpDownStarMin";
            this.numericUpDownStarMin.Size = new System.Drawing.Size(120, 19);
            this.numericUpDownStarMin.TabIndex = 8;
            // 
            // timerSavePost
            // 
            this.timerSavePost.Tick += new System.EventHandler(this.timerSavePostTime_Tick);
            // 
            // checkBoxObsAuto
            // 
            this.checkBoxObsAuto.AutoSize = true;
            this.checkBoxObsAuto.Checked = true;
            this.checkBoxObsAuto.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxObsAuto.Location = new System.Drawing.Point(13, 334);
            this.checkBoxObsAuto.Name = "checkBoxObsAuto";
            this.checkBoxObsAuto.Size = new System.Drawing.Size(125, 21);
            this.checkBoxObsAuto.TabIndex = 9;
            this.checkBoxObsAuto.Text = "checkBoxObsAuto";
            this.checkBoxObsAuto.UseVisualStyleBackColor = true;
            this.checkBoxObsAuto.CheckedChanged += new System.EventHandler(this.checkBoxObsAuto_CheckedChanged);
            // 
            // timerWaitShutdown
            // 
            this.timerWaitShutdown.Interval = 10000;
            this.timerWaitShutdown.Tick += new System.EventHandler(this.timerWaitShutdown_Tick);
            // 
            // checkBoxDispAvg
            // 
            this.checkBoxDispAvg.AutoSize = true;
            this.checkBoxDispAvg.Location = new System.Drawing.Point(12, 501);
            this.checkBoxDispAvg.Name = "checkBoxDispAvg";
            this.checkBoxDispAvg.Size = new System.Drawing.Size(124, 21);
            this.checkBoxDispAvg.TabIndex = 10;
            this.checkBoxDispAvg.Text = "checkBoxDispAvg";
            this.checkBoxDispAvg.UseVisualStyleBackColor = true;
            // 
            // label_X2Y2
            // 
            this.label_X2Y2.AutoSize = true;
            this.label_X2Y2.Font = new System.Drawing.Font("MS UI Gothic", 11F);
            this.label_X2Y2.Location = new System.Drawing.Point(8, 744);
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
            this.toolStripStatusLabelGain,
            this.toolStripStatusLabelTemp});
            this.statusStrip1.Location = new System.Drawing.Point(0, 1074);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1565, 45);
            this.statusStrip1.TabIndex = 13;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabelFramerate
            // 
            this.toolStripStatusLabelFramerate.Font = new System.Drawing.Font("Yu Gothic UI", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.toolStripStatusLabelFramerate.Name = "toolStripStatusLabelFramerate";
            this.toolStripStatusLabelFramerate.Size = new System.Drawing.Size(141, 38);
            this.toolStripStatusLabelFramerate.Text = "Framerate";
            // 
            // toolStripStatusLabelExposure
            // 
            this.toolStripStatusLabelExposure.Font = new System.Drawing.Font("Yu Gothic UI", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.toolStripStatusLabelExposure.Name = "toolStripStatusLabelExposure";
            this.toolStripStatusLabelExposure.Size = new System.Drawing.Size(129, 38);
            this.toolStripStatusLabelExposure.Text = "Exposure";
            // 
            // toolStripStatusLabelID
            // 
            this.toolStripStatusLabelID.Font = new System.Drawing.Font("Yu Gothic UI", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.toolStripStatusLabelID.Name = "toolStripStatusLabelID";
            this.toolStripStatusLabelID.Size = new System.Drawing.Size(120, 38);
            this.toolStripStatusLabelID.Text = "FrameID";
            // 
            // toolStripStatusLabelFailed
            // 
            this.toolStripStatusLabelFailed.Font = new System.Drawing.Font("Yu Gothic UI", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.toolStripStatusLabelFailed.Name = "toolStripStatusLabelFailed";
            this.toolStripStatusLabelFailed.Size = new System.Drawing.Size(85, 38);
            this.toolStripStatusLabelFailed.Text = "failed";
            // 
            // toolStripStatusLabelGain
            // 
            this.toolStripStatusLabelGain.Font = new System.Drawing.Font("Yu Gothic UI", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.toolStripStatusLabelGain.Name = "toolStripStatusLabelGain";
            this.toolStripStatusLabelGain.Size = new System.Drawing.Size(81, 38);
            this.toolStripStatusLabelGain.Text = "Gaim";
            // 
            // toolStripStatusLabelTemp
            // 
            this.toolStripStatusLabelTemp.BorderStyle = System.Windows.Forms.Border3DStyle.Sunken;
            this.toolStripStatusLabelTemp.Font = new System.Drawing.Font("Yu Gothic UI", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.toolStripStatusLabelTemp.Name = "toolStripStatusLabelTemp";
            this.toolStripStatusLabelTemp.Size = new System.Drawing.Size(168, 38);
            this.toolStripStatusLabelTemp.Text = "SensorTemp";
            // 
            // buttonMove
            // 
            this.buttonMove.Font = new System.Drawing.Font("MS UI Gothic", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.buttonMove.Location = new System.Drawing.Point(12, 541);
            this.buttonMove.Name = "buttonMove";
            this.buttonMove.Size = new System.Drawing.Size(103, 48);
            this.buttonMove.TabIndex = 14;
            this.buttonMove.Text = "Move";
            this.buttonMove.UseVisualStyleBackColor = true;
            this.buttonMove.Click += new System.EventHandler(this.buttonMove_Click);
            // 
            // numericUpDown_daz
            // 
            this.numericUpDown_daz.Location = new System.Drawing.Point(124, 590);
            this.numericUpDown_daz.Name = "numericUpDown_daz";
            this.numericUpDown_daz.Size = new System.Drawing.Size(120, 19);
            this.numericUpDown_daz.TabIndex = 15;
            // 
            // numericUpDown_dalt
            // 
            this.numericUpDown_dalt.Location = new System.Drawing.Point(124, 632);
            this.numericUpDown_dalt.Name = "numericUpDown_dalt";
            this.numericUpDown_dalt.Size = new System.Drawing.Size(120, 19);
            this.numericUpDown_dalt.TabIndex = 16;
            // 
            // checkBox_DispMode
            // 
            this.checkBox_DispMode.AutoSize = true;
            this.checkBox_DispMode.Checked = true;
            this.checkBox_DispMode.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_DispMode.Location = new System.Drawing.Point(12, 719);
            this.checkBox_DispMode.Name = "checkBox_DispMode";
            this.checkBox_DispMode.Size = new System.Drawing.Size(81, 21);
            this.checkBox_DispMode.TabIndex = 17;
            this.checkBox_DispMode.Text = "DispMode";
            this.checkBox_DispMode.UseVisualStyleBackColor = true;
            this.checkBox_DispMode.CheckedChanged += new System.EventHandler(this.checkBox_WideDR_CheckedChanged);
            // 
            // numericUpDownStarCount
            // 
            this.numericUpDownStarCount.Location = new System.Drawing.Point(124, 674);
            this.numericUpDownStarCount.Name = "numericUpDownStarCount";
            this.numericUpDownStarCount.Size = new System.Drawing.Size(120, 19);
            this.numericUpDownStarCount.TabIndex = 18;
            // 
            // button_mask
            // 
            this.button_mask.Font = new System.Drawing.Font("MS UI Gothic", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.button_mask.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.button_mask.Location = new System.Drawing.Point(12, 787);
            this.button_mask.Name = "button_mask";
            this.button_mask.Size = new System.Drawing.Size(103, 37);
            this.button_mask.TabIndex = 19;
            this.button_mask.Text = "Mask";
            this.button_mask.UseVisualStyleBackColor = true;
            this.button_mask.Click += new System.EventHandler(this.buttonMakeDark_Click);
            // 
            // button_test
            // 
            this.button_test.Font = new System.Drawing.Font("MS UI Gothic", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.button_test.Location = new System.Drawing.Point(13, 851);
            this.button_test.Name = "button_test";
            this.button_test.Size = new System.Drawing.Size(102, 56);
            this.button_test.TabIndex = 20;
            this.button_test.Text = "test";
            this.button_test.UseVisualStyleBackColor = true;
            this.button_test.Click += new System.EventHandler(this.button_test_Click);
            // 
            // timerObsOnOff
            // 
            this.timerObsOnOff.Enabled = true;
            this.timerObsOnOff.Interval = 5000;
            this.timerObsOnOff.Tick += new System.EventHandler(this.timerObsOnOff_Tick);
            // 
            // Form1
            // 
            this.ClientSize = new System.Drawing.Size(1565, 1119);
            this.Controls.Add(this.button_test);
            this.Controls.Add(this.button_mask);
            this.Controls.Add(this.numericUpDownStarCount);
            this.Controls.Add(this.checkBox_DispMode);
            this.Controls.Add(this.numericUpDown_dalt);
            this.Controls.Add(this.numericUpDown_daz);
            this.Controls.Add(this.buttonMove);
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
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.ObsStart);
            this.Name = "Form1";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Shown += new System.EventHandler(this.Form1_Shown);
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownStarMin)).EndInit();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_daz)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_dalt)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownStarCount)).EndInit();
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
        private System.Windows.Forms.Button buttonMove;
        private System.Windows.Forms.NumericUpDown numericUpDown_daz;
        private System.Windows.Forms.NumericUpDown numericUpDown_dalt;
        private System.Windows.Forms.CheckBox checkBox_DispMode;
        private System.Windows.Forms.NumericUpDown numericUpDownStarCount;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelTemp;
        private System.Windows.Forms.Button button_mask;
        private System.Windows.Forms.Button button_test;
        private System.Windows.Forms.Timer timerObsOnOff;
    }
}

