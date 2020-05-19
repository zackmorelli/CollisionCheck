using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMS.TPS;

/*
    Collision Check - GUI
    Copyright (c) 2019 Radiation Oncology Department, Lahey Hospital and Medical Center
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

        public GUI(IEnumerable<PlanSetup> Plans, Patient patient, VMS.TPS.Common.Model.API.Image image)
        {
            InitializeComponent();

            foreach (PlanSetup aplan in Plans)
            {
                Planlist.Items.Add(aplan.Id);
                plannames.Add(aplan.Id);
                // MessageBox.Show("Trig 8");
            }

            if (patient.Sex == "Female")
            {
                height = "161.5";
            }
            else if (patient.Sex == "Male")
            {
                height = "175.4";
            }
            else
            {
                height = "167.5";
            }

            Heightbox.Text = height;


            Executebutton.Click += (sender, EventArgs) => { LAMBDALINK(sender, EventArgs, Plans, image); };

        }

        
        private void EXECUTE(IEnumerable<PlanSetup> Plans, VMS.TPS.Common.Model.API.Image image)
        {
            // need to make plan/plansum pick more robust using PL
            // MessageBox.Show("Trig EXE - 1");

                    //  MessageBox.Show("pl is: " + pl);
            // plans

            IEnumerator ER = Plans.GetEnumerator();
            ER.MoveNext();
            PlanSetup Plan = (PlanSetup)ER.Current;
            // MessageBox.Show("K is: " + k1.ToString());

            int CNT = Plans.Count();

            //  MessageBox.Show("number of plans: " + CNT);

            if (Plan.Id == pl)
            {
                //  MessageBox.Show("Trig EXE - 2");
                Plan = (PlanSetup)ER.Current;
                // MessageBox.Show("Plan Id is: " + Plan.Id);
            }
            else
            {
                ER.MoveNext();
                Plan = (PlanSetup)ER.Current;
                if (Plan.Id == pl)
                {
                    // MessageBox.Show("Plan Id is: " + Plan.Id);
                    Plan = (PlanSetup)ER.Current;
                }
                else
                {
                    ER.MoveNext();
                    Plan = (PlanSetup)ER.Current;
                    if (Plan.Id == pl)
                    {
                        Plan = (PlanSetup)ER.Current;
                    }
                    else 
                    {
                        ER.MoveNext();
                        Plan = (PlanSetup)ER.Current;
                        if (Plan.Id == pl)
                        {
                            Plan = (PlanSetup)ER.Current;
                        }
                        else
                        {
                            ER.MoveNext();
                            Plan = (PlanSetup)ER.Current;
                            if (Plan.Id == pl)
                            {
                                Plan = (PlanSetup)ER.Current;
                            }
                            else
                            {
                                ER.MoveNext();
                                Plan = (PlanSetup)ER.Current;
                                if (Plan.Id == pl)
                                {
                                    Plan = (PlanSetup)ER.Current;
                                }
                                else
                                {
                                    ER.MoveNext();
                                    Plan = (PlanSetup)ER.Current;
                                    if (Plan.Id == pl)
                                    {
                                        Plan = (PlanSetup)ER.Current;
                                    }
                                    else
                                    {
                                        ER.MoveNext();
                                        Plan = (PlanSetup)ER.Current;
                                        if (Plan.Id == pl)
                                        {
                                            Plan = (PlanSetup)ER.Current;
                                        }
                                        else
                                        {
                                            ER.MoveNext();
                                            Plan = (PlanSetup)ER.Current;
                                            if (Plan.Id == pl)
                                            {
                                                Plan = (PlanSetup)ER.Current;
                                            }
                                            else
                                            {
                                                ER.MoveNext();
                                                Plan = (PlanSetup)ER.Current;
                                                if (Plan.Id == pl)
                                                {
                                                    Plan = (PlanSetup)ER.Current;
                                                }
                                                else
                                                {
                                                    ER.MoveNext();
                                                    Plan = (PlanSetup)ER.Current;
                                                    if (Plan.Id == pl)
                                                    {
                                                        Plan = (PlanSetup)ER.Current;
                                                    }
                                                    else
                                                    {
                                                        MessageBox.Show("Could not find the selected plan!");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            double ht = Convert.ToDouble(height);
           
            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Program starting...");

            List<Script.CollisionAlert> output = Script.CollisionCheck(Plan, bodyloc, ht, ProgOutput, image);

            MessageBox.Show("Collision analysis completed");
            MessageBox.Show("Collisions detected: " + output.Count);

            if (output.Count > 0)
            {

               // Patient Point: (" + Math.Round(alert.Patpoint.x, 1, MidpointRounding.AwayFromZero) + ", " + Math.Round(alert.Gantrypoint.y, 1, MidpointRounding.AwayFromZero) + ", " + Math.Round(alert.Gantrypoint.z, 1, MidpointRounding.AwayFromZero) + ")
                foreach (Script.CollisionAlert alert in output)
                {
                    CollOutput.AppendText(Environment.NewLine);
                    CollOutput.AppendText(Environment.NewLine);
                    CollOutput.AppendText("COLLISION: Beam: " + alert.beam + "  Couch Angle: " + alert.couchangle + Environment.NewLine + "  Gantry Angle: " + alert.gantryangle + "   " + alert.distpoint);                                           
                    CollOutput.AppendText(Environment.NewLine);

                    if(alert.edgeclip == "upper left")
                    {
                        CollOutput.AppendText("Note: This point is close to the upper left edge of the patient bounding box and is likely clipping through this corner and not at risk of colliding with the patient.");
                    }
                    else if (alert.edgeclip == "upper right")
                    {
                        CollOutput.AppendText("Note: This point is close to the upper right edge of the patient bounding box and is likely clipping through this corner and not at risk of colliding with the patient.");
                    }
                    else if (alert.edgeclip == "lower left")
                    {
                        CollOutput.AppendText("Note: This point is close to the lower left edge of the patient bounding box and is likely clipping through this corner and not at risk of colliding with the patient.");
                    }
                    else if (alert.edgeclip == "lower right")
                    {
                        CollOutput.AppendText("Note: This point is close to the lower right edge of the patient bounding box and is likely clipping through this corner and not at risk of colliding with the patient.");
                    }

                    if (alert.pbodyalert == true)
                    {
                        CollOutput.AppendText(Environment.NewLine);
                        CollOutput.AppendText("DANGER: THIS POINT INTERSECTS THE BODY CONTOUR OF THE PATIENT AND WILL RESULT IN A COLLISION.");
                    }

                }
                MessageBox.Show("End - at least one collision outputted/in list");
            }
            else
            {
                MessageBox.Show("End - No collisions in list");
            }
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
            ProgOutput.AppendText("Image location " + bodyloc);
        }

        private void Executebutton_Click(object sender, EventArgs args)
        {
            //  MessageBox.Show("Organ: " + org.ToString());
            //  MessageBox.Show("Trig 12 - First Click");
        }

        void LAMBDALINK(object sender, EventArgs e, IEnumerable<PlanSetup> Plans, VMS.TPS.Common.Model.API.Image image)
        {
            EXECUTE(Plans, image);
        }

        void Heightbox_TextChanged(object sender, EventArgs e)
        {
            height = Heightbox.Text;

            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Height Changed to " + height + " cm.");
        }

    }
}
