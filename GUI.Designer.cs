namespace CollisionCheck
{
    partial class GUI
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GUI));
            this.Planlist = new System.Windows.Forms.ListBox();
            this.CollOutput = new System.Windows.Forms.TextBox();
            this.Executebutton = new System.Windows.Forms.Button();
            this.DirectionsBox = new System.Windows.Forms.TextBox();
            this.imagelocationlist = new System.Windows.Forms.ListBox();
            this.heightlabel = new System.Windows.Forms.Label();
            this.Heightbox = new System.Windows.Forms.TextBox();
            this.Hunit = new System.Windows.Forms.Label();
            this.ProgOutput = new System.Windows.Forms.TextBox();
            this.ProgBar = new System.Windows.Forms.ProgressBar();
            this.SRSCheckBox = new System.Windows.Forms.CheckBox();
            this.FastBox = new System.Windows.Forms.CheckBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.E10 = new System.Windows.Forms.CheckBox();
            this.E15 = new System.Windows.Forms.CheckBox();
            this.E6 = new System.Windows.Forms.CheckBox();
            this.E20 = new System.Windows.Forms.CheckBox();
            this.E25 = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // Planlist
            // 
            this.Planlist.Font = new System.Drawing.Font("Goudy Old Style", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Planlist.FormattingEnabled = true;
            this.Planlist.ItemHeight = 23;
            this.Planlist.Location = new System.Drawing.Point(5, 223);
            this.Planlist.Name = "Planlist";
            this.Planlist.Size = new System.Drawing.Size(315, 142);
            this.Planlist.TabIndex = 0;
            this.Planlist.SelectedIndexChanged += new System.EventHandler(this.PlanList_SelectedIndexChanged);
            // 
            // CollOutput
            // 
            this.CollOutput.Font = new System.Drawing.Font("Goudy Old Style", 13F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CollOutput.Location = new System.Drawing.Point(782, 223);
            this.CollOutput.Multiline = true;
            this.CollOutput.Name = "CollOutput";
            this.CollOutput.ReadOnly = true;
            this.CollOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.CollOutput.Size = new System.Drawing.Size(523, 499);
            this.CollOutput.TabIndex = 1;
            // 
            // Executebutton
            // 
            this.Executebutton.Font = new System.Drawing.Font("Goudy Old Style", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Executebutton.Location = new System.Drawing.Point(12, 676);
            this.Executebutton.Name = "Executebutton";
            this.Executebutton.Size = new System.Drawing.Size(148, 85);
            this.Executebutton.TabIndex = 2;
            this.Executebutton.Text = "Execute Collision Check";
            this.Executebutton.UseVisualStyleBackColor = true;
            this.Executebutton.Click += new System.EventHandler(this.Executebutton_Click);
            // 
            // DirectionsBox
            // 
            this.DirectionsBox.Font = new System.Drawing.Font("Goudy Old Style", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DirectionsBox.Location = new System.Drawing.Point(13, 8);
            this.DirectionsBox.Multiline = true;
            this.DirectionsBox.Name = "DirectionsBox";
            this.DirectionsBox.ReadOnly = true;
            this.DirectionsBox.Size = new System.Drawing.Size(1292, 196);
            this.DirectionsBox.TabIndex = 3;
            this.DirectionsBox.Text = resources.GetString("DirectionsBox.Text");
            // 
            // imagelocationlist
            // 
            this.imagelocationlist.Font = new System.Drawing.Font("Goudy Old Style", 13F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.imagelocationlist.FormattingEnabled = true;
            this.imagelocationlist.ItemHeight = 21;
            this.imagelocationlist.Items.AddRange(new object[] {
            "Head",
            "Thorax",
            "Abdomen",
            "Pelvis",
            "Legs"});
            this.imagelocationlist.Location = new System.Drawing.Point(12, 467);
            this.imagelocationlist.Name = "imagelocationlist";
            this.imagelocationlist.Size = new System.Drawing.Size(113, 109);
            this.imagelocationlist.TabIndex = 4;
            this.imagelocationlist.SelectedIndexChanged += new System.EventHandler(this.imagelocationlist_SelectedIndexChanged);
            // 
            // heightlabel
            // 
            this.heightlabel.AutoSize = true;
            this.heightlabel.Font = new System.Drawing.Font("Goudy Old Style", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.heightlabel.Location = new System.Drawing.Point(8, 593);
            this.heightlabel.Name = "heightlabel";
            this.heightlabel.Size = new System.Drawing.Size(120, 23);
            this.heightlabel.TabIndex = 5;
            this.heightlabel.Text = "Patient Height";
            // 
            // Heightbox
            // 
            this.Heightbox.AcceptsReturn = true;
            this.Heightbox.Font = new System.Drawing.Font("Goudy Old Style", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Heightbox.Location = new System.Drawing.Point(16, 619);
            this.Heightbox.Name = "Heightbox";
            this.Heightbox.Size = new System.Drawing.Size(90, 30);
            this.Heightbox.TabIndex = 6;
            this.Heightbox.TextChanged += new System.EventHandler(this.Heightbox_TextChanged);
            // 
            // Hunit
            // 
            this.Hunit.AutoSize = true;
            this.Hunit.Font = new System.Drawing.Font("Goudy Old Style", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Hunit.Location = new System.Drawing.Point(112, 622);
            this.Hunit.Name = "Hunit";
            this.Hunit.Size = new System.Drawing.Size(33, 23);
            this.Hunit.TabIndex = 7;
            this.Hunit.Text = "cm";
            // 
            // ProgOutput
            // 
            this.ProgOutput.Font = new System.Drawing.Font("Goudy Old Style", 13F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ProgOutput.Location = new System.Drawing.Point(371, 223);
            this.ProgOutput.Multiline = true;
            this.ProgOutput.Name = "ProgOutput";
            this.ProgOutput.ReadOnly = true;
            this.ProgOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.ProgOutput.Size = new System.Drawing.Size(393, 499);
            this.ProgOutput.TabIndex = 8;
            // 
            // ProgBar
            // 
            this.ProgBar.ForeColor = System.Drawing.Color.Lime;
            this.ProgBar.Location = new System.Drawing.Point(371, 737);
            this.ProgBar.Name = "ProgBar";
            this.ProgBar.Size = new System.Drawing.Size(934, 24);
            this.ProgBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.ProgBar.TabIndex = 9;
            this.ProgBar.Visible = false;
            // 
            // SRSCheckBox
            // 
            this.SRSCheckBox.AutoSize = true;
            this.SRSCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SRSCheckBox.Location = new System.Drawing.Point(201, 577);
            this.SRSCheckBox.Name = "SRSCheckBox";
            this.SRSCheckBox.Size = new System.Drawing.Size(113, 24);
            this.SRSCheckBox.TabIndex = 10;
            this.SRSCheckBox.Text = "SRS Cone?";
            this.SRSCheckBox.UseVisualStyleBackColor = true;
            this.SRSCheckBox.CheckedChanged += new System.EventHandler(this.SRSCheckBox_CheckChange);
            // 
            // FastBox
            // 
            this.FastBox.AutoSize = true;
            this.FastBox.Checked = true;
            this.FastBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.FastBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FastBox.Location = new System.Drawing.Point(190, 396);
            this.FastBox.Name = "FastBox";
            this.FastBox.Size = new System.Drawing.Size(104, 24);
            this.FastBox.TabIndex = 12;
            this.FastBox.Text = "Fast Mode";
            this.FastBox.UseVisualStyleBackColor = true;
            this.FastBox.CheckedChanged += new System.EventHandler(this.FastBox_CheckChange);
            // 
            // textBox1
            // 
            this.textBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox1.Location = new System.Drawing.Point(190, 426);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(148, 125);
            this.textBox1.TabIndex = 13;
            this.textBox1.Text = "Set as the default. Slightly less accurate and doesn\'t calculate distances, but M" +
    "UCH faster. Meant as a preliminary check.";
            // 
            // E10
            // 
            this.E10.AutoSize = true;
            this.E10.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.E10.Location = new System.Drawing.Point(201, 637);
            this.E10.Name = "E10";
            this.E10.Size = new System.Drawing.Size(137, 24);
            this.E10.TabIndex = 14;
            this.E10.Text = "10x10 E Cone?";
            this.E10.UseVisualStyleBackColor = true;
            this.E10.CheckedChanged += new System.EventHandler(this.E10Box_CheckChange);
            // 
            // E15
            // 
            this.E15.AutoSize = true;
            this.E15.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.E15.Location = new System.Drawing.Point(201, 667);
            this.E15.Name = "E15";
            this.E15.Size = new System.Drawing.Size(137, 24);
            this.E15.TabIndex = 15;
            this.E15.Text = "15x15 E Cone?";
            this.E15.UseVisualStyleBackColor = true;
            this.E15.CheckedChanged += new System.EventHandler(this.E15Box_CheckChange);
            // 
            // E6
            // 
            this.E6.AutoSize = true;
            this.E6.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.E6.Location = new System.Drawing.Point(201, 607);
            this.E6.Name = "E6";
            this.E6.Size = new System.Drawing.Size(119, 24);
            this.E6.TabIndex = 16;
            this.E6.Text = "6x6 E Cone?";
            this.E6.UseVisualStyleBackColor = true;
            this.E6.CheckedChanged += new System.EventHandler(this.E6Box_CheckChange);
            // 
            // E20
            // 
            this.E20.AutoSize = true;
            this.E20.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.E20.Location = new System.Drawing.Point(201, 697);
            this.E20.Name = "E20";
            this.E20.Size = new System.Drawing.Size(137, 24);
            this.E20.TabIndex = 17;
            this.E20.Text = "20x20 E Cone?";
            this.E20.UseVisualStyleBackColor = true;
            this.E20.CheckedChanged += new System.EventHandler(this.E20Box_CheckChange);
            // 
            // E25
            // 
            this.E25.AutoSize = true;
            this.E25.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.E25.Location = new System.Drawing.Point(201, 727);
            this.E25.Name = "E25";
            this.E25.Size = new System.Drawing.Size(137, 24);
            this.E25.TabIndex = 18;
            this.E25.Text = "25x25 E Cone?";
            this.E25.UseVisualStyleBackColor = true;
            this.E25.CheckedChanged += new System.EventHandler(this.E25Box_CheckChange);
            // 
            // GUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1317, 763);
            this.Controls.Add(this.E25);
            this.Controls.Add(this.E6);
            this.Controls.Add(this.E20);
            this.Controls.Add(this.E15);
            this.Controls.Add(this.E10);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.SRSCheckBox);
            this.Controls.Add(this.FastBox);
            this.Controls.Add(this.ProgBar);
            this.Controls.Add(this.ProgOutput);
            this.Controls.Add(this.Hunit);
            this.Controls.Add(this.Heightbox);
            this.Controls.Add(this.heightlabel);
            this.Controls.Add(this.imagelocationlist);
            this.Controls.Add(this.DirectionsBox);
            this.Controls.Add(this.Executebutton);
            this.Controls.Add(this.CollOutput);
            this.Controls.Add(this.Planlist);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "GUI";
            this.Text = "Collision Check";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox Planlist;
        private System.Windows.Forms.TextBox CollOutput;
        private System.Windows.Forms.Button Executebutton;
        private System.Windows.Forms.TextBox DirectionsBox;
        private System.Windows.Forms.Label heightlabel;
        private System.Windows.Forms.TextBox Heightbox;
        private System.Windows.Forms.Label Hunit;
        private System.Windows.Forms.ListBox imagelocationlist;
        private System.Windows.Forms.TextBox ProgOutput;
        private System.Windows.Forms.ProgressBar ProgBar;
        private System.Windows.Forms.CheckBox SRSCheckBox;
        private System.Windows.Forms.CheckBox FastBox;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.CheckBox E10;
        private System.Windows.Forms.CheckBox E15;
        private System.Windows.Forms.CheckBox E6;
        private System.Windows.Forms.CheckBox E20;
        private System.Windows.Forms.CheckBox E25;
    }
}