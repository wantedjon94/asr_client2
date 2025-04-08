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
            pttButton = new Button();
            textBox1 = new TextBox();
            bottomPanel.SuspendLayout();
            SuspendLayout();
            // 
            // statusLabel
            // 
            statusLabel.Dock = DockStyle.Top;
            statusLabel.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 204);
            statusLabel.Location = new Point(0, 24);
            statusLabel.Margin = new Padding(0);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(340, 19);
            statusLabel.TabIndex = 0;
            statusLabel.Text = "Ответ";
            statusLabel.TextAlign = ContentAlignment.TopCenter;
            // 
            // menuStrip1
            // 
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(340, 24);
            menuStrip1.TabIndex = 1;
            menuStrip1.Text = "menuStrip1";
            // 
            // responseLabel
            // 
            responseLabel.Dock = DockStyle.Top;
            responseLabel.Font = new Font("Segoe UI", 18F, FontStyle.Regular, GraphicsUnit.Point, 204);
            responseLabel.Location = new Point(0, 43);
            responseLabel.Name = "responseLabel";
            responseLabel.Size = new Size(340, 66);
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
            bottomPanel.Location = new Point(0, 470);
            bottomPanel.Margin = new Padding(5);
            bottomPanel.Name = "bottomPanel";
            bottomPanel.RowCount = 1;
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            bottomPanel.Size = new Size(340, 71);
            bottomPanel.TabIndex = 3;
            // 
            // startButton
            // 
            startButton.Dock = DockStyle.Fill;
            startButton.Location = new Point(10, 10);
            startButton.Margin = new Padding(10);
            startButton.Name = "startButton";
            startButton.Size = new Size(150, 51);
            startButton.TabIndex = 0;
            startButton.Text = "START";
            startButton.UseVisualStyleBackColor = true;
            startButton.Click += startButton_Click;
            // 
            // stopButton
            // 
            stopButton.Dock = DockStyle.Fill;
            stopButton.Location = new Point(180, 10);
            stopButton.Margin = new Padding(10);
            stopButton.Name = "stopButton";
            stopButton.Size = new Size(150, 51);
            stopButton.TabIndex = 1;
            stopButton.Text = "STOP";
            stopButton.UseVisualStyleBackColor = true;
            // 
            // pttButton
            // 
            pttButton.Location = new Point(140, 432);
            pttButton.Name = "pttButton";
            pttButton.Size = new Size(60, 35);
            pttButton.TabIndex = 4;
            pttButton.Text = "Mic";
            pttButton.UseVisualStyleBackColor = true;
            // 
            // textBox1
            // 
            textBox1.Dock = DockStyle.Top;
            textBox1.Location = new Point(0, 109);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(340, 317);
            textBox1.TabIndex = 5;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(340, 541);
            Controls.Add(textBox1);
            Controls.Add(pttButton);
            Controls.Add(bottomPanel);
            Controls.Add(responseLabel);
            Controls.Add(statusLabel);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "MainForm";
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
        private Button pttButton;
        private TextBox textBox1;
    }
}
