namespace kursachm
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private Button btnSelectTrainFolder;
        private Button btnTrain;
        private TextBox txtTrainPath;
        private Label lblTrainPath;
        private Button btnSelectTestFile;
        private TextBox txtTestFile;
        private Button btnClassify;
        private Label lblTestFile;
        private TextBox txtLog;

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
            btnSelectTrainFolder = new Button();
            btnTrain = new Button();
            txtTrainPath = new TextBox();
            lblTrainPath = new Label();
            btnSelectTestFile = new Button();
            txtTestFile = new TextBox();
            btnClassify = new Button();
            lblTestFile = new Label();
            txtLog = new TextBox();
            SuspendLayout();
            // 
            // btnSelectTrainFolder
            // 
            btnSelectTrainFolder.Location = new Point(600, 10);
            btnSelectTrainFolder.Name = "btnSelectTrainFolder";
            btnSelectTrainFolder.Size = new Size(75, 25);
            btnSelectTrainFolder.TabIndex = 2;
            btnSelectTrainFolder.Text = "Обзор...";
            btnSelectTrainFolder.UseVisualStyleBackColor = true;
            btnSelectTrainFolder.Click += BtnSelectTrainFolder_Click;
            // 
            // btnTrain
            // 
            btnTrain.Location = new Point(690, 10);
            btnTrain.Name = "btnTrain";
            btnTrain.Size = new Size(75, 25);
            btnTrain.TabIndex = 3;
            btnTrain.Text = "Обучить";
            btnTrain.UseVisualStyleBackColor = true;
            btnTrain.Click += BtnTrain_Click;
            // 
            // txtTrainPath
            // 
            txtTrainPath.Location = new Point(290, 12);
            txtTrainPath.Name = "txtTrainPath";
            txtTrainPath.Size = new Size(300, 23);
            txtTrainPath.TabIndex = 1;
            // 
            // lblTrainPath
            // 
            lblTrainPath.Location = new Point(12, 15);
            lblTrainPath.Name = "lblTrainPath";
            lblTrainPath.Size = new Size(270, 20);
            lblTrainPath.TabIndex = 0;
            lblTrainPath.Text = "Папка с данными (WiLI или подпапки языков):";
            // 
            // btnSelectTestFile
            // 
            btnSelectTestFile.Location = new Point(600, 45);
            btnSelectTestFile.Name = "btnSelectTestFile";
            btnSelectTestFile.Size = new Size(75, 25);
            btnSelectTestFile.TabIndex = 6;
            btnSelectTestFile.Text = "Обзор...";
            btnSelectTestFile.UseVisualStyleBackColor = true;
            btnSelectTestFile.Click += BtnSelectTestFile_Click;
            // 
            // txtTestFile
            // 
            txtTestFile.Location = new Point(120, 47);
            txtTestFile.Name = "txtTestFile";
            txtTestFile.Size = new Size(470, 23);
            txtTestFile.TabIndex = 5;
            // 
            // btnClassify
            // 
            btnClassify.Location = new Point(690, 45);
            btnClassify.Name = "btnClassify";
            btnClassify.Size = new Size(122, 25);
            btnClassify.TabIndex = 7;
            btnClassify.Text = "Классифицировать";
            btnClassify.UseVisualStyleBackColor = true;
            btnClassify.Click += BtnClassify_Click;
            // 
            // lblTestFile
            // 
            lblTestFile.Location = new Point(12, 50);
            lblTestFile.Name = "lblTestFile";
            lblTestFile.Size = new Size(100, 20);
            lblTestFile.TabIndex = 4;
            lblTestFile.Text = "Тестовый файл:";
            // 
            // txtLog
            // 
            txtLog.Location = new Point(12, 76);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(798, 400);
            txtLog.TabIndex = 8;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(824, 500);
            Controls.Add(lblTrainPath);
            Controls.Add(txtTrainPath);
            Controls.Add(btnSelectTrainFolder);
            Controls.Add(btnTrain);
            Controls.Add(lblTestFile);
            Controls.Add(txtTestFile);
            Controls.Add(btnSelectTestFile);
            Controls.Add(btnClassify);
            Controls.Add(txtLog);
            Name = "Form1";
            Text = "Определение языка текста";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}