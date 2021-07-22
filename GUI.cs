using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;



/*
    Collision Check - GUI
    Copyright (c) 2020 Radiation Oncology Department, Lahey Hospital and Medical Center
    Written by Zack Morelli

    This program is expressely written as a plug-in script for use with Varian's Eclipse Treatment Planning System, and requires Varian's API files to run properly.
    This program also requires .NET Framework 4.5.0 to run properly.

    //  all linear dimensions are expressed in millimeters and all angles are expressed in degrees. 
    // This includes the API's internal vector objects and the positions it reports of various objects

    Description: This is the GUI for CollisionCheck.
*/

namespace CollisionCheck
{
    public partial class GUI : Form
    {

        List<string> plannames = new List<string>();
        public string pl = null;
        public string bodyloc = null;
        public string height = null;
        public double Xshiftvalue = 0.0;
        public double Yshiftvalue = 0.0;

        public GUI(List<PLAN> Plans, IMAGE image)
        {
            InitializeComponent();

            foreach (PLAN aplan in Plans)
            {
                Planlist.Items.Add(aplan.planId);
                plannames.Add(aplan.planId);
                // MessageBox.Show("Trig 8");
            }

            Executebutton.Click += (sender, EventArgs) => { LAMBDALINK(sender, EventArgs, Plans, image); };
        }


        private void EXECUTE(List<PLAN> Plans, IMAGE image)
        {
            ProgOutput.Clear();
            CollOutput.Clear();

            bool FAST = true;
            string Acc = null;

            if (FastBox.Checked == false)
            {
                FAST = false;
            }

            if (E6.Checked == true)
            {
                Acc = "6x6 Electron Cone";
            }

            if (E10.Checked == true)
            {
                Acc = "10x10 Electron Cone";
            }

            if (E15.Checked == true)
            {
                Acc = "15x15 Electron Cone";
            }

            if (E20.Checked == true)
            {
                Acc = "20x20 Electron Cone";
            }

            if (E25.Checked == true)
            {
                Acc = "25x25 Electron Cone";
            }

            if (SRSCheckBox.Checked == true)
            {
                Acc = "SRS Cone";
                //foreach (BEAM S in Plan.Beams)
                //{
                //    S.MLCtype = "Static";
                //}
            }

            // need to make plan/plansum pick more robust using PL
            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Program starting...");

            //  MessageBox.Show("pl is: " + pl);
            // plans

            // MessageBox.Show("K is: " + k1.ToString());

            int CNT = Plans.Count();

            //  MessageBox.Show("number of plans: " + CNT);

            if (pl == null)
            {
                MessageBox.Show("You must select a plan for the program to run on before starting! The list of plans in the course currently loaded into Eclipse is in the upper left.");
                return;
            }

            if (bodyloc == null)
            {
                MessageBox.Show("You must select one of the body areas that represents the treatment planning CT associated with this plan!");
                return;
            }

            List<PLAN> templistPlan = Plans.Where(a => a.planId.Equals(pl)).ToList();
            PLAN Plan = templistPlan.First();

            if (height == null)
            {
                if (Plan.patientsex == "Female")
                {
                    height = "161.5";

                }
                else if (Plan.patientsex == "Male")
                {
                    height = "175.4";
                }
                else
                {
                    height = "167.5";
                }
                Heightbox.Text = height;
            }

            List<BEAM> BEAMLIST = new List<BEAM>();

            try
            {
                double ht = Convert.ToDouble(height);
                //List<BEAM> BEAMLIST = new List<BEAM>();
                foreach (BEAM be in Plan.Beams)
                {
                    if (Plan.couchexists == false & Plan.breastboardexists == false)
                    {
                        BEAMLIST.Add(new BEAM
                        {
                            gantrydirection = be.gantrydirection,
                            setupfield = be.setupfield,
                            Isocenter = be.Isocenter,
                            MLCtype = be.MLCtype,
                            beamId = be.beamId,
                            arclength = be.arclength,
                            ControlPoints = be.ControlPoints,
                            imageuserorigin = image.imageuserorigin,
                            imageorigin = image.imageorigin,
                            APISource = be.APISource,
                            planId = Plan.planId,
                            patientId = Plan.patientId,
                            courseId = Plan.courseId,
                            patientsex = Plan.patientsex,
                            StructureSetId = Plan.StructureSetId,
                            TreatmentOrientation = Plan.TreatmentOrientation,
                            Bodycenter = Plan.Bodycenter,
                            CouchInteriorcenter = Plan.CouchInteriorcenter,
                            BreastBoardcenter = Plan.BreastBoardcenter,
                            couchexists = Plan.couchexists,
                            breastboardexists = Plan.breastboardexists,
                            bodylocation = bodyloc,
                            patientheight = ht,

                            //CUSTOM MADE MESH INFO

                            BodyBoxXsize = Plan.BodyBoxXsize,
                            BodyBoxYSize = Plan.BodyBoxYSize,
                            BodyBoxZSize = Plan.BodyBoxZSize,
                            Bodyvects = Plan.Bodyvects,
                            Bodyindices = Plan.Bodyindices
                        });
                    }
                    else if (Plan.couchexists == true & Plan.breastboardexists == false)
                    {
                        BEAMLIST.Add(new BEAM
                        {
                            gantrydirection = be.gantrydirection,
                            setupfield = be.setupfield,
                            Isocenter = be.Isocenter,
                            MLCtype = be.MLCtype,
                            beamId = be.beamId,
                            arclength = be.arclength,
                            ControlPoints = be.ControlPoints,
                            imageuserorigin = image.imageuserorigin,
                            imageorigin = image.imageorigin,
                            APISource = be.APISource,
                            planId = Plan.planId,
                            patientId = Plan.patientId,
                            courseId = Plan.courseId,
                            patientsex = Plan.patientsex,
                            StructureSetId = Plan.StructureSetId,
                            TreatmentOrientation = Plan.TreatmentOrientation,
                            Bodycenter = Plan.Bodycenter,
                            CouchInteriorcenter = Plan.CouchInteriorcenter,
                            BreastBoardcenter = Plan.BreastBoardcenter,
                            couchexists = Plan.couchexists,
                            breastboardexists = Plan.breastboardexists,
                            bodylocation = bodyloc,
                            patientheight = ht,

                            //CUSTOM MADE MESH INFO

                            BodyBoxXsize = Plan.BodyBoxXsize,
                            BodyBoxYSize = Plan.BodyBoxYSize,
                            BodyBoxZSize = Plan.BodyBoxZSize,
                            Bodyvects = Plan.Bodyvects,
                            Bodyindices = Plan.Bodyindices,
                            CouchInteriorvects = Plan.CouchInteriorvects,
                            CouchInteriorindices = Plan.CouchInteriorindices
                        });
                    }
                    else if (Plan.couchexists == false & Plan.breastboardexists == true)
                    {
                        BEAMLIST.Add(new BEAM
                        {
                            gantrydirection = be.gantrydirection,
                            setupfield = be.setupfield,
                            Isocenter = be.Isocenter,
                            MLCtype = be.MLCtype,
                            beamId = be.beamId,
                            arclength = be.arclength,
                            ControlPoints = be.ControlPoints,
                            imageuserorigin = image.imageuserorigin,
                            imageorigin = image.imageorigin,
                            APISource = be.APISource,
                            planId = Plan.planId,
                            patientId = Plan.patientId,
                            courseId = Plan.courseId,
                            patientsex = Plan.patientsex,
                            StructureSetId = Plan.StructureSetId,
                            TreatmentOrientation = Plan.TreatmentOrientation,
                            Bodycenter = Plan.Bodycenter,
                            CouchInteriorcenter = Plan.CouchInteriorcenter,
                            BreastBoardcenter = Plan.BreastBoardcenter,
                            couchexists = Plan.couchexists,
                            breastboardexists = Plan.breastboardexists,
                            bodylocation = bodyloc,
                            patientheight = ht,

                            //CUSTOM MADE MESH INFO

                            BodyBoxXsize = Plan.BodyBoxXsize,
                            BodyBoxYSize = Plan.BodyBoxYSize,
                            BodyBoxZSize = Plan.BodyBoxZSize,
                            Bodyvects = Plan.Bodyvects,
                            Bodyindices = Plan.Bodyindices,
                            BreastBoardvects = Plan.BreastBoardvects,
                            BreastBoardindices = Plan.BreastBoardindices
                        });
                    }
                    else if (Plan.couchexists == true & Plan.breastboardexists == true)
                    {
                        BEAMLIST.Add(new BEAM
                        {
                            gantrydirection = be.gantrydirection,
                            setupfield = be.setupfield,
                            Isocenter = be.Isocenter,
                            MLCtype = be.MLCtype,
                            beamId = be.beamId,
                            arclength = be.arclength,
                            ControlPoints = be.ControlPoints,
                            imageuserorigin = image.imageuserorigin,
                            imageorigin = image.imageorigin,
                            APISource = be.APISource,
                            planId = Plan.planId,
                            patientId = Plan.patientId,
                            courseId = Plan.courseId,
                            patientsex = Plan.patientsex,
                            StructureSetId = Plan.StructureSetId,
                            TreatmentOrientation = Plan.TreatmentOrientation,
                            Bodycenter = Plan.Bodycenter,
                            CouchInteriorcenter = Plan.CouchInteriorcenter,
                            BreastBoardcenter = Plan.BreastBoardcenter,
                            couchexists = Plan.couchexists,
                            breastboardexists = Plan.breastboardexists,
                            bodylocation = bodyloc,
                            patientheight = ht,

                            //CUSTOM MADE MESH INFO

                            BodyBoxXsize = Plan.BodyBoxXsize,
                            BodyBoxYSize = Plan.BodyBoxYSize,
                            BodyBoxZSize = Plan.BodyBoxZSize,
                            Bodyvects = Plan.Bodyvects,
                            Bodyindices = Plan.Bodyindices,
                            BreastBoardvects = Plan.BreastBoardvects,
                            BreastBoardindices = Plan.BreastBoardindices,
                            CouchInteriorvects = Plan.CouchInteriorvects,
                            CouchInteriorindices = Plan.CouchInteriorindices
                        });
                    }
                }

                BEAMLIST.RemoveAll(el => el.setupfield == true);

                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Initiating CollisionCheck...");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }

            // System.Windows.Forms.MessageBox.Show("APISource : (" + BEAMLIST.First().APISource.x + ", " + BEAMLIST.First().APISource.y + ", " + BEAMLIST.First().APISource.z + ")");

            List<CollisionAlert> output = CollisionCheckMethods.CollisionCheckExecute(BEAMLIST, ProgOutput, Acc, FAST);

            //List<CollisionAlert> output = outputTask.Result;

            ProgBar.Visible = false;

            if (output.Count == 0)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Collision analysis complete. No collisions detected.");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Although no collisions were detected, the program will render images of its model for each beam for you to view if you wish.");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("A separate window depicting an image of the 3D collision model will now open for each beam. You can click and drag your mouse (slowly) to rotate the image and use the mouse wheel to zoom in and out.");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("This window will persist until closed.");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("You can close the window or run the program again.");

                CollOutput.AppendText(Environment.NewLine);
                CollOutput.AppendText("No Collisions found.");
                CollOutput.AppendText(Environment.NewLine);

                MessageBox.Show("No Collisions detected!");
            }
            else
            {
                // special dialog box in case of found collision with a red background and such for effect
                string status = "WARNING: COLLISIONS/TIGHT CLEARANCE DETECTED!";

                OutputWindow(status);

                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Collision analysis complete. Collisions/tight clearance detected!");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Rendering Collision images...");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("A separate window depicting an image of the 3D collision model will now open for each beam. You can click and drag your mouse (slowly) to rotate the image and use the mouse wheel to zoom in and out.");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("This window will persist until closed.");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("You can close the window or run the program again.");

                CollOutput.AppendText(Environment.NewLine);
                CollOutput.AppendText("The following collisions are predicted...");
                CollOutput.AppendText(Environment.NewLine);
                CollOutput.AppendText(Environment.NewLine);
            }


            if (output.Count > 0)
            {
                // Patient Point: (" + Math.Round(alert.Patpoint.x, 1, MidpointRounding.AwayFromZero) + ", " + Math.Round(alert.Gantrypoint.y, 1, MidpointRounding.AwayFromZero) + ", " + Math.Round(alert.Gantrypoint.z, 1, MidpointRounding.AwayFromZero) + ")
                // last contig should be false by default in the collision alert class
                foreach (CollisionAlert alert in output)
                {
                    if (output.First().PatObject.StartsWith("FAST"))
                    {
                        CollOutput.AppendText(Environment.NewLine);
                        CollOutput.AppendText(Environment.NewLine);
                        CollOutput.AppendText("Beam " + alert.beam + ": Collision/tight clearance between " + alert.GantryObject + " and " + alert.PatObject + "." + Environment.NewLine + "Gantry Angle: " + alert.gantryangle + "  Couch Angle: " + alert.couchangle + "  Rotation Direction: " + alert.rotationdirection);
                        CollOutput.AppendText(Environment.NewLine);
                    }
                    else
                    {
                        if (alert.contiguous == true)
                        {
                            continue;
                        }

                        if (alert.endoflist == true)
                        {
                            CollOutput.AppendText(Environment.NewLine);
                            CollOutput.AppendText(Environment.NewLine);
                            CollOutput.AppendText("END OF BEAM " + alert.beam + ". " + alert.GantryObject + " still in potential collision area with " + alert.PatObject + "." + Environment.NewLine + "Couch Angle: " + alert.couchangle + "  Gantry Angle: " + alert.gantryangle + "  Rotation Direction: " + alert.rotationdirection + "  Distance: " + alert.distance + " mm");
                            CollOutput.AppendText(Environment.NewLine);
                        }
                        else
                        {
                            if (alert.lastcontig == false)
                            {
                                CollOutput.AppendText(Environment.NewLine);
                                CollOutput.AppendText(Environment.NewLine);
                                CollOutput.AppendText("Beam " + alert.beam + ": START of " + alert.GantryObject + " potential collision area with " + alert.PatObject + "." + Environment.NewLine + "Couch Angle: " + alert.couchangle + "  Gantry Angle: " + alert.gantryangle + "  Rotation Direction: " + alert.rotationdirection + "  Distance: " + alert.distance + " mm");
                                CollOutput.AppendText(Environment.NewLine);
                            }
                            else if (alert.lastcontig == true)
                            {
                                CollOutput.AppendText(Environment.NewLine);
                                CollOutput.AppendText(Environment.NewLine);
                                CollOutput.AppendText("Beam " + alert.beam + ": END of " + alert.GantryObject + " potential collision area with " + alert.PatObject + "." + Environment.NewLine + "Couch Angle: " + alert.couchangle + "  Gantry Angle: " + alert.gantryangle + "  Rotation Direction: " + alert.rotationdirection + "  Distance: " + alert.distance + " mm");
                                CollOutput.AppendText(Environment.NewLine);
                            }
                        }
                    } // ends if FAST mode or not
                }
            }

            Thread.Sleep(1000); //wait 1 seconds
   
            foreach (BEAM Be in BEAMLIST)
            {
                FileInfo file = new FileInfo(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\" + Plan.patientId + "_" + Plan.courseId + "_" + Plan.planId + "_" + "Beam_" + Be.beamId + ".stl"); //Lahey                                                                                                                                                                                                                                // FileInfo file = new FileInfo(@"\\shceclipseimg\PHYSICS\New File Structure PHYSICS\Script Reports\Collision_Check_STL_files\" + Plan.patientId + "_" + Plan.courseId + "_" + Plan.planId + "_" + "Beam_" + al.beam + ".stl"); //Winchester
                bool ftest = IsFileLocked(file, Be.beamId);
                if (ftest == false)
                {
                    string window_init = @"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\" + Plan.patientId + "_" + Plan.courseId + "_" + Plan.planId + "_" + "Beam_" + Be.beamId + ".stl"; //Lahey
                                                                                                                                                                                                                                                        // string window_init = @"\\shceclipseimg\PHYSICS\New File Structure PHYSICS\Script Reports\Collision_Check_STL_files\" + Plan.patientId + "_" + Plan.courseId + "_" + Plan.planId + "_" + "Beam_" + al.beam + ".stl";  //Winchester
                    Thread WPFWindowInit = new Thread(() =>
                    {
                        _3DRender.MainWindow window = new _3DRender.MainWindow(window_init, Be.beamId);
                        window.Closed += (s, e) =>
                            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                        window.Show();
                        Dispatcher.Run();
                    });

                    WPFWindowInit.SetApartmentState(ApartmentState.STA);
                    WPFWindowInit.Start();
                }
            }
            //MessageBox.Show("All images have been displayed in their own window. You can close each window, as well as the GUI, when you are done. The GUI window should now reappear in front of you");

            //this.WindowState = FormWindowState.Normal;
            //this.Activate();
        }

        protected static bool IsFileLocked(FileInfo file, string beamid)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException e)
            {
                System.Windows.Forms.MessageBox.Show("An IOException occured when attempting to open the image file for the beam " + beamid +  "." + Environment.NewLine + " This file is either 1) Still being written to. 2) Being processed by another thread. 3) Does not exist. The program will skip opening the image file." + Environment.NewLine + " The following information is just to help Zack debug this. You can close the dialog box without a problem." + Environment.NewLine + Environment.NewLine + "Source:   " + e.Source + Environment.NewLine + "Message:   " + e.Message + Environment.NewLine + "Stack Trace:   " + e.StackTrace);

                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }

        void PlanList_SelectedIndexChanged(object sender, EventArgs e)
        {
            pl = Planlist.SelectedItem.ToString();

            ProgOutput.Text = "Plan Selected: " + pl;
 
            //  MessageBox.Show("Trig 10");
        }

        void imagelocationlist_SelectedIndexChanged(object sender, EventArgs e)
        {
            bodyloc = imagelocationlist.SelectedItem.ToString();

            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("CT Scan area " + bodyloc);
        }

        private void Executebutton_Click(object sender, EventArgs args)
        {
            //  MessageBox.Show("Organ: " + org.ToString());
            //  MessageBox.Show("Trig 12 - First Click");

            ProgBar.Style = ProgressBarStyle.Marquee;
            ProgBar.Visible = true;
            ProgBar.MarqueeAnimationSpeed = 100;
        }

        void E25Box_CheckChange(object sender, EventArgs args)
        {
            if (SRSCheckBox.Checked == true || E6.Checked == true || E15.Checked == true || E20.Checked == true || E10.Checked == true)
            {
                E25.CheckState = CheckState.Unchecked;
                ProgOutput.AppendText("Only one Gantry accessory can be chosen at a time!");
                return;
            }

            if (E25.CheckState == CheckState.Checked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("25x25 Electron Cone model enabled");
            }
            else if (E25.CheckState == CheckState.Unchecked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("25x25 Electron Cone model disabled");
            }
        }

        void E20Box_CheckChange(object sender, EventArgs args)
        {
            if (SRSCheckBox.Checked == true || E6.Checked == true || E15.Checked == true || E10.Checked == true || E25.Checked == true)
            {
                E20.CheckState = CheckState.Unchecked;
                ProgOutput.AppendText("Only one Gantry accessory can be chosen at a time!");
                return;
            }

            if (E20.CheckState == CheckState.Checked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("20x20 Electron Cone model enabled");
            }
            else if (E20.CheckState == CheckState.Unchecked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("20x20 Electron Cone model disabled");
            }
        }

        void E15Box_CheckChange(object sender, EventArgs args)
        {
            if (SRSCheckBox.Checked == true || E6.Checked == true || E10.Checked == true || E20.Checked == true || E25.Checked == true)
            {
                E15.CheckState = CheckState.Unchecked;
                ProgOutput.AppendText("Only one Gantry accessory can be chosen at a time!");
                return;
            }

            if (E15.CheckState == CheckState.Checked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("15x15 Electron Cone model enabled");
            }
            else if (E15.CheckState == CheckState.Unchecked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("15x15 Electron Cone model disabled");
            }
        }

        void E10Box_CheckChange(object sender, EventArgs args)
        {
            if (SRSCheckBox.Checked == true || E6.Checked == true || E15.Checked == true || E20.Checked == true || E25.Checked == true)
            {
                E10.CheckState = CheckState.Unchecked;
                ProgOutput.AppendText("Only one Gantry accessory can be chosen at a time!");
                return;
            }

            if (E10.CheckState == CheckState.Checked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("10x10 Electron Cone model enabled");
            }
            else if (E10.CheckState == CheckState.Unchecked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("10x10 Electron Cone model disabled");
            }
        }

        void E6Box_CheckChange(object sender, EventArgs args)
        {
            if (SRSCheckBox.Checked == true || E10.Checked == true || E15.Checked == true || E20.Checked == true || E25.Checked == true)
            {
                E6.CheckState = CheckState.Unchecked;
                ProgOutput.AppendText("Only one Gantry accessory can be chosen at a time!");
                return;
            }

            if (E6.CheckState == CheckState.Checked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("6x6 Electron Cone model enabled");
            }
            else if (E6.CheckState == CheckState.Unchecked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("6x6 Electron Cone model disabled");
            }
        }

        void SRSCheckBox_CheckChange(object sender, EventArgs args)
        {
            if(E6.Checked == true || E10.Checked == true || E15.Checked == true || E20.Checked == true || E25.Checked == true)
            {
                SRSCheckBox.CheckState = CheckState.Unchecked;
                ProgOutput.AppendText("Only one Gantry accessory can be chosen at a time!");
                return;
            }

            if(SRSCheckBox.CheckState == CheckState.Checked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("SRS Cone model enabled");
            }
            else if(SRSCheckBox.CheckState == CheckState.Unchecked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("SRS Cone model disabled");
            }
        }

        void FastBox_CheckChange(object sender, EventArgs args)
        {
            if (FastBox.CheckState == CheckState.Checked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Fast Mode enabled");
            }
            else if (FastBox.CheckState == CheckState.Unchecked)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Fast Mode disabled");
            }
        }


        void LAMBDALINK(object sender, EventArgs e, List<PLAN> Plans, IMAGE image)
        {
            try
            {
                Task.Run(() => EXECUTE(Plans, image));
            }
            catch(Exception ae)
            {
                MessageBox.Show(ae.ToString());
            }
        }

        void Heightbox_TextChanged(object sender, EventArgs e)
        {
            height = Heightbox.Text;

            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Height Changed to " + height + " cm.");
        }


        public static DialogResult OutputWindow(string status)
        {
            Form Dialog = new Form()
            {
                Width = 400,
                Height = 400,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Collision Status",
                BackColor = Color.Red,
                StartPosition = FormStartPosition.CenterScreen,
                Font = new System.Drawing.Font("Goudy Old Style", 16.0F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)))
            };
            Label txtlab = new Label() { Left = 10, Top = 15, Width = 320, Height = 140, Text = status };
            Button confirm = new Button() { Text = "Ok", Left = 120, Width = 100, Top = 300, DialogResult = DialogResult.OK };
                            
            confirm.Click += (sender, e) => { Dialog.Close(); };
            Dialog.Controls.Add(confirm);
            Dialog.Controls.Add(txtlab);
            Dialog.AcceptButton = confirm;

            return Dialog.ShowDialog();
        }



    }
}
