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
            menuStrip1 = new MenuStrip();
            файлToolStripMenuItem = new ToolStripMenuItem();
            выходToolStripMenuItem = new ToolStripMenuItem();
            bottomPanel = new TableLayoutPanel();
            startButton = new Button();
            stopButton = new Button();
            textBox1 = new TextBox();
            flowLayoutPanel1 = new FlowLayoutPanel();
            statusLabel = new Label();
            responseLabel = new Label();
            menuStrip1.SuspendLayout();
            bottomPanel.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { файлToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(384, 24);
            menuStrip1.TabIndex = 1;
            menuStrip1.Text = "menuStrip1";
            // 
            // файлToolStripMenuItem
            // 
            файлToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { выходToolStripMenuItem });
            файлToolStripMenuItem.Name = "файлToolStripMenuItem";
            файлToolStripMenuItem.Size = new Size(48, 20);
            файлToolStripMenuItem.Text = "Файл";
            // 
            // выходToolStripMenuItem
            // 
            выходToolStripMenuItem.Name = "выходToolStripMenuItem";
            выходToolStripMenuItem.Size = new Size(109, 22);
            выходToolStripMenuItem.Text = "Выход";
            // 
            // bottomPanel
            // 
            bottomPanel.ColumnCount = 2;
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottomPanel.Controls.Add(startButton, 0, 0);
            bottomPanel.Controls.Add(stopButton, 1, 0);
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Location = new Point(0, 394);
            bottomPanel.Margin = new Padding(5);
            bottomPanel.Name = "bottomPanel";
            bottomPanel.RowCount = 1;
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            bottomPanel.Size = new Size(384, 71);
            bottomPanel.TabIndex = 3;
            // 
            // startButton
            // 
            startButton.Dock = DockStyle.Fill;
            startButton.Location = new Point(10, 10);
            startButton.Margin = new Padding(10);
            startButton.Name = "startButton";
            startButton.Size = new Size(172, 51);
            startButton.TabIndex = 0;
            startButton.Text = "Запуск";
            startButton.UseVisualStyleBackColor = true;
            startButton.Click += startButton_Click;
            // 
            // stopButton
            // 
            stopButton.Dock = DockStyle.Fill;
            stopButton.Location = new Point(202, 10);
            stopButton.Margin = new Padding(10);
            stopButton.Name = "stopButton";
            stopButton.Size = new Size(172, 51);
            stopButton.TabIndex = 1;
            stopButton.Text = "Остановка";
            stopButton.UseVisualStyleBackColor = true;
            stopButton.Click += stopButton_Click;
            // 
            // textBox1
            // 
            textBox1.Dock = DockStyle.Bottom;
            textBox1.Location = new Point(0, 359);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.ScrollBars = ScrollBars.Vertical;
            textBox1.Size = new Size(384, 35);
            textBox1.TabIndex = 5;
            textBox1.Visible = false;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Controls.Add(statusLabel);
            flowLayoutPanel1.Controls.Add(responseLabel);
            flowLayoutPanel1.Dock = DockStyle.Fill;
            flowLayoutPanel1.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel1.Location = new Point(0, 24);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(384, 335);
            flowLayoutPanel1.TabIndex = 6;
            // 
            // statusLabel
            // 
            statusLabel.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 204);
            statusLabel.Location = new Point(0, 0);
            statusLabel.Margin = new Padding(0);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(383, 19);
            statusLabel.TabIndex = 3;
            statusLabel.TextAlign = ContentAlignment.TopCenter;
            // 
            // responseLabel
            // 
            responseLabel.Dock = DockStyle.Top;
            responseLabel.Font = new Font("Segoe UI", 18F, FontStyle.Italic, GraphicsUnit.Point, 204);
            responseLabel.ForeColor = SystemColors.WindowFrame;
            responseLabel.Location = new Point(3, 19);
            responseLabel.Name = "responseLabel";
            responseLabel.Size = new Size(377, 66);
            responseLabel.TabIndex = 4;
            responseLabel.TextAlign = ContentAlignment.TopCenter;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(384, 465);
            Controls.Add(flowLayoutPanel1);
            Controls.Add(textBox1);
            Controls.Add(bottomPanel);
            Controls.Add(menuStrip1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MainMenuStrip = menuStrip1;
            MaximizeBox = false;
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Голосовой помощник - БАП ГМЗ-2";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            bottomPanel.ResumeLayout(false);
            flowLayoutPanel1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private MenuStrip menuStrip1;
        private TableLayoutPanel bottomPanel;
        private Button startButton;
        private Button stopButton;
        private TextBox textBox1;
        private FlowLayoutPanel flowLayoutPanel1;
        private Label responseLabel;
        private Label statusLabel;
        private ToolStripMenuItem файлToolStripMenuItem;
        private ToolStripMenuItem выходToolStripMenuItem;
    }
}
