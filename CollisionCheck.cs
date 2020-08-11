using System;
using System.Media;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using g3;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;



namespace CollisionCheck
{
    public class CollisionCheckMethods
    {

        public static double ABSDISTANCE(Vector3d V1, Vector3d V2)
        {
            double distance = 0.0;
            double xdiff = 0.0;
            double ydiff = 0.0;
            double zdiff = 0.0;

            xdiff = V1.x - V2.x;
            ydiff = V1.y - V2.y;
            zdiff = V1.z - V2.z;

            distance = Math.Sqrt(Math.Pow(Math.Abs(xdiff), 2.0) + Math.Pow(Math.Abs(ydiff), 2.0) + Math.Pow(Math.Abs(zdiff), 2.0));

            return distance;
        }

        public static async Task<StreamReader> GantryRetrievalAsync(string pid, string cid, string lid, BEAM beam)
        {
            StreamReader GantryAngleRetrieveOutput = await Task.Run(() => ActualRetrieval(pid, cid, lid, beam));
            return GantryAngleRetrieveOutput;
        }

        public static StreamReader ActualRetrieval(string pid, string cid, string lid, BEAM beam)
        {
            
            ProcessStartInfo processinfo = new ProcessStartInfo(@"\\wvvrnimbp01ss\va_data$\filedata\ProgramData\Vision\Stand-alone Programs\CollisionCheck_InfoRetrieval\CollisionCheck_InfoRetrieval.exe", pid + " " + cid + " " + beam.beamId + " " + lid);        // path name of the Collision retrieval program
            
            processinfo.UseShellExecute = false;
            processinfo.ErrorDialog = false;
            processinfo.RedirectStandardOutput = true;
            processinfo.WindowStyle = ProcessWindowStyle.Hidden;

            Process GantryAngleRetrieve = new Process();
            GantryAngleRetrieve.StartInfo = processinfo;
            GantryAngleRetrieve.EnableRaisingEvents = true;

            GantryAngleRetrieve.Start();
            StreamReader Gstream = GantryAngleRetrieve.StandardOutput;
            GantryAngleRetrieve.WaitForExit();
            return Gstream;
        }


        //=========================================================================================================================================================================================================================================

        public static List<CollisionAlert> BeamCollisionAnalysis(BEAM beam, TextBox ProgOutput)
        {
  
            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Beam " + beam.beamId + " analysis running....");
            
            List<CollisionAlert> collist = new List<CollisionAlert>();

            double INTERSECTDIST = 50.0;
            Index2i snear_tids = new Index2i(-1, -1);
            DistTriangle3Triangle3 STriDist;
            double ZABSDIST;
            double reportCang;

            DMesh3 PBodyContour = new DMesh3();
            DMeshAABBTree3 PBodyContourSpatial = new DMeshAABBTree3(PBodyContour);

            DMesh3 PCouchInterior = new DMesh3();
            DMeshAABBTree3 PCouchInteriorSpatial = new DMeshAABBTree3(PCouchInterior);

            DMesh3 PProne_Brst_Board = new DMesh3();
            DMeshAABBTree3 PProne_Brst_BoardSpatial = new DMeshAABBTree3(PProne_Brst_Board);

            DMesh3 PATBOX = new DMesh3();
            DMeshAABBTree3 spatial = new DMeshAABBTree3(PATBOX);

            // ANGLES ARE IN DEGREES
            List<double> GAngleList = new List<double>();
            int gantrylistCNT = 0;
            double CouchStartAngle = beam.ControlPoints[0].Couchangle;
            double CouchEndAngle = beam.ControlPoints[beam.ControlPoints.Count - 1].Couchangle;       // count - 1 is the end becuase the index starts at 0
            List<WriteMesh> tempbeam = new List<WriteMesh>();

            //these variables are used in the collision anlysis
            double? MRGPATBOX = null;
            double? MRGCSURFACE = null;
            double? MRGCINTERIOR = null;
            double? MRGBBOARD = null;
            bool lastcontigrealPATBOX = false;
            bool lastcontigrealCouch = false;
            bool lastcontigrealBoard = false;


            if (CouchStartAngle != CouchEndAngle)
            {
                 System.Windows.Forms.MessageBox.Show("WARNING: The patient couch has a different rotation angle at the end of beam " + beam.beamId + " in plan " + beam.planId + " than what the beam starts with.");
                // just in case the start and stop of an beam have different couch angles for some reason. not sure if this is actually possible.
            }
            else if (CouchStartAngle == CouchEndAngle)
            {
                //ProgOutput.AppendText(Environment.NewLine);
                //ProgOutput.AppendText("Starting analysis of beam " + beam.Id + " in plan " + plan.Id + ".");
                // MessageBox.Show("Starting analysis of beam " + beam.Id + " in plan " + plan.Id + " .");

                Vector3d ISO = beam.Isocenter;

                Vector3d UserOrigin = beam.imageuserorigin;
                Vector3d Origin = beam.imageorigin;
                

                bool cp = false;  //this represents whether the program is using MLC control points or not

               // ProgOutput.AppendText(Environment.NewLine);
               // ProgOutput.AppendText("Beam " + beam.Id + " calling BOXMAKER....");

                List<DMesh3> PatMeshList = new List<DMesh3>();

                // calls the boxmaker method which makes all the patient realated 3D meshes.

                try
                {
                   // System.Windows.Forms.MessageBox.Show("Before Patbox");

                    PatMeshList = BOXMAKER(beam, PBodyContour, PBodyContourSpatial, PCouchInterior, PCouchInteriorSpatial, PATBOX, spatial, PProne_Brst_Board, PProne_Brst_BoardSpatial);
                    
                }
                catch(Exception e)
                {
                    System.Windows.Forms.MessageBox.Show(e.ToString());
                }

               // System.Windows.Forms.MessageBox.Show("after Patbox");
               // System.Windows.Forms.MessageBox.Show("PatMeshListSize: " + PatMeshList.Count);

                if (beam.couchexists == true & beam.breastboardexists == true)
                {
                    PBodyContour = PatMeshList[0];
                    tempbeam.Add(new WriteMesh(PBodyContour));
                    PBodyContourSpatial = new DMeshAABBTree3(PBodyContour);
                    PBodyContourSpatial.Build();

                    PCouchInterior = PatMeshList[1];
                    tempbeam.Add(new WriteMesh(PCouchInterior));
                    PCouchInteriorSpatial = new DMeshAABBTree3(PCouchInterior);
                    PCouchInteriorSpatial.Build();

                    PProne_Brst_Board = PatMeshList[2];
                    tempbeam.Add(new WriteMesh(PProne_Brst_Board));
                    PProne_Brst_BoardSpatial = new DMeshAABBTree3(PProne_Brst_Board);
                    PProne_Brst_BoardSpatial.Build();

                    PATBOX = PatMeshList[3];
                    spatial = new DMeshAABBTree3(PATBOX);
                    spatial.Build();
                }
                else if(beam.couchexists == false & beam.breastboardexists == false)
                {
                    PBodyContour = PatMeshList[0];
                    tempbeam.Add(new WriteMesh(PBodyContour));
                    PBodyContourSpatial = new DMeshAABBTree3(PBodyContour);
                    PBodyContourSpatial.Build();


                    PATBOX = PatMeshList[1];
                    spatial = new DMeshAABBTree3(PATBOX);
                    spatial.Build();

                }
                else if(beam.couchexists == true & beam.breastboardexists == false)
                {
                    PBodyContour = PatMeshList[0];
                    tempbeam.Add(new WriteMesh(PBodyContour));
                    PBodyContourSpatial = new DMeshAABBTree3(PBodyContour);
                    PBodyContourSpatial.Build();

                    PCouchInterior = PatMeshList[1];
                    tempbeam.Add(new WriteMesh(PCouchInterior));
                    PCouchInteriorSpatial = new DMeshAABBTree3(PCouchInterior);
                    PCouchInteriorSpatial.Build();

                    PATBOX = PatMeshList[2];
                    spatial = new DMeshAABBTree3(PATBOX);
                    spatial.Build();
                }
                else if(beam.couchexists == false & beam.breastboardexists == true)
                {
                    PBodyContour = PatMeshList[0];
                    tempbeam.Add(new WriteMesh(PBodyContour));
                    PBodyContourSpatial = new DMeshAABBTree3(PBodyContour);
                    PBodyContourSpatial.Build();

                    PProne_Brst_Board = PatMeshList[1];
                    tempbeam.Add(new WriteMesh(PProne_Brst_Board));
                    PProne_Brst_BoardSpatial = new DMeshAABBTree3(PProne_Brst_Board);
                    PProne_Brst_BoardSpatial.Build();

                    PATBOX = PatMeshList[2];
                    spatial = new DMeshAABBTree3(PATBOX);
                    spatial.Build();
                }

                // System.Windows.Forms.MessageBox.Show("Isocenter point is at: (" + ISO.x + " ," + ISO.y + " ," + ISO.z + ")");
                // MessageBox.Show("Image origin is at: (" + Origin.x + " ," + Origin.y + " ," + Origin.z + ")");
                // System.Windows.Forms.MessageBox.Show("User Origin at: (" + UserOrigin.x + " ," + UserOrigin.y + " ," + UserOrigin.z + ")");

                // source position creation
                double myZ = 0.0;
                double myX = 0.0;
                double myY = 0.0;

                double ANGLE = 0.0;
                double Gangle = 0.0;

                string gantryXISOshift = null;   //not an iso shift, this variable is used in the intial construction of the gantry disk model

                // initial gantry head point construction
                double gfxp = 0.0;
                double gfyp = 0.0;
                double gantrycenterxtrans = 0.0;
                double gantrycenterztrans = 0.0;

                double xpp = 0.0;
                double ypp = 0.0;
                // coordinate system transform

                double thetap = 0.0;

                double Backxtrans = 0.0;
                double Frontxtrans = 0.0;
                double Leftxtrans = 0.0;
                double Rightxtrans = 0.0;

                double Backztrans = 0.0;
                double Frontztrans = 0.0;
                double Leftztrans = 0.0;
                double Rightztrans = 0.0;
                //  DMeshAABBTree3.IntersectionsQueryResult intersectlist = new DMeshAABBTree3.IntersectionsQueryResult();
                //  double GantryAngle = 5000.5;

                if (beam.MLCtype == "Static")
                {
                    double GantryStartAngle = 500.0;   //initially set to 500 so that they are clearly outside of the appropriate domain, not a real angle
                    double GantryEndAngle = 500.0;
                    string ArcDirection = null;

                    //ProgOutput.AppendText(Environment.NewLine);
                    //ProgOutput.AppendText("This is a static MLC beam with no control points. Attempting to get Gantry information for this beam from the ARIA database (this might take a minute)... ");

                    //System.Windows.Forms.MessageBox.Show("This is a static MLC beam with no control points. The program will get the gantry information it needs for this beam from the ARIA database.\nA blank terminal window will appear while it does this. A dialogue box will appear that will tell you that the program is busy because it is waiting for the other program to query the database.\nYou will have to click on 'switch to' several times until it is done. The GUI window will reappear when the program is finished.");

                    Task<StreamReader> TGantryAngleRetrieveOutput = GantryRetrievalAsync(beam.patientId, beam.courseId, beam.planId, beam);
                    
                    StreamReader GantryAngleRetrieveOutput = TGantryAngleRetrieveOutput.Result;

                    // This is high-level .NET multithreading using the Task Parallel Library included in .NET 4.0

                    //ProgOutput.AppendText(Environment.NewLine);
                    //ProgOutput.AppendText("Aria retrieval complete! Building list of gantry angles...");

                    ArcDirection = GantryAngleRetrieveOutput.ReadLine();
                    GantryStartAngle = Convert.ToDouble(GantryAngleRetrieveOutput.ReadLine());
                    GantryEndAngle = Convert.ToDouble(GantryAngleRetrieveOutput.ReadLine());

                    if (ArcDirection == "NONE")
                    {
                        GAngleList.Add(GantryStartAngle);
                    }
                    else if (ArcDirection == "CW")
                    {
                        double tempangle = GantryStartAngle;
                        GAngleList.Add(GantryStartAngle);

                        while (tempangle != GantryEndAngle)
                        {
                            tempangle++;

                            if (tempangle == 360)
                            {
                                tempangle = 0;
                            }

                            GAngleList.Add(tempangle);
                        }
                    }
                    else if (ArcDirection == "CC")
                    {
                        double tempangle = GantryStartAngle;
                        GAngleList.Add(GantryStartAngle);

                        while (tempangle != GantryEndAngle)
                        {
                            tempangle--;

                            if (tempangle == -1)
                            {
                                tempangle = 359;
                            }

                            GAngleList.Add(tempangle);
                        }
                    }
                }
                else
                {
                    //System.Windows.Forms.MessageBox.Show("Arc Length: " + beam.ArcLength);
                    if (beam.arclength == 0)
                    {
                        //ProgOutput.AppendText(Environment.NewLine);
                        //ProgOutput.AppendText("Static gantry IMRT beam. Retrieving gantry angle from first MLC control point ... ");
                        GAngleList.Add(beam.ControlPoints.First().Gantryangle);
                    }
                    else
                    {
                        //ProgOutput.AppendText(Environment.NewLine);
                        //ProgOutput.AppendText("Moving gantry IMRT beam. Building list of gantry angles from control points ... ");
                        cp = true;

                        foreach (CONTROLPOINT point in beam.ControlPoints)
                        {
                            GAngleList.Add(point.Gantryangle);
                        }
                    }
                    // System.Windows.Forms.MessageBox.Show("Trigger 1");
                }
                // System.Windows.Forms.MessageBox.Show("Trigger 2");

                foreach (double GantryAngle in GAngleList)
                {
                    gantrylistCNT++;
                    // System.Windows.Forms.MessageBox.Show("Gantry ANGLE :  " + GantryAngle + "  ");
                    //  MessageBox.Show("couch ANGLE :  " + CouchEndAngle + "  ");
                    if (GAngleList.Count <= 10)
                    {
                        //ProgOutput.AppendText(Environment.NewLine);
                        //ProgOutput.AppendText("Gantry Angle: " + gantrylistCNT + "/" + GAngleList.Count);
                    }
                    else if (cp == false)
                    {
                        if (gantrylistCNT % 4 == 0)
                        {
                            //ProgOutput.AppendText(Environment.NewLine);
                            //ProgOutput.AppendText("Gantry Angle: " + gantrylistCNT + "/" + GAngleList.Count);
                            // System.Windows.Forms.MessageBox.Show("Current (not from cp) Gangle: " + GantryAngle);

                        }
                        else
                        {
                            continue;
                        }
                    }
                    else if (cp == true)
                    {
                        if (gantrylistCNT % 3 == 0)
                        {
                            //ProgOutput.AppendText(Environment.NewLine);
                            // ProgOutput.AppendText("Gantry Angle: " + gantrylistCNT + "/" + GAngleList.Count);
                            //System.Windows.Forms.MessageBox.Show("Current (cp) Gangle: " + GantryAngle);
                            // ProgBar.PerformStep();
                        }
                        else
                        {
                            continue;
                        }
                    }

                    //System.Windows.Forms.MessageBox.Show("Trigger 3");
                    // ProgOutput.AppendText(Environment.NewLine);
                    //ProgOutput.AppendText("Conducting Collision analysis and writing STL files to disk...");

                    //  MessageBox.Show("real couch ANGLE :  " + realangle + "  ");

                    //VVector APISOURCE = beam.GetSourceLocation(GantryAngle);  // negative Y
                    // MessageBox.Show("SOURCE (already transformed by API): (" + APISOURCE.x + " ," + APISOURCE.y + " ," + APISOURCE.z + ")");

                    /*  So, the issue with the Source position is that it actually does change in accordance with the couch angle,
                     *  in other words, the position returned by the source location method has already had a coordinate transformation
                     *  performed on it so that it is in the coordinate system of the patient's image. And it is in the Eclipse coord. system.
                     *  
                     *  Because the gantry center point and other points of the gantry need to first be constructed with everything at couch zero,
                     *  the position of the Source at couch zero needs to be determined, otherwise the gantrycenter point calculated from the source can be wrong,
                     *  because it may operate on the wrong position components (because it assumes the couch is at zero). 
                     *  
                     *  It is safer to calculate the position of all the gantry points as if they were at couch zero first, and then do a coord. transform for the couch angle.
                     *  
                     *  Anyway, in order to find the correct position of the gantry center point we need to find the couch zero position of source, using the following equation.
                     */

                    // need to come up with something to set polarity ISO.x and ISO.y based on patient orientation and where the ISO is.
                    // ISO coords can be negative !!!!!

                    // this just innately handles iso shifts. No artifical shifts are neccessary. The only thing is the gantry disks are rotated at the end if a prone orientation. the patient structures are never shifted.
                    myZ = ISO.z;
                    myX = 1000 * Math.Cos((((GantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.x;    // - 90 degrees is because the polar coordinate system has 0 degrees on the right side
                    myY = 1000 * Math.Sin((((-GantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.y;   // need negative because y axis is inverted
                                                                                                  // THIS WORKS!
                    Vector3d mySOURCE = new Vector3d(myX, myY, myZ);

                    // MessageBox.Show("mySOURCE: (" + mySOURCE.x + " ," + mySOURCE.y + " ," + mySOURCE.z + ")");

                    // MessageBox.Show("SOURCE : (" + convSOURCE.x + " ," + convSOURCE.y + " ," + convSOURCE.z + ")");

                    // this determines the position of gantrycenterpoint (from mySOURCE) at all gantry angles at couch 0 degrees

                    if (GantryAngle > 270.0)
                    {
                        Gangle = 90.0 - (GantryAngle - 270.0);

                        gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                        gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                    }
                    else if (GantryAngle >= 0.0 & GantryAngle <= 90.0)
                    {
                        Gangle = GantryAngle;
                        gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                        gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                    }
                    else if (GantryAngle > 90.0 & GantryAngle <= 180.0)
                    {
                        Gangle = 90.0 - (GantryAngle - 90.0);

                        gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                        gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                    }
                    else if (GantryAngle > 180.0 & GantryAngle <= 270.0)
                    {
                        //  MessageBox.Show("Trig 5");
                        Gangle = GantryAngle - 180.0;

                        gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                        gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                    }

                    thetap = 90.0 - Gangle;

                    // MessageBox.Show("thetap is: " + thetap);

                    Vector3d gantrycenter = mySOURCE;    // this will represent the center of the gantry head's surface once the transforms below are performed
                                                         // For couch zero degrees

                    if (GantryAngle >= 270.0 | (GantryAngle >= 0.0 & GantryAngle <= 90.0))
                    {
                        gantrycenter.y = gantrycenter.y + gfyp;
                        //  MessageBox.Show("gf.y is: " + gf.y);
                    }
                    else if (GantryAngle < 270.0 & GantryAngle > 90.0)
                    {
                        gantrycenter.y = gantrycenter.y - gfyp;
                    }

                    // this just determines if the original xshift to gf is positive or negative
                    if (GantryAngle >= 0.0 & GantryAngle <= 180.0)
                    {
                        gantrycenter.x = gantrycenter.x - gfxp;
                        gantryXISOshift = "POS";
                    }
                    else if (GantryAngle > 180.0)
                    {
                        gantrycenter.x = gantrycenter.x + gfxp;
                        gantryXISOshift = "NEG";
                    }

                    Vector3d origgantrycenter = gantrycenter;
                    //System.Windows.Forms.MessageBox.Show("Trigger 4");
                    // MessageBox.Show("gantrycenter before transform is: (" + gantrycenter.x + " ," + gantrycenter.y + " ," + gantrycenter.z + ")");
                    //gantrycenter now represents the center point of the gantry for all gantry angles at 0 degrees couch angle
                    // a coordinate transformation for couch angle is performed next
                    //once the gantry centerpoint and the patient are in the same coordinate system, the edges of the gantry are found from there.


                    // GANTRY CENTER POINT COORDINATE SYSTEM TRANSFORMATION FOR COUCH ANGLE
                    if (CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
                    {
                        // this is really for 0 to 90 couch angle
                        //  MessageBox.Show("REAL couch ANGLE :  " + realangle + "  ");
                        //  MessageBox.Show("TRIGGER ROT 0 to 270");
                        ANGLE = 360.0 - CouchEndAngle;

                        gantrycenterxtrans = (gantrycenter.x * Math.Cos(((-ANGLE * Math.PI) / 180.0))) + (gantrycenter.z * Math.Sin(((-ANGLE * Math.PI) / 180.0)));
                        gantrycenterztrans = (-gantrycenter.x * Math.Sin(((-ANGLE * Math.PI) / 180.0))) + (gantrycenter.z * Math.Cos(((-ANGLE * Math.PI) / 180.0)));

                    }
                    else if (CouchEndAngle >= 0.0 & CouchEndAngle <= 90.0)
                    {
                        ANGLE = CouchEndAngle;

                        gantrycenterxtrans = (gantrycenter.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (gantrycenter.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                        gantrycenterztrans = (-gantrycenter.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (gantrycenter.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));
                    }

                    // that is just the first part of the coordinate system. we now need to account for shifts to the respective ccordinate axes for the ISO position. This is dependent on couch and gantry rotation.

                    if (CouchEndAngle >= 0.0 & CouchEndAngle <= 90.0)
                    {
                        if (gantryXISOshift == "NEG")   // gantry on left side
                        {
                            gantrycenter.x = gantrycenterxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            gantrycenter.z = gantrycenterztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (ISO.x + ISO.z));
                        }
                        else if (gantryXISOshift == "POS")  // gantry on right side
                        {
                            gantrycenter.x = gantrycenterxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            gantrycenter.z = gantrycenterztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.x + ISO.z));
                        }
                    }
                    else if (CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
                    {
                        if (gantryXISOshift == "NEG")   // gantry on left side
                        {
                            gantrycenter.x = gantrycenterxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            gantrycenter.z = gantrycenterztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (ISO.x + ISO.z));
                        }
                        else if (gantryXISOshift == "POS")  // gantry on right side
                        {
                            gantrycenter.x = gantrycenterxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            gantrycenter.z = gantrycenterztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.x + ISO.z));
                        }
                    }

                    // MessageBox.Show("gantrycenter after transform is: (" + gantrycenter.x + " ," + gantrycenter.y + " ," + gantrycenter.z + ")");

                    ypp = 384.0 * Math.Cos((thetap * Math.PI) / 180.0);      // gantry head diameter is 76.5 cm
                    xpp = 384.0 * Math.Sin((thetap * Math.PI) / 180.0);

                    // calaulate the left, right, front, back points of the gantry head for couch at 0 deg, for all gantry angles
                    // these 4 points represent the gantry head
                    Vector3d RIGHTEDGE = origgantrycenter;
                    Vector3d LEFTEDGE = origgantrycenter;
                    Vector3d BACKEDGE = origgantrycenter;
                    Vector3d FRONTEDGE = origgantrycenter;

                    FRONTEDGE.z = FRONTEDGE.z - 384.0;
                    BACKEDGE.z = BACKEDGE.z + 384.0;

                    if (GantryAngle >= 0.0 & GantryAngle <= 90.0)
                    {
                        //  MessageBox.Show("Trigger gantry angle between 0 and 90 gantry points calculation");
                        RIGHTEDGE.y = RIGHTEDGE.y + ypp;
                        LEFTEDGE.y = LEFTEDGE.y - ypp;
                        RIGHTEDGE.x = RIGHTEDGE.x + xpp;
                        LEFTEDGE.x = LEFTEDGE.x - xpp;
                    }
                    else if (GantryAngle > 270.0)
                    {
                        //MessageBox.Show("Trigger gantry angle > 270 gantry points calculation");
                        RIGHTEDGE.y = RIGHTEDGE.y - ypp;
                        LEFTEDGE.y = LEFTEDGE.y + ypp;
                        RIGHTEDGE.x = RIGHTEDGE.x + xpp;
                        LEFTEDGE.x = LEFTEDGE.x - xpp;
                    }
                    else if (GantryAngle > 90.0 & GantryAngle <= 180.0)
                    {
                        RIGHTEDGE.y = RIGHTEDGE.y + ypp;
                        LEFTEDGE.y = LEFTEDGE.y - ypp;
                        RIGHTEDGE.x = RIGHTEDGE.x - xpp;
                        LEFTEDGE.x = LEFTEDGE.x + xpp;
                    }
                    else if (GantryAngle > 180.0 & GantryAngle <= 270.0)
                    {
                        RIGHTEDGE.y = RIGHTEDGE.y - ypp;
                        LEFTEDGE.y = LEFTEDGE.y + ypp;
                        RIGHTEDGE.x = RIGHTEDGE.x - xpp;
                        LEFTEDGE.x = LEFTEDGE.x + xpp;
                    }

                    // System.Windows.Forms.MessageBox.Show("Trigger 5");
                    // GANTRY EDGE POINTS COORDINATE SYSTEM TRANSFORMATION FOR COUCH ANGLE
                    if (CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
                    {

                        ANGLE = 360.0 - CouchEndAngle;

                        Backxtrans = (BACKEDGE.x * Math.Cos(((-ANGLE * Math.PI) / 180.0))) + (BACKEDGE.z * Math.Sin(((-ANGLE * Math.PI) / 180.0)));
                        Backztrans = (-BACKEDGE.x * Math.Sin(((-ANGLE * Math.PI) / 180.0))) + (BACKEDGE.z * Math.Cos(((-ANGLE * Math.PI) / 180.0)));

                        Frontxtrans = (FRONTEDGE.x * Math.Cos(((-ANGLE * Math.PI) / 180.0))) + (FRONTEDGE.z * Math.Sin(((-ANGLE * Math.PI) / 180.0)));
                        Frontztrans = (-FRONTEDGE.x * Math.Sin(((-ANGLE * Math.PI) / 180.0))) + (FRONTEDGE.z * Math.Cos(((-ANGLE * Math.PI) / 180.0)));

                        Leftxtrans = (LEFTEDGE.x * Math.Cos(((-ANGLE * Math.PI) / 180.0))) + (LEFTEDGE.z * Math.Sin(((-ANGLE * Math.PI) / 180.0)));
                        Leftztrans = (-LEFTEDGE.x * Math.Sin(((-ANGLE * Math.PI) / 180.0))) + (LEFTEDGE.z * Math.Cos(((-ANGLE * Math.PI) / 180.0)));

                        Rightxtrans = (RIGHTEDGE.x * Math.Cos(((-ANGLE * Math.PI) / 180.0))) + (RIGHTEDGE.z * Math.Sin(((-ANGLE * Math.PI) / 180.0)));
                        Rightztrans = (-RIGHTEDGE.x * Math.Sin(((-ANGLE * Math.PI) / 180.0))) + (RIGHTEDGE.z * Math.Cos(((-ANGLE * Math.PI) / 180.0)));

                    }
                    else if (CouchEndAngle >= 0.0 & CouchEndAngle <= 90.0)
                    {

                        ANGLE = CouchEndAngle;

                        Backxtrans = (BACKEDGE.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (BACKEDGE.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                        Backztrans = (-BACKEDGE.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (BACKEDGE.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                        Frontxtrans = (FRONTEDGE.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (FRONTEDGE.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                        Frontztrans = (-FRONTEDGE.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (FRONTEDGE.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                        Leftxtrans = (LEFTEDGE.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (LEFTEDGE.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                        Leftztrans = (-LEFTEDGE.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (LEFTEDGE.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                        Rightxtrans = (RIGHTEDGE.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (RIGHTEDGE.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                        Rightztrans = (-RIGHTEDGE.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (RIGHTEDGE.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                    }

                    if (CouchEndAngle >= 0.0 & CouchEndAngle <= 90.0)
                    {
                        if (gantryXISOshift == "NEG")   // gantry on left side
                        {
                            BACKEDGE.x = Backxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            BACKEDGE.z = Backztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (ISO.x + ISO.z));

                            FRONTEDGE.x = Frontxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            FRONTEDGE.z = Frontztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (ISO.x + ISO.z));

                            LEFTEDGE.x = Leftxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            LEFTEDGE.z = Leftztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (ISO.x + ISO.z));

                            RIGHTEDGE.x = Rightxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            RIGHTEDGE.z = Rightztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (ISO.x + ISO.z));
                        }
                        else if (gantryXISOshift == "POS")  // gantry on right side
                        {
                            BACKEDGE.x = Backxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            BACKEDGE.z = Backztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.x + ISO.z));

                            FRONTEDGE.x = Frontxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            FRONTEDGE.z = Frontztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.x + ISO.z));

                            LEFTEDGE.x = Leftxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            LEFTEDGE.z = Leftztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.x + ISO.z));

                            RIGHTEDGE.x = Rightxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            RIGHTEDGE.z = Rightztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.x + ISO.z));
                        }
                    }
                    else if (CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
                    {
                        if (gantryXISOshift == "NEG")   // gantry on left side
                        {
                            BACKEDGE.x = Backxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            BACKEDGE.z = Backztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (ISO.x + ISO.z));

                            FRONTEDGE.x = Frontxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            FRONTEDGE.z = Frontztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (ISO.x + ISO.z));

                            LEFTEDGE.x = Leftxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            LEFTEDGE.z = Leftztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (ISO.x + ISO.z));

                            RIGHTEDGE.x = Rightxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            RIGHTEDGE.z = Rightztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (ISO.x + ISO.z));
                        }
                        else if (gantryXISOshift == "POS")  // gantry on right side
                        {
                            BACKEDGE.x = Backxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            BACKEDGE.z = Backztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.x + ISO.z));

                            FRONTEDGE.x = Frontxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            FRONTEDGE.z = Frontztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.x + ISO.z));

                            LEFTEDGE.x = Leftxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            LEFTEDGE.z = Leftztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.x + ISO.z));

                            RIGHTEDGE.x = Rightxtrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.z + ISO.x));
                            RIGHTEDGE.z = Rightztrans + (Math.Sin(((ANGLE * Math.PI) / 180.0)) * (-ISO.x + ISO.z));
                        }
                    }

                    // MessageBox.Show("backedge after transform is: (" + BACKEDGE.x + " ," + BACKEDGE.y + " ," + BACKEDGE.z + ")");
                    // MessageBox.Show("frontedge after transform is: (" + FRONTEDGE.x + " ," + FRONTEDGE.y + " ," + FRONTEDGE.z + ")");
                    // MessageBox.Show("leftedge after transform is: (" + LEFTEDGE.x + " ," + LEFTEDGE.y + " ," + LEFTEDGE.z + ")");
                    // MessageBox.Show("rightedge after transform is: (" + RIGHTEDGE.x + " ," + RIGHTEDGE.y + " ," + RIGHTEDGE.z + ")");

                    Vector3d Ri = new Vector3d(RIGHTEDGE.x, RIGHTEDGE.y, RIGHTEDGE.z);  //0           5     9    13
                    Vector3d Le = new Vector3d(LEFTEDGE.x, LEFTEDGE.y, LEFTEDGE.z);      //1          6     10   14
                    Vector3d Ba = new Vector3d(BACKEDGE.x, BACKEDGE.y, BACKEDGE.z);      //2          7     11   15
                    Vector3d Fr = new Vector3d(FRONTEDGE.x, FRONTEDGE.y, FRONTEDGE.z);    //3         8     12   16
                    Vector3d Ce = new Vector3d(gantrycenter.x, gantrycenter.y, gantrycenter.z);   //4
                                                                                                  //System.Windows.Forms.MessageBox.Show("Trigger 6");

                    // MessageBox.Show("Trig");

                    List<Index3i> Grangle = new List<Index3i>();
                    Index3i shangle = new Index3i(3, 4, 0);
                    Grangle.Add(shangle);

                    shangle = new Index3i(4, 2, 0);
                    Grangle.Add(shangle);

                    shangle = new Index3i(4, 1, 2);
                    Grangle.Add(shangle);

                    shangle = new Index3i(1, 3, 4);
                    Grangle.Add(shangle);

                    DMesh3 GANTRY = new DMesh3(MeshComponents.VertexNormals);

                    GANTRY.AppendVertex(new NewVertexInfo(Ri));
                    GANTRY.AppendVertex(new NewVertexInfo(Le));
                    GANTRY.AppendVertex(new NewVertexInfo(Ba));
                    GANTRY.AppendVertex(new NewVertexInfo(Fr));
                    GANTRY.AppendVertex(new NewVertexInfo(Ce));

                    foreach (Index3i tri in Grangle)
                    {
                        GANTRY.AppendTriangle(tri);
                    }

                    Vector3d gantrynormal = GANTRY.GetTriNormal(0);

                    TrivialDiscGenerator makegantryhead = new TrivialDiscGenerator();
                    makegantryhead.Radius = 382.5f;
                    makegantryhead.StartAngleDeg = 0.0f;
                    makegantryhead.EndAngleDeg = 360.0f;
                    makegantryhead.Slices = 72;
                    makegantryhead.Generate();
                    DMesh3 diskgantry = makegantryhead.MakeDMesh();
                    MeshTransforms.Translate(diskgantry, Ce);

                    // IOWriteResult result40 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\diskGantryi" + beam.Id + point.Index + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);

                    Vector3d diskgantrynormal = diskgantry.GetTriNormal(0);
                    double gantrydotprod = Vector3d.Dot(diskgantrynormal.Normalized, gantrynormal.Normalized);
                    double anglebetweengantrynormals = Math.Acos(gantrydotprod);     // in radians
                                                                                     // MessageBox.Show("angle between: " + anglebetweengantrynormals);

                    // this changes with couch rotation. (0,0,1) at couch zero degrees. implement coordinate system transformation. 
                    Vector3d zaxisgd = new Vector3d(0, 0, 1);
                    double zaxisgd_xp = 0.0;
                    double zaxisgd_zp = 0.0;

                    if (CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
                    {
                        ANGLE = 360.0 - CouchEndAngle;

                        // counterclockwise rotation
                        zaxisgd_xp = (zaxisgd.x * Math.Cos(((-ANGLE * Math.PI) / 180.0))) + (zaxisgd.z * Math.Sin(((-ANGLE * Math.PI) / 180.0)));
                        zaxisgd_zp = (zaxisgd.z * Math.Cos(((-ANGLE * Math.PI) / 180.0))) - (zaxisgd.x * Math.Sin(((-ANGLE * Math.PI) / 180.0)));
                    }
                    else if (CouchEndAngle >= 0.0 & CouchEndAngle <= 90.0)
                    {
                        ANGLE = CouchEndAngle;

                        //clockwise rotation
                        zaxisgd_xp = (zaxisgd.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (zaxisgd.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                        zaxisgd_zp = (zaxisgd.z * Math.Cos(((ANGLE * Math.PI) / 180.0))) - (zaxisgd.x * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                    }

                    zaxisgd.x = zaxisgd_xp;
                    zaxisgd.z = zaxisgd_zp;

                    if (GantryAngle > 180.0)
                    {
                        anglebetweengantrynormals = -1 * anglebetweengantrynormals;
                    }

                    Vector3d ISOV = new Vector3d(Ce.x, Ce.y, Ce.z);
                    Quaterniond diskrot = new Quaterniond(zaxisgd, (anglebetweengantrynormals * MathUtil.Rad2Deg));
                    MeshTransforms.Rotate(diskgantry, ISOV, diskrot);

                    
                    
                        if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                        {
                            Vector3d g3ISO = new Vector3d(ISO.x, ISO.y, ISO.z);
                            Vector3d ZaxisPatOrientRot = new Vector3d(0, 0, 1);
                            Quaterniond PatOrientRot = new Quaterniond(ZaxisPatOrientRot, 180.0);
                            MeshTransforms.Rotate(diskgantry, g3ISO, PatOrientRot);
                        }
                    

                    IOWriteResult result42 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\diskgantry" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);
                    tempbeam.Add(new WriteMesh(diskgantry));


                    // System.Windows.Forms.MessageBox.Show("Trigger 7");
                    // IOWriteResult result5 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SquareGantry\Gantry" + beam.Id + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GANTRY) }, WriteOptions.Defaults);

                    DMeshAABBTree3 diskgantryspatial = new DMeshAABBTree3(diskgantry);
                    diskgantryspatial.Build();
                    reportCang = 360.0 - CouchEndAngle;
                    if (reportCang == 360.0)
                    {
                        reportCang = 0.0;
                    }

                    // Generation of geometric model is no complete for both the patient structures and the diskgantry. The collision analysis is below
                    //------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------


                    /*
                        // couch surface check - Couch surface structure does not generate properly (its empty) because it is hollow, so ignoring it for now. Hopefully we can merge the interior and surface to get one couch structure that we can use in the future.
                        if (findCouchSurf == true)
                        {
                                snear_tids = diskgantryspatial.FindNearestTriangles(CouchSurfSpatial, null, out INTERSECTDIST);
                            System.Windows.Forms.MessageBox.Show("Tri IDs: " + snear_tids.a + ", " + snear_tids.b);
                            if (snear_tids.a == -1 || snear_tids.b == -1)
                                {
                                // out of range, do nothing
                                System.Windows.Forms.MessageBox.Show("Trigger 3");
                                }
                                else
                                {
                                System.Windows.Forms.MessageBox.Show("Trigger 3.1");
                                STriDist = MeshQueries.TrianglesDistance(diskgantry, snear_tids.a, PCouchsurf, snear_tids.b);
                                System.Windows.Forms.MessageBox.Show("Trigger 3.2");
                                ZABSDIST = ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest);
                                System.Windows.Forms.MessageBox.Show("Trigger 4");
                                if (ZABSDIST <= 50.0)
                                    {
                                    //System.Windows.Forms.MessageBox.Show("PATBOX collision");
                                    System.Windows.Forms.MessageBox.Show("Trigger 5");
                                    if (MRGPATBOX == null)
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Surface", contiguous = false });
                                            MRGPATBOX = GantryAngle;
                                            lastcontigreal = false;
                                        }
                                        else if ((MRGPATBOX >= GantryAngle - 15.0) & (MRGPATBOX <= GantryAngle + 15.0))
                                        {
                                            // contiguous collisions, do not report
                                            collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round((double)MRGPATBOX, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Surface", contiguous = true });
                                            MRGPATBOX = GantryAngle;
                                            lastcontigreal = true;
                                            // System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                        }
                                        else
                                        {
                                            if (lastcontigreal == true)
                                            {
                                                // System.Windows.Forms.MessageBox.Show("Last contig fire");
                                                collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Surface", contiguous = false, lastcontig = true });
                                                MRGPATBOX = GantryAngle;
                                                lastcontigreal = false;
                                            }
                                            else
                                            {
                                                collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Surface", contiguous = false });
                                                MRGPATBOX = GantryAngle;
                                                lastcontigreal = false;
                                            }
                                        }
                                    }
                                    else if (lastcontigreal == true)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Surface", contiguous = false, lastcontig = true });
                                        lastcontigreal = false;
                                    }
                                }
                            }
                        */

                    //lock (ProgOutput)
                    //{
                    //    ProgOutput.AppendText(Environment.NewLine);
                    //    ProgOutput.AppendText("Beam " + beam.Id + " collision reporting....");
                    //}


                    // System.Windows.Forms.MessageBox.Show("Trigger 6");
                    // couch interior collision check-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                    bool couchex = beam.couchexists;
                    bool boardex = beam.breastboardexists;

                        if (couchex == true)
                        {
                            snear_tids = diskgantryspatial.FindNearestTriangles(PCouchInteriorSpatial, null, out INTERSECTDIST);
                            //  System.Windows.Forms.MessageBox.Show("Trigger 7");
                            if (snear_tids.a == DMesh3.InvalidID || snear_tids.b == DMesh3.InvalidID)
                            {
                                // out of range, do nothing
                                // System.Windows.Forms.MessageBox.Show("Trigger 8");
                            }
                            else
                            {
                                STriDist = MeshQueries.TrianglesDistance(diskgantry, snear_tids.a, PCouchInterior, snear_tids.b);
                                ZABSDIST = ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest);
                                //  System.Windows.Forms.MessageBox.Show("Trigger 9");
                                if (ZABSDIST <= 50.0)
                                {
                                    //System.Windows.Forms.MessageBox.Show("Couch less than 50");

                                    if (MRGCINTERIOR == null)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Interior", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGCINTERIOR = GantryAngle;
                                        lastcontigrealCouch = true;
                                        //System.Windows.Forms.MessageBox.Show("First couch collision   Angle: " + GantryAngle);
                                    }
                                    else if ((MRGCINTERIOR >= GantryAngle - 15.0) & (MRGCINTERIOR <= GantryAngle + 15.0))
                                    {
                                        if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                        {
                                            // if at the end of the gantry angle list, 
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Interior", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Interior", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else
                                        {
                                            // contiguous collisions, do not report
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGCINTERIOR, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Interior", contiguous = true, lastcontig = false, endoflist = false });
                                            MRGCINTERIOR = GantryAngle;
                                            lastcontigrealCouch = true;
                                            //System.Windows.Forms.MessageBox.Show("Couch: Within 15 degrees of last collision, meaning it is contiguous, don't report   Angle: " + GantryAngle);
                                        }
                                    }
                                    else
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Interior", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGCINTERIOR = GantryAngle;
                                        lastcontigrealCouch = true;
                                        //System.Windows.Forms.MessageBox.Show("Couch: NOT Within 15 degrees of last collision, not contiguous, start of new collision area    Angle: " + GantryAngle);
                                    }
                                }
                                else if (ZABSDIST >= 50.0 & lastcontigrealCouch == true)
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Interior", contiguous = false, lastcontig = true, endoflist = false });
                                    lastcontigrealCouch = false;
                                    //System.Windows.Forms.MessageBox.Show("Couch: greater than 50, but last angle was a collison, so end of collision area    Angle: " + GantryAngle);
                                }
                            }
                        }
                    

                    //  System.Windows.Forms.MessageBox.Show("Trigger 10");
                    //prone breast board collision check-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                    
                    
                        if (boardex == true)
                        {
                            snear_tids = diskgantryspatial.FindNearestTriangles(PProne_Brst_BoardSpatial, null, out INTERSECTDIST);
                            //  System.Windows.Forms.MessageBox.Show("Trigger 11");
                            if (snear_tids.a == DMesh3.InvalidID || snear_tids.b == DMesh3.InvalidID)
                            {
                                // out of range, do nothing
                                // System.Windows.Forms.MessageBox.Show("Trigger 12");
                            }
                            else
                            {
                                STriDist = MeshQueries.TrianglesDistance(diskgantry, snear_tids.a, PProne_Brst_Board, snear_tids.b);
                                ZABSDIST = ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest);
                                //  System.Windows.Forms.MessageBox.Show("Trigger 13");
                                if (ZABSDIST <= 50.0)
                                {
                                    //System.Windows.Forms.MessageBox.Show("PATBOX collision");

                                    if (MRGBBOARD == null)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Prone Breast Board", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGBBOARD = GantryAngle;
                                        lastcontigrealBoard = true;
                                    }
                                    else if ((MRGBBOARD >= GantryAngle - 15.0) & (MRGBBOARD <= GantryAngle + 15.0))
                                    {
                                        if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                        {
                                            // if at the end of the gantry angle list, 
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Prone Breast Board", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Prone Breast Board", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else
                                        {
                                            // contiguous collisions, do not report
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGBBOARD, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Prone Breast Board", contiguous = true, lastcontig = false, endoflist = false });
                                            MRGBBOARD = GantryAngle;
                                            lastcontigrealBoard = true;
                                            // System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                        }
                                    }
                                    else
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Prone Breast Board", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGBBOARD = GantryAngle;
                                        lastcontigrealBoard = true;
                                    }
                                }
                                else if (ZABSDIST >= 50.0 & lastcontigrealBoard == true)
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Prone Breast Board", contiguous = false, lastcontig = true, endoflist = false });
                                    lastcontigrealBoard = false;
                                }
                            }
                        }
                    

                    //  System.Windows.Forms.MessageBox.Show("Trigger 14");
                    //PATBOX collision check----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                    snear_tids = diskgantryspatial.FindNearestTriangles(spatial, null, out INTERSECTDIST);
                    //   System.Windows.Forms.MessageBox.Show("Trigger 15");
                    if (snear_tids.a == DMesh3.InvalidID || snear_tids.b == DMesh3.InvalidID)
                    {
                        // out of range, do nothing
                        //System.Windows.Forms.MessageBox.Show("PATBOX out of range");
                        // System.Windows.Forms.MessageBox.Show("Trigger 16");
                    }
                    else
                    {
                        STriDist = MeshQueries.TrianglesDistance(diskgantry, snear_tids.a, PATBOX, snear_tids.b);
                        ZABSDIST = ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest);
                        //  System.Windows.Forms.MessageBox.Show("Trigger 17");
                        if (ZABSDIST <= 50.0)
                        {
                            // System.Windows.Forms.MessageBox.Show("PATBOX collision");

                            if (MRGPATBOX == null)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = false, lastcontig = false, endoflist = false });
                                MRGPATBOX = GantryAngle;
                                lastcontigrealPATBOX = true;
                                //System.Windows.Forms.MessageBox.Show("First patbox collision   Angle: " + GantryAngle);
                                // System.Windows.Forms.MessageBox.Show("Trigger 18");
                            }
                            else if ((MRGPATBOX >= GantryAngle - 15.0) & (MRGPATBOX <= GantryAngle + 15.0))
                            {
                                if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                {
                                    // if at the end of the gantry angle list, 
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = false, lastcontig = false, endoflist = true });
                                    //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                }
                                else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = false, lastcontig = false, endoflist = true });
                                    //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                }
                                else
                                {
                                    // contiguous collisions, do not report
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGPATBOX, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = true, lastcontig = false, endoflist = false });
                                    MRGPATBOX = GantryAngle;
                                    lastcontigrealPATBOX = true;
                                    //System.Windows.Forms.MessageBox.Show("Patbox: Within 15 degrees of last collision, meaning it is contiguous, don't report   Angle: " + GantryAngle);
                                }
                                //  System.Windows.Forms.MessageBox.Show("Trigger 19");
                                //System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                            }
                            else
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = false, lastcontig = false, endoflist = false });
                                MRGPATBOX = GantryAngle;
                                lastcontigrealPATBOX = true;
                                // System.Windows.Forms.MessageBox.Show("Trigger 21");
                                //System.Windows.Forms.MessageBox.Show("Patbox: NOT Within 15 degrees of last collision, not contiguous, start of new collision area    Angle: " + GantryAngle);
                            }
                        }
                        else if (ZABSDIST >= 50.0 & lastcontigrealPATBOX == true)
                        {
                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = false, lastcontig = true, endoflist = false });
                            lastcontigrealPATBOX = false;
                            //System.Windows.Forms.MessageBox.Show("Patbox: greater than 50, but last angle was a collison, so end of collision area    Angle: " + GantryAngle);
                            // System.Windows.Forms.MessageBox.Show("Trigger 22");
                        }
                    }

                    //   System.Windows.Forms.MessageBox.Show("Trigger 23");

                    //  MessageBox.Show("Collision analysis done");

                }  // ends gantry angle loop

            }    // ends if counch angle start = couch angle end

            //MessageBox.Show("COUCH LOOP DONE    ");

            // this writes out the "composite" STL file of each beam. It is put on Therapy physics so everyone can access it. The GUI then uses this to diplay a picture of each beam. 
            
            
             IOWriteResult EVERY = StandardMeshWriter.WriteFile(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\" + beam.patientId + "_" + beam.courseId + "_" + beam.planId + "_" + "Beam_" + beam.beamId + ".stl", tempbeam, WriteOptions.Defaults);


            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Beam " + beam.beamId + " analysis complete.");

            return collist;

        } // end BeamCollisionAnalysis



        //=====================================================================================================================================================================================================================================



        // This method makes all of the patient-related structures. Most of it is dedicated to constructing PATBOX, the patient bounding box. BOXMAKER is called by CollisionCheck.
        // This is quite extensive because it involves manipulations to each corner of the box that are different depending on the body are of the CT scan. The indices defining the triangles and the vertex points are all manually condtructed and then put together at the end, so it takes up a lot of space.
        public static List<DMesh3> BOXMAKER(BEAM beam, DMesh3 PBodyContour, DMeshAABBTree3 PBodyContourSpatial, DMesh3 PCouchInterior, DMeshAABBTree3 PCouchInteriorSpatial, DMesh3 PATBOX, DMeshAABBTree3 spatial, DMesh3 PProne_Brst_Board, DMeshAABBTree3 PProne_Brst_BoardSpatial)
        {

            List<DMesh3> PatMeshList = new List<DMesh3>();

           // System.Windows.Forms.MessageBox.Show("Start BOXMAKER");

            beam.patientheight = beam.patientheight * 10.0;    //convert from cm to mm.   IT is in mm

            Vector3d PatOrientRotCenter = new Vector3d(0, 0, 0);
            Quaterniond PatOrientRot = new Quaterniond();
            Vector3d ZaxisPatOrientRot = new Vector3d(0, 0, 1);

            // makes mesh out of patient body contour
            //ProgOutput.AppendText(Environment.NewLine);
            //ProgOutput.AppendText("Building Body Contour mesh... ");

            List<Index3i> ptl = new List<Index3i>();
            Index3i pt = new Index3i();
            int ptmcount = 0;
            int rem = 0;

            foreach (int ptm in beam.Bodyindices)
            {
                ptmcount++;
                Math.DivRem(ptmcount, 3, out rem);

                if (rem == 2)
                {
                    pt.a = ptm;
                }
                else if (rem == 1)
                {
                    pt.b = ptm;
                }
                else if (rem == 0)
                {
                    pt.c = ptm;
                    ptl.Add(pt);
                }
            }

            PBodyContour = new DMesh3(MeshComponents.VertexNormals);
            for (int i = 0; i < beam.Bodyvects.Count; i++)
            {
                PBodyContour.AppendVertex(new NewVertexInfo(beam.Bodyvects[i]));
            }

            for (int i = 0; i < ptl.Count; i++)
            {
                PBodyContour.AppendTriangle(ptl[i]);
            }

            //if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
            //{
            //    PatOrientRotCenter = MeshMeasurements.Centroid(PBodyContour);
            //    PatOrientRot = new Quaterniond(ZaxisPatOrientRot, 180.0);
            //    MeshTransforms.Rotate(PBodyContour, PatOrientRotCenter, PatOrientRot);
            //}

            //System.Windows.Forms.MessageBox.Show("before PBODY stl write");
            IOWriteResult result24 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\PBODY.stl", new List<WriteMesh>() { new WriteMesh(PBodyContour) }, WriteOptions.Defaults);
            // System.Windows.Forms.MessageBox.Show("after PBODY stl write")

            // This is a list of STL files which is used to visually display each beam of a plan for which a collision occurs.

            //This is the Hierarchial AABB (Axis Aligned Bounding Box) that is made for use with the collion check analysis.
            PatMeshList.Add(PBodyContour);


            // The couch surface is not currently used because the contour in Eclipse is hollow and not a closed surface, so can't easily make a mesh out of it.
            //if (findCouchSurf == true)
            //{
            //    // -------------------------------------------------------------------- makes mesh out of Couch surface

            //    ProgOutput.AppendText(Environment.NewLine);
            //    ProgOutput.AppendText("Building Couch Surface mesh... ");

            //    List<Vector3d> cspvl = new List<Vector3d>();
            //    Vector3d cspv = new Vector3d();

            //    List<Index3i> csptl = new List<Index3i>();
            //    Index3i cspt = new Index3i();
            //    int csptmcount = 0;

            //    foreach (Point3D pm in CouchSurface.MeshGeometry.Positions)
            //    {
            //        cspv.x = pm.X;
            //        cspv.y = pm.Y;
            //        cspv.z = pm.Z;

            //        cspvl.Add(cspv);
            //    }

            //    foreach (Int32 ptm in CouchSurface.MeshGeometry.TriangleIndices)
            //    {
            //        csptmcount++;
            //        Math.DivRem(csptmcount, 3, out rem);

            //        if (rem == 2)
            //        {
            //            cspt.a = ptm;
            //        }
            //        else if (rem == 1)
            //        {
            //            cspt.b = ptm;
            //        }
            //        else if (rem == 0)
            //        {
            //            cspt.c = ptm;
            //            csptl.Add(cspt);
            //        }
            //    }

            //    PCouchsurf = new DMesh3(MeshComponents.VertexNormals);
            //    for (int i = 0; i < cspvl.Count; i++)
            //    {
            //        PCouchsurf.AppendVertex(new NewVertexInfo(cspvl[i]));
            //    }

            //    for (int i = 0; i < csptl.Count; i++)
            //    {
            //        PBodyContour.AppendTriangle(csptl[i]);
            //    }

            //    if (PATEINTORIENTATION == "HeadFirstProne" || PATEINTORIENTATION == "FeetFirstProne")
            //    {
            //        PatOrientRotCenter = MeshMeasurements.Centroid(PBodyContour);
            //        PatOrientRot = new Quaterniond(ZaxisPatOrientRot, 180.0);
            //        MeshTransforms.Rotate(PCouchsurf, PatOrientRotCenter, PatOrientRot);
            //    }

            //   // IOWriteResult result31 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\CouchSurface.stl", new List<WriteMesh>() { new WriteMesh(PCouchsurf) }, WriteOptions.Defaults);
            //    tempbeam.Add(new WriteMesh(PCouchsurf));

            //    CouchSurfSpatial = new DMeshAABBTree3(PCouchsurf);
            //    CouchSurfSpatial.Build();
            //}


            if (beam.couchexists == true)
            {
                // ------------------------------------------------------- makes mesh out of Couch interior
                //ProgOutput.AppendText(Environment.NewLine);
                //ProgOutput.AppendText("Building Couch interior mesh... ");

                List<Index3i> ciptl = new List<Index3i>();
                Index3i cipt = new Index3i();
                int ciptmcount = 0;

                foreach (int ptm in beam.CouchInteriorindices)
                {
                    ciptmcount++;
                    Math.DivRem(ciptmcount, 3, out rem);

                    if (rem == 2)
                    {
                        cipt.a = ptm;
                    }
                    else if (rem == 1)
                    {
                        cipt.b = ptm;
                    }
                    else if (rem == 0)
                    {
                        cipt.c = ptm;
                        ciptl.Add(cipt);
                    }
                }

                PCouchInterior = new DMesh3(MeshComponents.VertexNormals);
                for (int i = 0; i < beam.CouchInteriorvects.Count; i++)
                {
                    PCouchInterior.AppendVertex(new NewVertexInfo(beam.CouchInteriorvects[i]));
                }

                for (int i = 0; i < ciptl.Count; i++)
                {
                    PCouchInterior.AppendTriangle(ciptl[i]);
                }

                //if (PATIENTORIENTATION == "HeadFirstProne" || PATIENTORIENTATION == "FeetFirstProne")
                //{
                //    PatOrientRotCenter = MeshMeasurements.Centroid(PBodyContour);
                //    PatOrientRot = new Quaterniond(ZaxisPatOrientRot, 180.0);
                //    MeshTransforms.Rotate(PCouchInterior, PatOrientRotCenter, PatOrientRot);
                //}


                IOWriteResult result30 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\CouchInterior.stl", new List<WriteMesh>() { new WriteMesh(PCouchInterior) }, WriteOptions.Defaults);
     
                PatMeshList.Add(PCouchInterior);

            }

            if (beam.breastboardexists == true)
            {
                // ------------------------------------------------------- makes mesh out of Prone Breast Board
                //ProgOutput.AppendText(Environment.NewLine);
                //ProgOutput.AppendText("Building Prone Breast Board mesh... ");

                List<Index3i> bbptl = new List<Index3i>();
                Index3i bbpt = new Index3i();
                int bbptmcount = 0;

                foreach (int ptm in beam.BreastBoardindices)
                {
                    bbptmcount++;
                    Math.DivRem(bbptmcount, 3, out rem);

                    if (rem == 2)
                    {
                        bbpt.a = ptm;
                    }
                    else if (rem == 1)
                    {
                        bbpt.b = ptm;
                    }
                    else if (rem == 0)
                    {
                        bbpt.c = ptm;
                        bbptl.Add(bbpt);
                    }
                }

                PProne_Brst_Board = new DMesh3(MeshComponents.VertexNormals);
                for (int i = 0; i < beam.BreastBoardvects.Count; i++)
                {
                    PProne_Brst_Board.AppendVertex(new NewVertexInfo(beam.BreastBoardvects[i]));
                }

                for (int i = 0; i < bbptl.Count; i++)
                {
                    PProne_Brst_Board.AppendTriangle(bbptl[i]);
                }

                //if (PATIENTORIENTATION == "HeadFirstProne" || PATIENTORIENTATION == "FeetFirstProne")
                //{
                //    PatOrientRotCenter = MeshMeasurements.Centroid(PBodyContour);
                //    PatOrientRot = new Quaterniond(ZaxisPatOrientRot, 180.0);
                //    MeshTransforms.Rotate(PProne_Brst_Board, PatOrientRotCenter, PatOrientRot);
                //}

                IOWriteResult result36 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\ProneBreastBoard.stl", new List<WriteMesh>() { new WriteMesh(PProne_Brst_Board) }, WriteOptions.Defaults);
            
                PatMeshList.Add(PProne_Brst_Board);

            }

           // System.Windows.Forms.MessageBox.Show("Start of PATBOX construction");


            //PATBOX CONSTRUCTION ---------------------------------------------------------------------------------------------------------------------------------------------------------

            // MessageBox.Show("body structure center point is: (" + beam.Bodycenter.x + " ," + Body.CenterPoint.y + " ," + Body.CenterPoint.z + ")");

            //   VVector dicomvec = image.DicomToUser(Body.CenterPoint, plan);

            // MessageBox.Show("body structure center point is at USER: (" + dicomvec.x + " ," + dicomvec.y + " ," + dicomvec.z + ")");

            //  MessageBox.Show("body structure center point is at DICOM: (" + Body.CenterPoint.x + " ," + Body.CenterPoint.y + " ," + Body.CenterPoint.z + ")");

            // VVector dicomvec = image.DicomToUser(Body.CenterPoint, plan);

            //  MessageBox.Show("body structure center point is at USER: (" + dicomvec.x + " ," + dicomvec.y + " ," + dicomvec.z + ")");

            // MessageBox.Show("PATBOX origin (top Right Corner) is at DICOM: (" + BOX.X + " ," + BOX.Y + " ," + BOX.Z + ")");
            //  MessageBox.Show("PATBOX origin (top Right Corner) is at USER: (" + BOX.X + " ," + BOX.Y + " ," + BOX.Z + ")");

            //  MessageBox.Show("PATBOX SIZE is: (" + BOX.SizeX + " ," + BOX.SizeY + " ," + BOX.SizeZ + ")");

            //ProgOutput.AppendText(Environment.NewLine);
            //ProgOutput.AppendText("Building extended patient bounding box ...");

            // MessageBox.Show("ht is: " + ht);

            double LT = beam.BodyBoxZSize;
            // MessageBox.Show("LT is: " + LT);

            double headdownshift = beam.patientheight - LT;
            double thoraxupshift = (beam.patientheight - LT) * 0.24;
            double thoraxdownshift = (beam.patientheight - LT) * 0.78;
            double abdomenupshift = (beam.patientheight - LT) * 0.35;
            double abdomendownshift = (beam.patientheight - LT) * 0.7;
            double pelvisupshift = (beam.patientheight - LT) * 0.55;
            double pelvisdownshift = (beam.patientheight - LT) * 0.55;
            double legsupshift = (beam.patientheight - LT) * 0.75;
            double legsdownshift = (beam.patientheight - LT) * 0.31;

            // MessageBox.Show("headdownshift is: " + headdownshift);

            // find the 8 corners of the Rect3D, use them to make a separate mesh. use Body.CenterPoint as origin to maintain coordinate system
            double patbxshift = beam.BodyBoxXsize / 2.0;
            double patbyshift = beam.BodyBoxYSize / 2.0;
            double patbzshift = beam.BodyBoxZSize/ 2.0;

            //need box extension to cover entire patient !!!!!!!!!!!!!!!

            List<Vector3d> vertices = new List<Vector3d>();
            List<Index3i> triangles = new List<Index3i>();
            // each triangle is simply a struct of 3 ints which are indices referring to the vertices which make up that triangle
            // in other words, a triangle is a collection of 3 vertices, and it is just composed of indices referencing the vertices
            
            Vector3d vect = new Vector3d();

            Vector3d centerofforwardface = new Vector3d(beam.Bodycenter.x, beam.Bodycenter.y, beam.Bodycenter.z + patbzshift);
            Vector3d centerofdownwardface = new Vector3d(beam.Bodycenter.x, beam.Bodycenter.y, beam.Bodycenter.z - patbzshift);
            
            //  MessageBox.Show("Center of downward face (y) (before shift): " + centerofdownwardface.y);

            if (beam.bodylocation == "Head")
            {
                centerofdownwardface.z = centerofdownwardface.z - headdownshift;
                //  MessageBox.Show("Center of downward face (y) (after shift): " + centerofdownwardface.y);
            }
            else if (beam.bodylocation == "Thorax")
            {
                centerofforwardface.z = centerofforwardface.z + thoraxupshift;
                centerofdownwardface.z = centerofdownwardface.z - thoraxdownshift;
            }
            else if (beam.bodylocation == "Abdomen")
            {
                centerofforwardface.z = centerofforwardface.z + abdomenupshift;
                centerofdownwardface.z = centerofdownwardface.z - abdomendownshift;
            }
            else if (beam.bodylocation == "Pelvis")
            {
                centerofforwardface.z = centerofforwardface.z + pelvisupshift;
                centerofdownwardface.z = centerofdownwardface.z - pelvisdownshift;
            }
            else if (beam.bodylocation == "Legs")
            {
                centerofforwardface.z = centerofforwardface.z + legsupshift;
                centerofdownwardface.z = centerofdownwardface.z - legsdownshift;
            }

            // MessageBox.Show("Center of Forward Face (plane above head): (" + centerofforwardface.x + " ," + centerofforwardface.y + " ," + centerofforwardface.z + ")");

            Vector3d centeroftopface = new Vector3d(beam.Bodycenter.x, beam.Bodycenter.y - patbyshift, beam.Bodycenter.z);
            Vector3d centerofbottomface = new Vector3d(beam.Bodycenter.x, beam.Bodycenter.y + patbyshift, beam.Bodycenter.z);
            Vector3d centerofrightface = new Vector3d(beam.Bodycenter.x + patbxshift, beam.Bodycenter.y, beam.Bodycenter.z);
            Vector3d centerofleftface = new Vector3d(beam.Bodycenter.x - patbxshift, beam.Bodycenter.y, beam.Bodycenter.z);

            vertices.Add(centeroftopface);         // 0
            vertices.Add(centerofbottomface);      //  1
            vertices.Add(centerofrightface);        // 2
            vertices.Add(centerofleftface);         // ...
            vertices.Add(centerofforwardface);
            vertices.Add(centerofdownwardface);

            // top upper right corner
            vect.x = beam.Bodycenter.x + patbxshift;
            if (beam.bodylocation == "Head")
            {
                vect.z = beam.Bodycenter.z + patbzshift;
            }
            else if (beam.bodylocation == "Thorax")
            {
                vect.z = beam.Bodycenter.z + patbzshift + thoraxupshift;
            }
            else if (beam.bodylocation == "Abdomen")
            {
                vect.z = beam.Bodycenter.z + patbzshift + abdomenupshift;
            }
            else if (beam.bodylocation == "Pelvis")
            {
                vect.z = beam.Bodycenter.z + patbzshift + pelvisupshift;
            }
            else if (beam.bodylocation == "Legs")
            {
                vect.z = beam.Bodycenter.z + patbzshift + legsupshift;
            }

            vect.y = beam.Bodycenter.y - patbyshift;
            vertices.Add(vect);
            Vector3d tur = new Vector3d(vect.x, vect.y, vect.z);

            // top upper left corner
            vect.x = beam.Bodycenter.x - patbxshift;
            if (beam.bodylocation == "Head")
            {
                vect.z = beam.Bodycenter.z + patbzshift;
            }
            else if (beam.bodylocation == "Thorax")
            {
                vect.z = beam.Bodycenter.z + patbzshift + thoraxupshift;
            }
            else if (beam.bodylocation == "Abdomen")
            {
                vect.z = beam.Bodycenter.z + patbzshift + abdomenupshift;
            }
            else if (beam.bodylocation == "Pelvis")
            {
                vect.z = beam.Bodycenter.z + patbzshift + pelvisupshift;
            }
            else if (beam.bodylocation == "Legs")
            {
                vect.z = beam.Bodycenter.z + patbzshift + legsupshift;
            }

            vect.y = beam.Bodycenter.y - patbyshift;
            vertices.Add(vect);
            Vector3d tul = new Vector3d(vect.x, vect.y, vect.z);

            // top bottom right corner
            vect.x = beam.Bodycenter.x + patbxshift;
            if (beam.bodylocation == "Head")
            {
                vect.z = beam.Bodycenter.z - patbzshift - headdownshift;
            }
            else if (beam.bodylocation == "Thorax")
            {
                vect.z = beam.Bodycenter.z - patbzshift - thoraxdownshift;
            }
            else if (beam.bodylocation == "Abdomen")
            {
                vect.z = beam.Bodycenter.z - patbzshift - abdomendownshift;
            }
            else if (beam.bodylocation == "Pelvis")
            {
                vect.z = beam.Bodycenter.z - patbzshift - pelvisdownshift;
            }
            else if (beam.bodylocation == "Legs")
            {
                vect.z = beam.Bodycenter.z - patbzshift - legsdownshift;
            }
            vect.y = beam.Bodycenter.y - patbyshift;
            vertices.Add(vect);
            Vector3d tbr = new Vector3d(vect.x, vect.y, vect.z);

            // top bottom left corner
            vect.x = beam.Bodycenter.x - patbxshift;
            if (beam.bodylocation == "Head")
            {
                vect.z = beam.Bodycenter.z - patbzshift - headdownshift;
            }
            else if (beam.bodylocation == "Thorax")
            {
                vect.z = beam.Bodycenter.z - patbzshift - thoraxdownshift;
            }
            else if (beam.bodylocation == "Abdomen")
            {
                vect.z = beam.Bodycenter.z - patbzshift - abdomendownshift;
            }
            else if (beam.bodylocation == "Pelvis")
            {
                vect.z = beam.Bodycenter.z - patbzshift - pelvisdownshift;
            }
            else if (beam.bodylocation == "Legs")
            {
                vect.z = beam.Bodycenter.z - patbzshift - legsdownshift;
            }

            vect.y = beam.Bodycenter.y - patbyshift;
            vertices.Add(vect);
            Vector3d tbl = new Vector3d(vect.x, vect.y, vect.z);

            // lower upper right corner
            vect.x = beam.Bodycenter.x + patbxshift;
            if (beam.bodylocation == "Head")
            {
                vect.z = beam.Bodycenter.z + patbzshift;
            }
            else if (beam.bodylocation == "Thorax")
            {
                vect.z = beam.Bodycenter.z + patbzshift + thoraxupshift;
            }
            else if (beam.bodylocation == "Abdomen")
            {
                vect.z = beam.Bodycenter.z + patbzshift + abdomenupshift;
            }
            else if (beam.bodylocation == "Pelvis")
            {
                vect.z = beam.Bodycenter.z + patbzshift + pelvisupshift;
            }
            else if (beam.bodylocation == "Legs")
            {
                vect.z = beam.Bodycenter.z + patbzshift + legsupshift;
            }

            vect.y = beam.Bodycenter.y + patbyshift;
            vertices.Add(vect);
            Vector3d lur = new Vector3d(vect.x, vect.y, vect.z);

            // lower upper left corner
            vect.x = beam.Bodycenter.x - patbxshift;
            if (beam.bodylocation == "Head")
            {
                vect.z = beam.Bodycenter.z + patbzshift;
            }
            else if (beam.bodylocation == "Thorax")
            {
                vect.z = beam.Bodycenter.z + patbzshift + thoraxupshift;
            }
            else if (beam.bodylocation == "Abdomen")
            {
                vect.z = beam.Bodycenter.z + patbzshift + abdomenupshift;
            }
            else if (beam.bodylocation == "Pelvis")
            {
                vect.z = beam.Bodycenter.z + patbzshift + pelvisupshift;
            }
            else if (beam.bodylocation == "Legs")
            {
                vect.z = beam.Bodycenter.z + patbzshift + legsupshift;
            }

            vect.y = beam.Bodycenter.y + patbyshift;
            vertices.Add(vect);
            Vector3d lul = new Vector3d(vect.x, vect.y, vect.z);

            // lower bottom right corner
            vect.x = beam.Bodycenter.x + patbxshift;
            if (beam.bodylocation == "Head")
            {
                vect.z = beam.Bodycenter.z - patbzshift - headdownshift;
            }
            else if (beam.bodylocation == "Thorax")
            {
                vect.z = beam.Bodycenter.z - patbzshift - thoraxdownshift;
            }
            else if (beam.bodylocation == "Abdomen")
            {
                vect.z = beam.Bodycenter.z - patbzshift - abdomendownshift;
            }
            else if (beam.bodylocation == "Pelvis")
            {
                vect.z = beam.Bodycenter.z - patbzshift - pelvisdownshift;
            }
            else if (beam.bodylocation == "Legs")
            {
                vect.z = beam.Bodycenter.z - patbzshift - legsdownshift;
            }

            vect.y = beam.Bodycenter.y + patbyshift;
            vertices.Add(vect);
            Vector3d lbr = new Vector3d(vect.x, vect.y, vect.z);

            // lower bottom leftcorner
            vect.x = beam.Bodycenter.x - patbxshift;
            if (beam.bodylocation == "Head")
            {
                vect.z = beam.Bodycenter.z - patbzshift - headdownshift;
            }
            else if (beam.bodylocation == "Thorax")
            {
                vect.z = beam.Bodycenter.z - patbzshift - thoraxdownshift;
            }
            else if (beam.bodylocation == "Abdomen")
            {
                vect.z = beam.Bodycenter.z - patbzshift - abdomendownshift;
            }
            else if (beam.bodylocation == "Pelvis")
            {
                vect.z = beam.Bodycenter.z - patbzshift - pelvisdownshift;
            }
            else if (beam.bodylocation == "Legs")
            {
                vect.z = beam.Bodycenter.z - patbzshift - legsdownshift;
            }

            vect.y = beam.Bodycenter.y + patbyshift;
            vertices.Add(vect);
            Vector3d lbl = new Vector3d(vect.x, vect.y, vect.z);

            //top face
            Index3i rangle = new Index3i(0, 7, 6);
            triangles.Add(rangle);

            rangle = new Index3i(0, 6, 8);
            triangles.Add(rangle);

            rangle = new Index3i(0, 8, 9);
            triangles.Add(rangle);

            rangle = new Index3i(0, 9, 7);
            triangles.Add(rangle);

            //bottom face
            rangle = new Index3i(1, 11, 10);
            triangles.Add(rangle);

            rangle = new Index3i(1, 10, 12);
            triangles.Add(rangle);

            rangle = new Index3i(1, 12, 13);
            triangles.Add(rangle);

            rangle = new Index3i(1, 13, 11);
            triangles.Add(rangle);

            // right side
            rangle = new Index3i(2, 6, 10);
            triangles.Add(rangle);

            rangle = new Index3i(2, 10, 12);
            triangles.Add(rangle);

            rangle = new Index3i(2, 12, 8);
            triangles.Add(rangle);

            rangle = new Index3i(2, 8, 6);
            triangles.Add(rangle);

            // left side
            rangle = new Index3i(3, 7, 11);
            triangles.Add(rangle);

            rangle = new Index3i(3, 11, 13);
            triangles.Add(rangle);

            rangle = new Index3i(3, 13, 9);
            triangles.Add(rangle);

            rangle = new Index3i(3, 9, 7);
            triangles.Add(rangle);

            // forward face
            rangle = new Index3i(4, 7, 6);
            triangles.Add(rangle);

            rangle = new Index3i(4, 6, 10);
            triangles.Add(rangle);

            rangle = new Index3i(4, 10, 11);
            triangles.Add(rangle);

            rangle = new Index3i(4, 11, 7);
            triangles.Add(rangle);

            //downward face
            rangle = new Index3i(5, 9, 8);
            triangles.Add(rangle);

            rangle = new Index3i(5, 8, 12);
            triangles.Add(rangle);

            rangle = new Index3i(5, 12, 13);
            triangles.Add(rangle);

            rangle = new Index3i(5, 13, 9);
            triangles.Add(rangle);

            int cbht = 0;
            // everything made to make a mesh out of the body structure bounding box (with extensions of the box added to represent the patient's entire body)

            PATBOX = new DMesh3(MeshComponents.VertexNormals);
            for (int i = 0; i < vertices.Count; i++)
            {
                PATBOX.AppendVertex(new NewVertexInfo(vertices[i]));
            }

            foreach (Index3i tri in triangles)
            {
                PATBOX.AppendTriangle(tri);
                cbht++;
            }

            // MessageBox.Show("number of triangles: " + cbht);

            DMesh3 PATBOXCOPY = PATBOX;

            //use the remesher in Gradientspace to add triangles/vertices to mesh based off of the simple box mesh. This increases the resolution of the box to make it useful for the collision analysis.
            // Remeshing settings aren't perfect, but they are fairly dialed in
            // ProgOutput.AppendText(Environment.NewLine);
            // ProgOutput.AppendText("Remeshing patient bounding box... ");

            Remesher R = new Remesher(PATBOX);
            MeshConstraintUtil.PreserveBoundaryLoops(R);
            R.PreventNormalFlips = true;
            R.SetTargetEdgeLength(30.0);
            R.SmoothSpeedT = 0.5;
            R.SetProjectionTarget(MeshProjectionTarget.Auto(PATBOXCOPY));
            R.ProjectionMode = Remesher.TargetProjectionMode.Inline;

            for (int k = 0; k < 8; k++)
            {
                R.BasicRemeshPass();
            }

            //if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
            //{
            //    PatOrientRotCenter = MeshMeasurements.Centroid(PATBOX);
            //    PatOrientRot = new Quaterniond(ZaxisPatOrientRot, 180.0);
            //    MeshTransforms.Rotate(PATBOX, PatOrientRotCenter, PatOrientRot);
            //}

            IOWriteResult result3 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\PATBOX.stl", new List<WriteMesh>() { new WriteMesh(PATBOX) }, WriteOptions.Defaults);
            //tempbeam.Add(new WriteMesh(PATBOX));

            PatMeshList.Add(PATBOX);

            //System.Windows.Forms.MessageBox.Show("End of BOXMAKER");

            return PatMeshList;
        }


        //=================================================================================================================================================================================================================================================================================================


        public static List<CollisionAlert> CollisionCheckExecute(List<BEAM> BEAMLIST, TextBox ProgOutput)
        {
            // declaration space for outputs and things used between boxmaker and collision check

            List<List<CollisionAlert>> collist = new List<List<CollisionAlert>>();

            // already gone through the structures. only has structures in the list that exist

            //System.Windows.Forms.MessageBox.Show(plan.TreatmentOrientation.ToString());

            // No correction needed for Feet First vs. Head First, but the 180 degree flip is needed for both Prone orientations vs. Supine (Program built off of HFS).
            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Looping through beams...");


            //BEAM beam in plan.Beams)
            // start of beam loop-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

            try
            {
                Parallel.ForEach(BEAMLIST, beam =>
                {
                    List<CollisionAlert> tempcol = BeamCollisionAnalysis(beam, ProgOutput);
                    collist.Add(tempcol);
                }); // ends beam loop
            }
            catch(Exception e)
            {
                System.Windows.Forms.MessageBox.Show(e.ToString());
            }
            //MessageBox.Show("Beam loop done");

            //var collist = await Task.WhenAll(collistTasks);

            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("All beams complete.");

            List<CollisionAlert> Totalcollist = new List<CollisionAlert>();

            foreach (List<CollisionAlert> L in collist)
            {
                foreach (CollisionAlert al in L)
                {
                    Totalcollist.Add(al);
                }
            }

            return Totalcollist;
        } // ends collision check

    }

    //========================================================================================================================================================================================================

    //This class is used by the CollionCheck method to output the collision information to the GUI. Some of these variables are old and no longer used.
    public class CollisionAlert
    {
        public string beam { get; set; }

        public bool endoflist { get; set; }

        public double gantryangle { get; set; }

        public double couchangle { get; set; }

        public double distance { get; set; }

        public string type { get; set; }   // this specifies the mesh being checked against diskgantry for collision (PATBOX, body contour, breast board, etc.)

        public bool contiguous { get; set; }

        public bool lastcontig { get; set; }

        public string rotationdirection { get; set; }

        public CollisionAlert()
        {
            lastcontig = false;
            endoflist = false;
        }
    }

    public class PLAN
    {
        public string planId { get; set; }

        public string patientId { get; set; }

        public string courseId { get; set; }

        public string patientsex { get; set; }

        public string StructureSetId { get; set; }

        public string TreatmentOrientation { get; set; }

        public MeshGeometry3D Body { get; set; }

        public Vector3d Bodycenter { get; set; }

        public MeshGeometry3D CouchInterior { get; set; }

        public Vector3d CouchInteriorcenter { get; set; }

        public MeshGeometry3D ProneBreastBoard { get; set; }

        public Vector3d BreastBoardcenter { get; set; }

        public bool couchexists { get; set; }
        public bool breastboardexists { get; set; }

        public List<BEAM> Beams { get; set; }

        public List<Vector3d> Bodyvects { get; set; }

        public List<int> Bodyindices { get; set; }

        public double BodyBoxXsize { get; set; }

        public double BodyBoxYSize { get; set; }

        public double BodyBoxZSize { get; set; }
            
        public List<Vector3d> CouchInteriorvects { get; set; }

        public List<int> CouchInteriorindices { get; set; }

        public List<Vector3d> BreastBoardvects { get; set; }

        public List<int> BreastBoardindices { get; set; }
    }

    public class BEAM
    {

        public string gantrydirection { get; set; }

        public bool setupfield { get; set; }

        public Vector3d Isocenter { get; set; }

        public string MLCtype { get; set; }

        public string beamId { get; set; }

        public double arclength { get; set; }

        public List<CONTROLPOINT> ControlPoints { get; set; }

        //image stuff put in beam
        public Vector3d imageuserorigin { get; set; }


        public Vector3d imageorigin { get; set; }


        // plan stuff put in beam

        public string planId { get; set; }

        public string patientId { get; set; }

        public string courseId { get; set; }

        public string patientsex { get; set; }

        public string StructureSetId { get; set; }

        public string TreatmentOrientation { get; set; }

        public Vector3d Bodycenter { get; set; }

        public List<Vector3d> Bodyvects { get; set; }

        public List<int> Bodyindices { get; set; }

        public double BodyBoxXsize { get; set; }

        public double BodyBoxYSize { get; set; }

        public double BodyBoxZSize { get; set; }

        public Vector3d CouchInteriorcenter { get; set; }

        public List<Vector3d> CouchInteriorvects { get; set; }

        public List<int> CouchInteriorindices { get; set; }

        public Vector3d BreastBoardcenter { get; set; }

        public List<Vector3d> BreastBoardvects { get; set; }

        public List<int> BreastBoardindices { get; set; }

        public bool couchexists { get; set; }

        public bool breastboardexists { get; set; }

        public string bodylocation { get; set; }

        public double patientheight { get; set; }

    }

    public class CONTROLPOINT
    {
        public double Gantryangle { get; set; }
        public double Couchangle { get; set; }
    }


    public class IMAGE
    {
        public Vector3d imageuserorigin { get; set; }

        public Vector3d imageorigin { get; set; }

    }

}
