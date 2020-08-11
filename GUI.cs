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

            // need to make plan/plansum pick more robust using PL
            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Program starting...");

            //  MessageBox.Show("pl is: " + pl);
            // plans

            // MessageBox.Show("K is: " + k1.ToString());

            int CNT = Plans.Count();

            //  MessageBox.Show("number of plans: " + CNT);

            if(pl == null)
            {
                MessageBox.Show("You must select a plan for the program to run on before starting! The list of plans in the course currently loaded into Eclipse is in the upper left.");
                return;
            }

            if(bodyloc == null)
            {
                MessageBox.Show("You must select one of the body areas that represents the treatment planning CT associated with this plan!");
                return;
            }

            List<PLAN> templistPlan = Plans.Where(a => a.planId.Equals(pl)).ToList();
            PLAN Plan = templistPlan.First();
            
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
                    else if(Plan.couchexists == true & Plan.breastboardexists == false)
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
                    else if(Plan.couchexists == false & Plan.breastboardexists == true)
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
                    else if(Plan.couchexists == true & Plan.breastboardexists == true)
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
            catch(Exception e)
            {
                MessageBox.Show(e.ToString());
            }

            List<CollisionAlert> output = CollisionCheckMethods.CollisionCheckExecute(BEAMLIST, ProgOutput);

            //List<CollisionAlert> output = outputTask.Result;

            ProgBar.Visible = false;

            if (output.Count == 0)
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Collision analysis complete. No collisions detected.");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("The program is now done running.");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("This window will persist until closed.");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("You can close the window or run the program again.");

                CollOutput.AppendText(Environment.NewLine);
                CollOutput.AppendText("No Collisions found.");
                CollOutput.AppendText(Environment.NewLine);

                MessageBox.Show("No Collisions detected! Program done.");
            }
            else
            {
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Collision analysis complete. Collisions detected!");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Rendering Collision images...");
                MessageBox.Show("Collisions detected!");
            }

            if (output.Count > 0)
            {
                // Patient Point: (" + Math.Round(alert.Patpoint.x, 1, MidpointRounding.AwayFromZero) + ", " + Math.Round(alert.Gantrypoint.y, 1, MidpointRounding.AwayFromZero) + ", " + Math.Round(alert.Gantrypoint.z, 1, MidpointRounding.AwayFromZero) + ")
                // last contig should be false by default in the collision alert class
                foreach (CollisionAlert alert in output)
                {
                    if(alert.contiguous == true)
                    {
                        continue;
                    }

                    if (alert.endoflist == true)
                    {
                        CollOutput.AppendText(Environment.NewLine);
                        CollOutput.AppendText(Environment.NewLine);
                        CollOutput.AppendText("END OF BEAM " + alert.beam + ". Still colliding with " + alert.type + "." + Environment.NewLine + "Couch Angle: " + alert.couchangle + "  Gantry Angle: " + alert.gantryangle + "  Rotation Direction: " + alert.rotationdirection + "  Distance: " + alert.distance + " mm");
                        CollOutput.AppendText(Environment.NewLine);
                    }
                    else
                    {
                        if (alert.lastcontig == false)
                        {
                            CollOutput.AppendText(Environment.NewLine);
                            CollOutput.AppendText(Environment.NewLine);
                            CollOutput.AppendText("Beam " + alert.beam + ": START of gantry collision area with " + alert.type + "." + Environment.NewLine + "Couch Angle: " + alert.couchangle + "  Gantry Angle: " + alert.gantryangle + "  Rotation Direction: " + alert.rotationdirection + "  Distance: " + alert.distance + " mm");
                            CollOutput.AppendText(Environment.NewLine);
                        }
                        else if (alert.lastcontig == true)
                        {
                            CollOutput.AppendText(Environment.NewLine);
                            CollOutput.AppendText(Environment.NewLine);
                            CollOutput.AppendText("Beam " + alert.beam + ": END of gantry collision area with " + alert.type + "." + Environment.NewLine + "Couch Angle: " + alert.couchangle + "  Gantry Angle: " + alert.gantryangle + "  Rotation Direction: " + alert.rotationdirection + "  Distance: " + alert.distance + " mm");
                            CollOutput.AppendText(Environment.NewLine);
                        }
                    }
                }

                Thread.Sleep(1000); //wait 1 seconds
                MessageBox.Show("A separate window depicting an image of the 3D collision model will now open for each beam where a collision was detected (takes about ten seconds). You can click and drag your mouse (slowly) to rotate the image and use the mouse wheel to zoom in and out.");
          
                List<string> beam_distinct_enforcer = new List<string>();

                int wincnt = 0;

                foreach(CollisionAlert al in output)
                {
                    if (wincnt > 0)
                    {
                        if(beam_distinct_enforcer.Last() == al.beam)
                        {
                            //same beam, skip
                            continue;
                        }
                    }

                    beam_distinct_enforcer.Add(al.beam);
                        
                    FileInfo file = new FileInfo(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\" + Plan.patientId + "_" + Plan.courseId + "_" + Plan.planId + "_" + "Beam_" + al.beam + ".stl");
                    bool ftest = IsFileLocked(file, al);
                    if (ftest == false)
                    {

                        string window_init = @"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\" + Plan.patientId + "_" + Plan.courseId + "_" + Plan.planId + "_" + "Beam_" + al.beam + ".stl";

                        Thread WPFWindowInit = new Thread(() =>
                        {
                            _3DRender.MainWindow window = new _3DRender.MainWindow(window_init);
                            window.Closed += (s, e) =>
                                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                            window.Show();
                            Dispatcher.Run();
                        });

                        WPFWindowInit.SetApartmentState(ApartmentState.STA);
                        WPFWindowInit.Start();

                    }
                    wincnt++;
                }
                //MessageBox.Show("All images have been displayed in their own window. You can close each window, as well as the GUI, when you are done. The GUI window should now reappear in front of you");

                //this.WindowState = FormWindowState.Normal;
                //this.Activate();

                Thread.Sleep(12000);

                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("The program is now done running.");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("This window will persist until closed.");
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("You can close the window or run the program again."); 
            }
        }

        protected static bool IsFileLocked(FileInfo file, CollisionAlert al)
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
                System.Windows.Forms.MessageBox.Show("An IOException occured when attempting to open the image file for the beam " + al.beam +  "." + Environment.NewLine + " This file is either 1) Still being written to. 2) Being processed by another thread. 3) Does not exist. The program will skip opening the image file." + Environment.NewLine + " The following information is just to help Zack debug this. You can close the dialog box without a problem." + Environment.NewLine + Environment.NewLine + "Source:   " + e.Source + Environment.NewLine + "Message:   " + e.Message + Environment.NewLine + "Stack Trace:   " + e.StackTrace);

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
    }
}
