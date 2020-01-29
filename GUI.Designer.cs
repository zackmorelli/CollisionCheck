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
            this.SuspendLayout();
            // 
            // Planlist
            // 
            this.Planlist.Font = new System.Drawing.Font("Goudy Old Style", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Planlist.FormattingEnabled = true;
            this.Planlist.ItemHeight = 23;
            this.Planlist.Location = new System.Drawing.Point(12, 188);
            this.Planlist.Name = "Planlist";
            this.Planlist.Size = new System.Drawing.Size(315, 234);
            this.Planlist.TabIndex = 0;
            this.Planlist.SelectedIndexChanged += new System.EventHandler(this.PlanList_SelectedIndexChanged);
            // 
            // CollOutput
            // 
            this.CollOutput.Font = new System.Drawing.Font("Goudy Old Style", 13F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CollOutput.Location = new System.Drawing.Point(344, 12);
            this.CollOutput.Multiline = true;
            this.CollOutput.Name = "CollOutput";
            this.CollOutput.ReadOnly = true;
            this.CollOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.CollOutput.Size = new System.Drawing.Size(432, 410);
            this.CollOutput.TabIndex = 1;
            // 
            // Executebutton
            // 
            this.Executebutton.Font = new System.Drawing.Font("Goudy Old Style", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Executebutton.Location = new System.Drawing.Point(798, 317);
            this.Executebutton.Name = "Executebutton";
            this.Executebutton.Size = new System.Drawing.Size(148, 85);
            this.Executebutton.TabIndex = 2;
            this.Executebutton.Text = "Execute Collision Check";
            this.Executebutton.UseVisualStyleBackColor = true;
            this.Executebutton.Click += new System.EventHandler(this.Executebutton_Click);
            // 
            // DirectionsBox
            // 
            this.DirectionsBox.Font = new System.Drawing.Font("Goudy Old Style", 13F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DirectionsBox.Location = new System.Drawing.Point(12, 12);
            this.DirectionsBox.Multiline = true;
            this.DirectionsBox.Name = "DirectionsBox";
            this.DirectionsBox.ReadOnly = true;
            this.DirectionsBox.Size = new System.Drawing.Size(315, 159);
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
            "Pelvis"});
            this.imagelocationlist.Location = new System.Drawing.Point(798, 12);
            this.imagelocationlist.Name = "imagelocationlist";
            this.imagelocationlist.Size = new System.Drawing.Size(166, 88);
            this.imagelocationlist.TabIndex = 4;
            this.imagelocationlist.SelectedIndexChanged += new System.EventHandler(this.imagelocationlist_SelectedIndexChanged);
            // 
            // heightlabel
            // 
            this.heightlabel.AutoSize = true;
            this.heightlabel.Font = new System.Drawing.Font("Goudy Old Style", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.heightlabel.Location = new System.Drawing.Point(794, 148);
            this.heightlabel.Name = "heightlabel";
            this.heightlabel.Size = new System.Drawing.Size(120, 23);
            this.heightlabel.TabIndex = 5;
            this.heightlabel.Text = "Patient Height";
            // 
            // Heightbox
            // 
            this.Heightbox.AcceptsReturn = true;
            this.Heightbox.Font = new System.Drawing.Font("Goudy Old Style", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Heightbox.Location = new System.Drawing.Point(798, 174);
            this.Heightbox.Name = "Heightbox";
            this.Heightbox.Size = new System.Drawing.Size(103, 30);
            this.Heightbox.TabIndex = 6;
            this.Heightbox.TextChanged += new System.EventHandler(this.Heightbox_TextChanged);
            // 
            // Hunit
            // 
            this.Hunit.AutoSize = true;
            this.Hunit.Font = new System.Drawing.Font("Goudy Old Style", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Hunit.Location = new System.Drawing.Point(907, 177);
            this.Hunit.Name = "Hunit";
            this.Hunit.Size = new System.Drawing.Size(33, 23);
            this.Hunit.TabIndex = 7;
            this.Hunit.Text = "cm";
            // 
            // ProgOutput
            // 
            this.ProgOutput.Font = new System.Drawing.Font("Goudy Old Style", 13F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ProgOutput.ReadOnly = true;
            this.ProgOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.ProgOutput.Location = new System.Drawing.Point(984, 12);
            this.ProgOutput.Multiline = true;
            this.ProgOutput.Name = "ProgOutput";
            this.ProgOutput.Size = new System.Drawing.Size(352, 414);
            this.ProgOutput.TabIndex = 8;
            // 
            // GUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1348, 450);
            this.Controls.Add(this.ProgOutput);
            this.Controls.Add(this.Hunit);
            this.Controls.Add(this.Heightbox);
            this.Controls.Add(this.heightlabel);
            this.Controls.Add(this.imagelocationlist);
            this.Controls.Add(this.DirectionsBox);
            this.Controls.Add(this.Executebutton);
            this.Controls.Add(this.CollOutput);
            this.Controls.Add(this.Planlist);
            this.Name = "GUI";
            this.Text = "Lahey Radiation Oncology Collision Check";
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
    }
}