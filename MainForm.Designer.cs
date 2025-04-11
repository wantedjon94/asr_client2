namespace ASR_Client2
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            statusLabel = new Label();
            menuStrip1 = new MenuStrip();
            responseLabel = new Label();
            bottomPanel = new TableLayoutPanel();
            startButton = new Button();
            stopButton = new Button();
            textBox1 = new TextBox();
            volumeMeter1 = new NAudio.Gui.VolumeMeter();
            bottomPanel.SuspendLayout();
            SuspendLayout();
            // 
            // statusLabel
            // 
            statusLabel.Dock = DockStyle.Top;
            statusLabel.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 204);
            statusLabel.Location = new Point(0, 24);
            statusLabel.Margin = new Padding(0);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(383, 19);
            statusLabel.TabIndex = 0;
            statusLabel.TextAlign = ContentAlignment.TopCenter;
            // 
            // menuStrip1
            // 
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(383, 24);
            menuStrip1.TabIndex = 1;
            menuStrip1.Text = "menuStrip1";
            // 
            // responseLabel
            // 
            responseLabel.Dock = DockStyle.Top;
            responseLabel.Font = new Font("Segoe UI", 18F, FontStyle.Italic, GraphicsUnit.Point, 204);
            responseLabel.ForeColor = SystemColors.WindowFrame;
            responseLabel.Location = new Point(0, 43);
            responseLabel.Name = "responseLabel";
            responseLabel.Size = new Size(383, 66);
            responseLabel.TabIndex = 2;
            responseLabel.TextAlign = ContentAlignment.TopCenter;
            // 
            // bottomPanel
            // 
            bottomPanel.ColumnCount = 2;
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottomPanel.Controls.Add(startButton, 0, 0);
            bottomPanel.Controls.Add(stopButton, 1, 0);
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Location = new Point(0, 310);
            bottomPanel.Margin = new Padding(5);
            bottomPanel.Name = "bottomPanel";
            bottomPanel.RowCount = 1;
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            bottomPanel.Size = new Size(383, 71);
            bottomPanel.TabIndex = 3;
            // 
            // startButton
            // 
            startButton.Dock = DockStyle.Fill;
            startButton.Location = new Point(10, 10);
            startButton.Margin = new Padding(10);
            startButton.Name = "startButton";
            startButton.Size = new Size(171, 51);
            startButton.TabIndex = 0;
            startButton.Text = "START";
            startButton.UseVisualStyleBackColor = true;
            startButton.Click += startButton_Click;
            // 
            // stopButton
            // 
            stopButton.Dock = DockStyle.Fill;
            stopButton.Location = new Point(201, 10);
            stopButton.Margin = new Padding(10);
            stopButton.Name = "stopButton";
            stopButton.Size = new Size(172, 51);
            stopButton.TabIndex = 1;
            stopButton.Text = "STOP";
            stopButton.UseVisualStyleBackColor = true;
            stopButton.Click += stopButton_Click;
            // 
            // textBox1
            // 
            textBox1.Dock = DockStyle.Top;
            textBox1.Location = new Point(0, 109);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.ScrollBars = ScrollBars.Vertical;
            textBox1.Size = new Size(383, 59);
            textBox1.TabIndex = 5;
            // 
            // volumeMeter1
            // 
            volumeMeter1.Amplitude = 0F;
            volumeMeter1.Location = new Point(56, 174);
            volumeMeter1.MaxDb = 60F;
            volumeMeter1.MinDb = 0F;
            volumeMeter1.Name = "volumeMeter1";
            volumeMeter1.Size = new Size(271, 128);
            volumeMeter1.TabIndex = 6;
            volumeMeter1.Text = "volumeMeter1";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(383, 381);
            Controls.Add(volumeMeter1);
            Controls.Add(textBox1);
            Controls.Add(bottomPanel);
            Controls.Add(responseLabel);
            Controls.Add(statusLabel);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "MainForm";
            bottomPanel.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label statusLabel;
        private MenuStrip menuStrip1;
        private Label responseLabel;
        private TableLayoutPanel bottomPanel;
        private Button startButton;
        private Button stopButton;
        private TextBox textBox1;
        private NAudio.Gui.VolumeMeter volumeMeter1;
    }
}
