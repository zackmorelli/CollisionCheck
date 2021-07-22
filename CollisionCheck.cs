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
using MessageBox = System.Windows.Forms.MessageBox;

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

        public static async Task<StreamReader> GantryRetrievalAsync(string PATid, string COURSEid, string PLANid, BEAM beam)
        {
            StreamReader GantryAngleRetrieveOutput = await Task.Run(() => ActualRetrieval(PATid, COURSEid, PLANid, beam));
            return GantryAngleRetrieveOutput;
        }

        public static StreamReader ActualRetrieval(string PATid, string COURSEid, string PLANid, BEAM beam)
        {
            string planID = "\"" + PLANid + "\"";                                                                                                                                                                                             
            ProcessStartInfo processinfo = new ProcessStartInfo(@"\\wvvrnimbp01ss\va_data$\filedata\ProgramData\Vision\Stand-alone Programs\CollisionCheck_InfoRetrieval\CollisionCheck_InfoRetrieval.exe", PATid + " ," + COURSEid + " ," + planID + " ," + beam.beamId);        // path name of the Collision retrieval program
            
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

        public static List<CollisionAlert> BeamCollisionAnalysis(BEAM beam, TextBox ProgOutput, string Acc, bool FAST)
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

            DMesh3 PATCYLINDER = new DMesh3();
            DMesh3 FASTPATCYLINDER = new DMesh3();
            DMeshAABBTree3 PATCYLSPATIAL = new DMeshAABBTree3(PATCYLINDER);
            DMeshAABBTree3 FASTPATCYLSPATIAL = new DMeshAABBTree3(FASTPATCYLINDER);

            DMeshAABBTree3 ScaledCouchspatial = new DMeshAABBTree3(PCouchInterior);
            DMeshAABBTree3 ScaledBreastBoardspatial = new DMeshAABBTree3(PProne_Brst_Board);

            bool FASTPATBOX = false;
            bool FASTCOUCH = false;
            bool FASTBREASTBOARD = false;

            bool AccFASTPATBOX = false;
            bool AccFASTCOUCH = false;
            bool AccFASTBREASTBOARD = false;

            // ANGLES ARE IN DEGREES
            List<double> GAngleList = new List<double>();
            int gantrylistCNT = 0;
            double CouchStartAngle = beam.ControlPoints[0].Couchangle;
            double CouchEndAngle = beam.ControlPoints[beam.ControlPoints.Count - 1].Couchangle;       // count - 1 is the end becuase the index starts at 0

            List<WriteMesh> tempbeam = new List<WriteMesh>();

            //these are important variables used in the collision anlaysis. They are used to convey information between iterations of the gantry angle loop, which is why they are declared here.
            // Because the program does only either a collision analysis with the SRS Cone disk or the Gantry disk, there is only one set of these variables, which are used in the collision analysis of the applicable disk.
            // if a collision analysis were to be performed on both the SRS Cone disk AND the Gantry disk then another unique set of these variables would have to be made in order to preserve that unique information between gantry angles.
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

                //MessageBox.Show("Beam: " + beam.beamId + " ESAPI couch Angle: " + CouchEndAngle);


                Vector3d ISO = beam.Isocenter;
                Vector3d UserOrigin = beam.imageuserorigin;
                Vector3d Origin = beam.imageorigin;
                
                //MessageBox.Show("Image UserOrigin: (" + UserOrigin.x + " ," + UserOrigin.y + " ," + UserOrigin.z + ")");
                //MessageBox.Show("Image Origin: (" + Origin.x + " ," + Origin.y + " ," + Origin.z + ")");

                bool cp = false;  //this represents whether the program is using MLC control points or not

               // ProgOutput.AppendText(Environment.NewLine);
               // ProgOutput.AppendText("Beam " + beam.Id + " calling BOXMAKER....");

                List<DMesh3> PatMeshList = new List<DMesh3>();

                // calls the boxmaker method which makes all the patient realated 3D meshes.

                try
                {
                    // System.Windows.Forms.MessageBox.Show("Before Patbox");
                    PatMeshList = BOXMAKER(beam, PBodyContour, PCouchInterior, PATCYLINDER, FASTPATCYLINDER, PProne_Brst_Board, FAST);
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

                    //if (FAST == true)
                    //{
                    //    DMesh3 ScaledCouch = new DMesh3(PCouchInterior);
                    //    MeshTransforms.Scale(ScaledCouch, 1.06);   //scale couch by 6%
                    //    ScaledCouchspatial = new DMeshAABBTree3(ScaledCouch);
                    //    ScaledCouchspatial.Build();
                    //    IOWriteResult result8 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\ScaledCouch.stl", new List<WriteMesh>() { new WriteMesh(ScaledCouch) }, WriteOptions.Defaults);
                    //}

                    PProne_Brst_Board = PatMeshList[2];
                    tempbeam.Add(new WriteMesh(PProne_Brst_Board));
                    PProne_Brst_BoardSpatial = new DMeshAABBTree3(PProne_Brst_Board);
                    PProne_Brst_BoardSpatial.Build();

                    //if (FAST == true)
                    //{
                    //    DMesh3 ScaledBreastBoard = new DMesh3(PProne_Brst_Board);
                    //    MeshTransforms.Scale(ScaledBreastBoard, 1.06);   //scale breastboard by 6%
                    //    ScaledBreastBoardspatial = new DMeshAABBTree3(ScaledBreastBoard);
                    //    ScaledBreastBoardspatial.Build();
                    //    IOWriteResult result33 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\ScaledBREASTBOARD.stl", new List<WriteMesh>() { new WriteMesh(ScaledBreastBoard) }, WriteOptions.Defaults);
                    //}

                    PATCYLINDER = PatMeshList[3];
                    PATCYLSPATIAL = new DMeshAABBTree3(PATCYLINDER);
                    PATCYLSPATIAL.Build();

                    if(FAST == true)
                    {
                        FASTPATCYLINDER = PatMeshList[4];
                        FASTPATCYLSPATIAL = new DMeshAABBTree3(FASTPATCYLINDER);
                        FASTPATCYLSPATIAL.Build();
                    }
                }
                else if(beam.couchexists == false & beam.breastboardexists == false)
                {
                    PBodyContour = PatMeshList[0];
                    tempbeam.Add(new WriteMesh(PBodyContour));
                    PBodyContourSpatial = new DMeshAABBTree3(PBodyContour);
                    PBodyContourSpatial.Build();

                    PATCYLINDER = PatMeshList[1];
                    PATCYLSPATIAL = new DMeshAABBTree3(PATCYLINDER);
                    PATCYLSPATIAL.Build();

                    if (FAST == true)
                    {
                        FASTPATCYLINDER = PatMeshList[2];
                        FASTPATCYLSPATIAL = new DMeshAABBTree3(FASTPATCYLINDER);
                        FASTPATCYLSPATIAL.Build();
                    }
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

                    //if(FAST == true)
                    //{
                    //    DMesh3 ScaledCouch = new DMesh3(PCouchInterior);
                    //    MeshTransforms.Scale(ScaledCouch, 1.06);   //scale couch by 6%
                    //    ScaledCouchspatial = new DMeshAABBTree3(ScaledCouch);
                    //    ScaledCouchspatial.Build();
                    //    IOWriteResult result8 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\ScaledCouch.stl", new List<WriteMesh>() { new WriteMesh(ScaledCouch) }, WriteOptions.Defaults);
                    //}

                    PATCYLINDER = PatMeshList[2];
                    PATCYLSPATIAL = new DMeshAABBTree3(PATCYLINDER);
                    PATCYLSPATIAL.Build();

                    if (FAST == true)
                    {
                        FASTPATCYLINDER = PatMeshList[3];
                        FASTPATCYLSPATIAL = new DMeshAABBTree3(FASTPATCYLINDER);
                        FASTPATCYLSPATIAL.Build();
                    }
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

                    //if(FAST == true)
                    //{
                    //    DMesh3 ScaledBreastBoard = new DMesh3(PProne_Brst_Board);
                    //    MeshTransforms.Scale(ScaledBreastBoard, 1.06);   //scale breast board by 6%
                    //    ScaledBreastBoardspatial = new DMeshAABBTree3(ScaledBreastBoard);
                    //    ScaledBreastBoardspatial.Build();
                    //    IOWriteResult result33 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\ScaledBREASTBOARD.stl", new List<WriteMesh>() { new WriteMesh(ScaledBreastBoard) }, WriteOptions.Defaults);
                    //}

                    PATCYLINDER = PatMeshList[2];
                    PATCYLSPATIAL = new DMeshAABBTree3(PATCYLINDER);
                    PATCYLSPATIAL.Build();

                    if (FAST == true)
                    {
                        FASTPATCYLINDER = PatMeshList[3];
                        FASTPATCYLSPATIAL = new DMeshAABBTree3(FASTPATCYLINDER);
                        FASTPATCYLSPATIAL.Build();
                    }
                }

                //System.Windows.Forms.MessageBox.Show("Isocenter point is at: (" + ISO.x + " ," + ISO.y + " ," + ISO.z + ")");
                // MessageBox.Show("Image origin is at: (" + Origin.x + " ," + Origin.y + " ," + Origin.z + ")");
                // System.Windows.Forms.MessageBox.Show("User Origin at: (" + UserOrigin.x + " ," + UserOrigin.y + " ," + UserOrigin.z + ")");
                //IOWriteResult result32 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\ScaledPATBOX.stl", new List<WriteMesh>() { new WriteMesh(ScaledPATBOX) }, WriteOptions.Defaults);

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

                List<double> ArtificialGantryAngles = new List<double>();
                ArtificialGantryAngles.Add(3.0);
                ArtificialGantryAngles.Add(6.0);
                ArtificialGantryAngles.Add(9.0);

                List<Vector3d> CEList = new List<Vector3d>();

                //================================================================================================================================================================================================
                // This calculate the gantry center point for three gantry angles so we can use them to define a plane whose normal will be used in the diskgantry rotation.
                // It does this artificially for 3 angles in case the beam is not an arc.
                foreach (double ROTANG in ArtificialGantryAngles)
                {
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
                    myX = 1000 * Math.Cos((((ROTANG - 90.0) * Math.PI) / 180.0)) + ISO.x;    // - 90 degrees is because the polar coordinate system has 0 degrees on the right side
                    myY = 1000 * Math.Sin((((-ROTANG - 90.0) * Math.PI) / 180.0)) + ISO.y;   // need negative because y axis is inverted
                                                                                                  // THIS WORKS!
                    Vector3d mySOURCErot = new Vector3d(myX, myY, myZ);

                    // MessageBox.Show(beam.beamId + " mySOURCE: (" + mySOURCE.x + " ," + mySOURCE.y + " ," + mySOURCE.z + ")");

                    // MessageBox.Show("SOURCE : (" + convSOURCE.x + " ," + convSOURCE.y + " ," + convSOURCE.z + ")");

                    // this determines the position of gantrycenterpoint (from mySOURCE) at all gantry angles at couch 0 degrees

                    if (ROTANG > 270.0)
                    {
                        Gangle = 90.0 - (ROTANG - 270.0);

                        gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                        gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                    }
                    else if (ROTANG >= 0.0 & ROTANG <= 90.0)
                    {
                        Gangle = ROTANG;
                        gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                        gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                    }
                    else if (ROTANG > 90.0 & ROTANG <= 180.0)
                    {
                        Gangle = 90.0 - (ROTANG - 90.0);

                        gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                        gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                    }
                    else if (ROTANG > 180.0 & ROTANG <= 270.0)
                    {
                        //  MessageBox.Show("Trig 5");
                        Gangle = ROTANG - 180.0;

                        gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                        gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                    }

                    thetap = 90.0 - Gangle;

                    // MessageBox.Show("thetap is: " + thetap);

                    Vector3d gantrycenterrot = mySOURCErot;    // this will represent the center of the gantry head's surface once the transforms below are performed
                                                               // For couch zero degrees

                    if (ROTANG >= 270.0 | (ROTANG >= 0.0 & ROTANG <= 90.0))
                    {
                        gantrycenterrot.y = gantrycenterrot.y + gfyp;
                        //  MessageBox.Show("gf.y is: " + gf.y);
                    }
                    else if (ROTANG < 270.0 & ROTANG > 90.0)
                    {
                        gantrycenterrot.y = gantrycenterrot.y - gfyp;
                    }

                    // this just determines if the original xshift to gf is positive or negative
                    if (ROTANG >= 0.0 & ROTANG <= 180.0)
                    {
                        gantrycenterrot.x = gantrycenterrot.x - gfxp;
                        gantryXISOshift = "POS";
                    }
                    else if (ROTANG > 180.0)
                    {
                        gantrycenterrot.x = gantrycenterrot.x + gfxp;
                        gantryXISOshift = "NEG";
                    }

                    Vector3d origgantrycenterrot = gantrycenterrot;

                    //System.Windows.Forms.MessageBox.Show("Trigger 4");
                    //  MessageBox.Show(beam.beamId + " gantrycenter before transform is: (" + gantrycenter.x + " ," + gantrycenter.y + " ," + gantrycenter.z + ")");
                    //gantrycenter now represents the center point of the gantry for all gantry angles at 0 degrees couch angle
                    // a coordinate transformation for couch angle is performed next
                    //once the gantry centerpoint and the patient are in the same coordinate system, the edges of the gantry are found from there.

                    // COORDINATE SYSTEM TRANSFORMATION FOR COUCH ANGLE
                    if (CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
                    {
                        // this is really for 0 to 90 couch angle
                        //  MessageBox.Show("REAL couch ANGLE :  " + realangle + "  ");
                        //  MessageBox.Show("TRIGGER ROT 0 to 270");
                        ANGLE = 360.0 - CouchEndAngle;

                        if (UserOrigin != ISO)
                        {
                            // The Iso is not at the user origin for this plan. this makes things complicated, instead of simply rotating the coordinate system
                            // we must translate the gantry to the user origin, rotate it, and then translate back
                            //THIS WORKS TESTED 5/24/2021
                            Vector3d gantrycenterISOtranslate = gantrycenterrot - ISO;

                            gantrycenterxtrans = (gantrycenterISOtranslate.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (-gantrycenterISOtranslate.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                            gantrycenterztrans = (gantrycenterISOtranslate.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (gantrycenterISOtranslate.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                            gantrycenterISOtranslate.x = gantrycenterxtrans;
                            gantrycenterISOtranslate.z = gantrycenterztrans;

                            gantrycenterrot = gantrycenterISOtranslate + ISO;
                        }
                        else
                        {
                            // rotates counterclockwise to oppose clockwise rotation of patient
                            gantrycenterxtrans = (gantrycenterrot.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (-gantrycenterrot.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                            gantrycenterztrans = (gantrycenterrot.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (gantrycenterrot.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                            gantrycenterrot.x = gantrycenterxtrans;
                            gantrycenterrot.z = gantrycenterztrans;
                        }

                        // MessageBox.Show(beam.beamId + "xtrans, ztrans: (" + gantrycenterxtrans + " ," + gantrycenterztrans + ")");
                    }
                    else if (CouchEndAngle >= 0.0 & CouchEndAngle <= 90.0)
                    {
                        //this is really 0 to 270 couch angle
                        ANGLE = CouchEndAngle;

                        if (UserOrigin != ISO)
                        {
                            // The Iso is not at the user origin for this plan. this makes things complicated, instead of simply rotating the coordinate system
                            // we must translate the gantry to the user origin, rotate it, and then translate back
                            Vector3d gantrycenterISOtranslate = gantrycenterrot - ISO;

                            gantrycenterxtrans = (gantrycenterISOtranslate.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (gantrycenterISOtranslate.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                            gantrycenterztrans = (-gantrycenterISOtranslate.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (gantrycenterISOtranslate.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                            gantrycenterISOtranslate.x = gantrycenterxtrans;
                            gantrycenterISOtranslate.z = gantrycenterztrans;

                            gantrycenterrot = gantrycenterISOtranslate + ISO;
                        }
                        else
                        {
                            // rotates counterclockwise to oppose clockwise rotation of patient
                            gantrycenterxtrans = (gantrycenterrot.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (gantrycenterrot.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                            gantrycenterztrans = (-gantrycenterrot.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (gantrycenterrot.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                            gantrycenterrot.x = gantrycenterxtrans;
                            gantrycenterrot.z = gantrycenterztrans;
                        }
                    }

                    Vector3d Cerot = new Vector3d(gantrycenterrot.x, gantrycenterrot.y, gantrycenterrot.z);   //4
                    CEList.Add(Cerot);
                }

                //calculates the normal vector of the plane defined by the gantry center points (basically the couch angle) for 3 gantry angles.
                Vector3d E1 = CEList[CEList.Count - 2] - CEList[CEList.Count - 1];
                Vector3d E2 = CEList[CEList.Count - 3] - CEList[CEList.Count - 1];
                Vector3d zaxisgd = E1.UnitCross(E2);

                //========================================================================================================================================================================================
                // figures out gantry angles, depends on beam/MLC type

                //System.Windows.Forms.MessageBox.Show("MLCType: " + beam.MLCtype);

                //System.Windows.Forms.MessageBox.Show("Arc Length: " + beam.ArcLength);
                if (beam.arclength == 0)
                {
                    // So this beam is not an arc, meaning we only need to evaluate it at one gantry angle, its start angle, which we know for every beam, whether it is static MLC or not
                    // No real point in running on Fast mode if we are only evaluating one gantry angle, so we force change it here
                    FAST = false;
                    ProgOutput.AppendText(Environment.NewLine);
                    ProgOutput.AppendText("Non-arc beam. Retrieving gantry angle from first MLC control point ... ");
                    GAngleList.Add(beam.ControlPoints.First().Gantryangle);
                }
                else
                {
                    // This is an arc beam, where we want to evaluate the position of the gantry every 3-4 degrees
                    // if this is a dynamic MLC plan, where the arc has control points throughout, this is easy
                    // we just obtain the gantry angle at each controlpoint throughout the arc
                    // However, controlpoints in an arc plan are only 2 degrees apart in some plans
                    // This can have a big effect on slowing down the program, so in order to exert more control
                    // we break up all the beams into 4 degree increments ourselves, even if they have controlpoints

                    // Back when I had access to the Aria database, I would query the database to get the gantry start and stop values and then interpolate gantry angles in between
                    // this was a good exercise for me in learning SQL at the time, and all the code to do it is still here
                    // but now, we just use the arc length, arc direction and the gantry start to break the arc up into 4 degree increments
                    //arc length is in degrees

                    double gantrystartangle = beam.ControlPoints.First().Gantryangle;
                    int numgantryangles = 0;
                    double currentangle = 0.0;
                    ProgOutput.AppendText(Environment.NewLine);
                    ProgOutput.AppendText("Arc beam. Building list of gantry angles based off gantry angle start, arc length, and arc direction ... ");

                    GAngleList.Add(gantrystartangle);

                    if (beam.gantrydirection == "Clockwise")
                    {
                        numgantryangles = Convert.ToInt16(Math.Round((beam.arclength / 4), 0, MidpointRounding.AwayFromZero));
                        for (int i = 1; i <= numgantryangles; i++)
                        {
                            currentangle = gantrystartangle + (i * 4);
                            if (currentangle > 360)
                            {
                                currentangle = currentangle - 360;
                            }
                            GAngleList.Add(currentangle);

                            //ProgOutput.AppendText(Environment.NewLine);
                            //ProgOutput.AppendText("Added angle: " + currentangle);
                        }
                    }
                    else if (beam.gantrydirection == "CounterClockwise")
                    {
                        numgantryangles = Convert.ToInt16(Math.Round((beam.arclength / 4), 0, MidpointRounding.AwayFromZero));
                        for (int i = 1; i <= numgantryangles; i++)
                        {
                            currentangle = gantrystartangle - (i * 4);
                            if (currentangle < 0)
                            {
                                currentangle = 360 - Math.Abs(currentangle);
                            }
                            GAngleList.Add(currentangle);

                            //ProgOutput.AppendText(Environment.NewLine);
                            //ProgOutput.AppendText("Added angle: " + currentangle);
                        }
                    }
                }

                //This is old code from when I queried the Aria DB

                //if (beam.MLCtype == "Static")
                //{
                //    double GantryStartAngle = 500.0;   //initially set to 500 so that they are clearly outside of the appropriate domain, not a real angle
                //    double GantryEndAngle = 500.0;
                //    string ArcDirection = null;

                //    //ProgOutput.AppendText(Environment.NewLine);
                //    //ProgOutput.AppendText("This is a static MLC beam with no control points. Attempting to get Gantry information for this beam from the ARIA database (this might take a minute)... ");

                //    //System.Windows.Forms.MessageBox.Show("This is a static MLC beam with no control points. The program will get the gantry information it needs for this beam from the ARIA database.\nA blank terminal window will appear while it does this. A dialogue box will appear that will tell you that the program is busy because it is waiting for the other program to query the database.\nYou will have to click on 'switch to' several times until it is done. The GUI window will reappear when the program is finished.");

                //    Task<StreamReader> TGantryAngleRetrieveOutput = GantryRetrievalAsync(beam.patientId, beam.courseId, beam.planId, beam);
                    
                //    StreamReader GantryAngleRetrieveOutput = TGantryAngleRetrieveOutput.Result;

                //    // This is high-level .NET multithreading using the Task Parallel Library included in .NET 4.0

                //    //ProgOutput.AppendText(Environment.NewLine);
                //    //ProgOutput.AppendText("Aria retrieval complete! Building list of gantry angles...");

                //    ArcDirection = GantryAngleRetrieveOutput.ReadLine();
                //    GantryStartAngle = Convert.ToDouble(GantryAngleRetrieveOutput.ReadLine());
                //    GantryEndAngle = Convert.ToDouble(GantryAngleRetrieveOutput.ReadLine());

                //    //System.Windows.Forms.MessageBox.Show("Arc direction: " + ArcDirection);
                //    //System.Windows.Forms.MessageBox.Show("Gantry Start Angle: " + GantryStartAngle);
                //    //System.Windows.Forms.MessageBox.Show("Gantry End Angle: " + GantryEndAngle);

                //    if (ArcDirection == "NONE")
                //    {
                //        GAngleList.Add(GantryStartAngle);
                //        // No real point in running on Fast mode if it is a static gantry plan
                //        FAST = false;
                //    }
                //    else if (ArcDirection == "CW")
                //    {
                //        double tempangle = GantryStartAngle;
                //        GAngleList.Add(GantryStartAngle);

                //        while (tempangle != GantryEndAngle)
                //        {
                //            tempangle++;

                //            if (tempangle == 360)
                //            {
                //                tempangle = 0;
                //            }

                //            GAngleList.Add(tempangle);
                //        }
                //    }
                //    else if (ArcDirection == "CC")
                //    {
                //        double tempangle = GantryStartAngle;
                //        GAngleList.Add(GantryStartAngle);

                //        while (tempangle != GantryEndAngle)
                //        {
                //            tempangle--;

                //            if (tempangle == -1)
                //            {
                //                tempangle = 359;
                //            }

                //            GAngleList.Add(tempangle);
                //        }
                //    }
                //}
 

                //========================================================================================================================================================================================================================================================================
                //==================================================================================================================================================================================================================================================================================
                //The foreach loop below starts the real guts of the program. the program iterates through each gantry angle that we have made for the beam and conducts its collision analysis


                foreach (double GantryAngle in GAngleList)
                {
                    gantrylistCNT++;

                    // Diskgantry calculation/creation for each gantry angle. This accounts for all couch angles and HFS vs. HFP patient orientation  
                    // System.Windows.Forms.MessageBox.Show("Gantry ANGLE :  " + GantryAngle + "  ");
                    //  MessageBox.Show("couch ANGLE :  " + CouchEndAngle + "  ");
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

                    Vector3d mySOURCE = new Vector3d();
                    if (Acc != null && Acc != "SRS Cone")
                    {
                        //Non-isocentric beam
                        mySOURCE = beam.APISource;
                    }
                    else
                    {
                        //Isocentric beam
                        mySOURCE = new Vector3d(myX, myY, myZ);
                        //MessageBox.Show("Isocentric beam");
                    }

                    //MessageBox.Show("SOURCE : (" + convSOURCE.x + " ," + convSOURCE.y + " ," + convSOURCE.z + ")");

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

                    //System.Windows.Forms.MessageBox.Show("Trigger 4");
                    //  MessageBox.Show(beam.beamId + " gantrycenter before transform is: (" + gantrycenter.x + " ," + gantrycenter.y + " ," + gantrycenter.z + ")");
                    //gantrycenter now represents the center point of the gantry for all gantry angles at 0 degrees couch angle
                    // a coordinate transformation for couch angle is performed next
                    //once the gantry centerpoint and the patient are in the same coordinate system, the edges of the gantry are found from there.
                    bool anglebetweennormalsneg = false;
                    if (GantryAngle >= 0 & GantryAngle <= 180)
                    {
                        anglebetweennormalsneg = true;
                    }

                    if (Acc != null && Acc != "SRS Cone")
                    {
                        //Non-isocentric beam
                        goto GantryConstruction;
                    }

                    //=========================================================================================================================================================================================================================
                    //===================COORDINATE SYSTEM TRANSFORMATION FOR COUCH ANGLE

                    //Regular Couch angle transformation for Isocentric beams
                    if (CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
                    {
                        // this is really for 0 to 90 couch angle
                        //  MessageBox.Show("REAL couch ANGLE :  " + realangle + "  ");
                        //  MessageBox.Show("TRIGGER ROT 0 to 270");
                        ANGLE = 360.0 - CouchEndAngle;

                        if(UserOrigin != ISO)
                        {
                            // The Iso is not at the user origin for this plan. this makes things complicated, instead of simply rotating the coordinate system
                            // we must translate the gantry to the user origin, rotate it, and then translate back
                            //THIS WORKS TESTED 5/24/2021
                            Vector3d gantrycenterISOtranslate = gantrycenter - ISO;

                            gantrycenterxtrans = (gantrycenterISOtranslate.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (-gantrycenterISOtranslate.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                            gantrycenterztrans = (gantrycenterISOtranslate.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (gantrycenterISOtranslate.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                            gantrycenterISOtranslate.x = gantrycenterxtrans;
                            gantrycenterISOtranslate.z = gantrycenterztrans;

                            gantrycenter = gantrycenterISOtranslate + ISO;
                        }
                        else
                        {
                            // rotates counterclockwise to oppose clockwise rotation of patient
                            gantrycenterxtrans = (gantrycenter.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (-gantrycenter.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                            gantrycenterztrans = (gantrycenter.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (gantrycenter.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                            gantrycenter.x = gantrycenterxtrans;
                            gantrycenter.z = gantrycenterztrans;
                        }

                        // MessageBox.Show(beam.beamId + "xtrans, ztrans: (" + gantrycenterxtrans + " ," + gantrycenterztrans + ")");
                    }
                    else if (CouchEndAngle >= 0.0 & CouchEndAngle <= 90.0)
                    {
                        //this is really 0 to 270 couch angle
                         ANGLE = CouchEndAngle;

                        if (UserOrigin != ISO)
                        {
                            // The Iso is not at the user origin for this plan. this makes things complicated, instead of simply rotating the coordinate system
                            // we must translate the gantry to the user origin, rotate it, and then translate back

                            Vector3d gantrycenterISOtranslate = gantrycenter - ISO;

                            gantrycenterxtrans = (gantrycenterISOtranslate.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (gantrycenterISOtranslate.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                            gantrycenterztrans = (-gantrycenterISOtranslate.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (gantrycenterISOtranslate.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                            gantrycenterISOtranslate.x = gantrycenterxtrans;
                            gantrycenterISOtranslate.z = gantrycenterztrans;

                            gantrycenter = gantrycenterISOtranslate + ISO;
                        }
                        else
                        {
                            // rotates counterclockwise to oppose clockwise rotation of patient
                            gantrycenterxtrans = (gantrycenter.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) + (gantrycenter.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                            gantrycenterztrans = (-gantrycenter.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (gantrycenter.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                            gantrycenter.x = gantrycenterxtrans;
                            gantrycenter.z = gantrycenterztrans;
                        }
                    }

                    // that is just the first part of the coordinate system. we now need to account for shifts to the respective coordinate axes for the ISO position. This is dependent on couch and gantry rotation.

                    if (CouchEndAngle >= 0.0 & CouchEndAngle <= 90.0)
                    {
                        //360 to 270
                        ANGLE = CouchEndAngle;

                        if (GantryAngle > 90.0 & GantryAngle < 270.0)
                        {
                            xpp = -xpp;
                            // anglebetweennormalsneg = true;
                        }

                        BACKEDGE.x = gantrycenter.x + (384.0 * (Math.Sin(((ANGLE * Math.PI) / 180.0))));
                        BACKEDGE.z = gantrycenter.z + (384.0 * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                        FRONTEDGE.x = gantrycenter.x - (384.0 * (Math.Sin(((ANGLE * Math.PI) / 180.0))));
                        FRONTEDGE.z = gantrycenter.z - (384.0 * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                        RIGHTEDGE.x = gantrycenter.x + (xpp * (Math.Cos(((ANGLE * Math.PI) / 180.0))));
                        RIGHTEDGE.z = gantrycenter.z - (xpp * Math.Sin(((ANGLE * Math.PI) / 180.0)));

                        LEFTEDGE.x = gantrycenter.x - (xpp * (Math.Cos(((ANGLE * Math.PI) / 180.0))));
                        LEFTEDGE.z = gantrycenter.z + (xpp * Math.Sin(((ANGLE * Math.PI) / 180.0)));

                    }
                    else if (CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
                    {
                        //0 to 90
                        ANGLE = 360.0 - CouchEndAngle;

                        if (GantryAngle > 90.0 & GantryAngle < 270.0)
                        {
                            xpp = -xpp;
                            // anglebetweennormalsneg = true;
                        }

                        BACKEDGE.x = gantrycenter.x - (384.0 * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                        BACKEDGE.z = gantrycenter.z + (384.0 * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                        //MessageBox.Show("ANGLE: " + ANGLE);
                        //MessageBox.Show("xpp: " + xpp);
                        //MessageBox.Show("Gantrycenter z: " + gantrycenter.z)

                        FRONTEDGE.x = gantrycenter.x + (384.0 * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                        FRONTEDGE.z = gantrycenter.z - (384.0 * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                        RIGHTEDGE.x = gantrycenter.x + (xpp * Math.Cos(((ANGLE * Math.PI) / 180.0)));
                        RIGHTEDGE.z = gantrycenter.z + (xpp * Math.Sin(((ANGLE * Math.PI) / 180.0)));

                        LEFTEDGE.x = gantrycenter.x - (xpp * Math.Cos(((ANGLE * Math.PI) / 180.0)));
                        LEFTEDGE.z = gantrycenter.z - (xpp * Math.Sin(((ANGLE * Math.PI) / 180.0)));


                        //  MessageBox.Show("Trigger gantry angle between 0 and 90 gantry points calculation");
                        //if (GantryAngle >= 90 & GantryAngle <= 180)
                        ////{
                        ////    //fine, do nothing
                        ////}
                        ////else if (GantryAngle >= 0 & GantryAngle <= 90)
                        ////{
                        ////    double ly = LEFTEDGE.y;
                        ////    double ry = RIGHTEDGE.y;
                        ////    //flip y values of right edge and left edge
                        ////    RIGHTEDGE.y = ly;
                        ////    LEFTEDGE.y = ry;
                        ////}
                    }
                    

                    // MessageBox.Show("gantrycenter after transform is: (" + gantrycenter.x + " ," + gantrycenter.y + " ," + gantrycenter.z + ")");
                    // MessageBox.Show("backedge after transform is: (" + BACKEDGE.x + " ," + BACKEDGE.y + " ," + BACKEDGE.z + ")");
                    // MessageBox.Show("frontedge after transform is: (" + FRONTEDGE.x + " ," + FRONTEDGE.y + " ," + FRONTEDGE.z + ")");
                    // MessageBox.Show("leftedge after transform is: (" + LEFTEDGE.x + " ," + LEFTEDGE.y + " ," + LEFTEDGE.z + ")");
                    // MessageBox.Show("rightedge after transform is: (" + RIGHTEDGE.x + " ," + RIGHTEDGE.y + " ," + RIGHTEDGE.z + ")");

                    //Done with Couch angle coordinate sytem correction
                    //gantry construction starts below

                    GantryConstruction:

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
                    //IOWriteResult result40 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\" + beam.beamId + "Gantrysquare" + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GANTRY) }, WriteOptions.Defaults);
                    //IOWriteResult result40 = StandardMeshWriter.WriteFile(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\" + beam.beamId + "Gantrysquare" + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GANTRY) }, WriteOptions.Defaults);

                    //@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files


                    Quaterniond PatOrientRot = new Quaterniond(zaxisgd, 180.0);
                    Vector3d g3ISO = new Vector3d(ISO.x, ISO.y, ISO.z);
                    Vector3d gantrynormal = GANTRY.GetTriNormal(0);

                    TrivialDiscGenerator makegantryhead = new TrivialDiscGenerator();
                    makegantryhead.Radius = 382.5f;
                    makegantryhead.StartAngleDeg = 0.0f;
                    makegantryhead.EndAngleDeg = 360.0f;
                    makegantryhead.Slices = 72;
                    makegantryhead.Generate();

                    DMesh3 GantryAcc = new DMesh3();
                    DMesh3 diskgantry = new DMesh3();
                    DMeshAABBTree3 GantryAccspatial = new DMeshAABBTree3(GantryAcc);
                    //MessageBox.Show("Beam: " + beam.beamId + " disknorm: (" + diskgantrynormalV2.x + ", " + diskgantrynormalV2.y + ", " + diskgantrynormalV2.z + ")");

                    //==================================================================================================================================================================================================================================================================================
                    // Here the program diverges for a while between non-isocentric and isocentric setups. A number of variables declared above are used so that the program can continue execution 
                    // in the collision analysis section with shared variables, regardless of whether the plan is isocentric or not.

                    // Nonisocentric Setups
                    if (Acc != null && Acc != "SRS Cone")
                    {
                        //MessageBox.Show(" NON Isocentric diskgantry creation");
                        //DMesh3 ASource = makegantryhead.MakeDMesh();
                        // DMesh3 ISOex = makegantryhead.MakeDMesh();
                        //System.Windows.Forms.MessageBox.Show("APISource : (" + beam.APISource.x + ", " + beam.APISource.y + ", " + beam.APISource.z + ")");
                        //MeshTransforms.Translate(ASource, beam.APISource);
                        //MeshTransforms.Translate(ISOex, ISO);
                        //IOWriteResult result89 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\" + beam.beamId + "Source" + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(ASource) }, WriteOptions.Defaults);
                        //IOWriteResult result311 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\" + beam.beamId + "ISO" + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(ISOex) }, WriteOptions.Defaults);

                        double DirA = beam.APISource.x - ISO.x;
                        double DirB = beam.APISource.y - ISO.y;
                        double DirC = beam.APISource.z - ISO.z;
                        double DirA1 = 0.415 * DirA;
                        double DirB1 = 0.415 * DirB;
                        double DirC1 = 0.415 * DirC;
                        double rx = ISO.x + DirA1;
                        double ry = ISO.y + DirB1;
                        double rz = ISO.z + DirC1;
                        Vector3d R = new Vector3d(rx, ry, rz);

                        MeshTransforms.Translate(GANTRY, R);
                        //IOWriteResult result639 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\" + beam.beamId + "GantrysquareNONISO" + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GANTRY) }, WriteOptions.Defaults);

                        gantrynormal = GANTRY.GetTriNormal(0);
                        diskgantry = makegantryhead.MakeDMesh();
                        MeshTransforms.Translate(diskgantry, R);
                        //IOWriteResult result312 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\nonisodiskgantryraw" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);

                        //MessageBox.Show("After Correction: Distance between ISO and R: " + ISO.Distance(R));
                        //MessageBox.Show("After Correction: Distance between R and SOURCE: " + R.Distance(beam.APISource));

                        Vector3d diskgantrynormal = diskgantry.GetTriNormal(0);
                        double gantrydotprod = Vector3d.Dot(diskgantrynormal.Normalized, gantrynormal.Normalized);
                        double anglebetweengantrynormals = Math.Acos(gantrydotprod);     // in radians
                        //MessageBox.Show("Angle between gantry normals: " + anglebetweengantrynormals);                                                                            // MessageBox.Show("angle between: " + anglebetweengantrynormals);
                        if (anglebetweennormalsneg == true)
                        {
                            anglebetweengantrynormals = -1 * anglebetweengantrynormals;
                        }

                        Vector3d ISOV = new Vector3d(R.x, R.y, R.z);
                        Quaterniond diskrot = new Quaterniond(zaxisgd, (anglebetweengantrynormals * MathUtil.Rad2Deg));
                        MeshTransforms.Rotate(diskgantry, ISOV, diskrot);

                        if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                        {
                            g3ISO = new Vector3d(ISO.x, ISO.y, ISO.z);
                            PatOrientRot = new Quaterniond(zaxisgd, 180.0);
                            MeshTransforms.Rotate(diskgantry, g3ISO, PatOrientRot);
                        }

                        //IOWriteResult result821 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\nonisodiskgantryfinal" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);
                        tempbeam.Add(new WriteMesh(diskgantry));

                        // Variables used in Electron Cone generation
                        Vector3d diskgantrynormalV2 = diskgantry.GetTriNormal(0);
                        //MessageBox.Show("Beam: " + beam.beamId + " disknorm: (" + diskgantrynormalV2.x + ", " + diskgantrynormalV2.y + ", " + diskgantrynormalV2.z + ")");
                        Vector3d diskgantrynormalMAG;
                        Vector3d cc;
                        TrivialDiscGenerator makeSRSCone;
                        TrivialRectGenerator makeRect;
                        Vector3d AccNormal;
                        double Accdotprod;
                        double anglebetweenAccNormals;
                        Vector3d AccCenter;
                        Quaterniond AccRot;

                        // Various Electron cones generated below
                        if (Acc == "6x6 Electron Cone")
                        {
                            diskgantrynormalMAG = diskgantrynormalV2 * 406.0;

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                cc = R - diskgantrynormalMAG;
                            }
                            else
                            {
                                cc = R + diskgantrynormalMAG;
                            }
                            //MessageBox.Show("NM Length: " + NM.Length);
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E normal: (" + diskgantrynormalMAG.x + ", " + diskgantrynormalMAG.y + ", " + diskgantrynormalMAG.z + ")");
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E cc: (" + cc.x + ", " + cc.y + ", " + cc.z + ")");
                            //MessageBox.Show("Iso: (" + ISO.x + " ," + ISO.y + " ," + ISO.z + ")");
                            //MessageBox.Show("Distance between R and cc: " + R.Distance(cc));
                            //MessageBox.Show("Distance between ISO and cc: " + ISO.Distance(cc));

                            makeRect = new TrivialRectGenerator();
                            makeRect.Height = 151.5f;
                            makeRect.Width = 151.5f;
                            makeRect.Generate();
                            GantryAcc = makeRect.MakeDMesh();
                            //IOWriteResult result322 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6Eorig" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            MeshTransforms.Translate(GantryAcc, cc);

                            //IOWriteResult result321 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6ETrans" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            // so we have made a disk representing the SRS Cone, and it should have the correct center point, but now we need to rotate it so it is oriented properly
                            AccNormal = GantryAcc.GetTriNormal(0);
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E AccNormal: (" + AccNormal.x + ", " + AccNormal.y + ", " + AccNormal.z + ")");
                            Accdotprod = Vector3d.Dot(AccNormal.Normalized, diskgantrynormalV2.Normalized);
                            anglebetweenAccNormals = Math.Acos(Accdotprod);          // in radians
                                                                                     //MessageBox.Show("Beam: " + beam.beamId + " 6E angle between normals (rad): " + anglebetweenAccNormals * (180/3.141));

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                if (anglebetweennormalsneg == false)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }
                            else
                            {
                                if (anglebetweennormalsneg == true)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }

                            AccCenter = new Vector3d(cc.x, cc.y, cc.z);
                            AccRot = new Quaterniond(zaxisgd, (anglebetweenAccNormals * MathUtil.Rad2Deg));
                            MeshTransforms.Rotate(GantryAcc, AccCenter, AccRot);
                            //IOWriteResult result323 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6Erot" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")     // so this is not actually working and the SRS Cones DO NOT work properly for Prone orientations, but this really doesn't matter because the SRS Cones would only be used in Supine orientations                                                                                                 
                            {                                                                                                        // I did verify the SRS Cones to work with a Trigem case, so it works for Supine orientations  - ZM 10/19/2020 
                                MeshTransforms.Rotate(GantryAcc, g3ISO, PatOrientRot);
                            }

                            GantryAccspatial = new DMeshAABBTree3(GantryAcc);
                            GantryAccspatial.Build();

                            //IOWriteResult result58 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6EFinal" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            tempbeam.Add(new WriteMesh(GantryAcc));
                        }
                        else if (Acc == "10x10 Electron Cone")
                        {
                            diskgantrynormalMAG = diskgantrynormalV2 * 406.0;

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                cc = R - diskgantrynormalMAG;
                            }
                            else
                            {
                                cc = R + diskgantrynormalMAG;
                            }
                            //MessageBox.Show("NM Length: " + NM.Length);
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E normal: (" + diskgantrynormalMAG.x + ", " + diskgantrynormalMAG.y + ", " + diskgantrynormalMAG.z + ")");
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E cc: (" + cc.x + ", " + cc.y + ", " + cc.z + ")");
                            //MessageBox.Show("Iso: (" + ISO.x + " ," + ISO.y + " ," + ISO.z + ")");
                            //MessageBox.Show("Distance between R and cc: " + R.Distance(cc));
                            //MessageBox.Show("Distance between ISO and cc: " + ISO.Distance(cc));

                            makeRect = new TrivialRectGenerator();
                            makeRect.Height = 186.5f;
                            makeRect.Width = 186.5f;
                            makeRect.Generate();
                            GantryAcc = makeRect.MakeDMesh();
                            //IOWriteResult result322 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6Eorig" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            MeshTransforms.Translate(GantryAcc, cc);

                            //IOWriteResult result321 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6ETrans" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            // so we have made a disk representing the SRS Cone, and it should have the correct center point, but now we need to rotate it so it is oriented properly
                            AccNormal = GantryAcc.GetTriNormal(0);
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E AccNormal: (" + AccNormal.x + ", " + AccNormal.y + ", " + AccNormal.z + ")");
                            Accdotprod = Vector3d.Dot(AccNormal.Normalized, diskgantrynormalV2.Normalized);
                            anglebetweenAccNormals = Math.Acos(Accdotprod);          // in radians
                                                                                     //MessageBox.Show("Beam: " + beam.beamId + " 6E angle between normals (rad): " + anglebetweenAccNormals * (180/3.141));

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                if (anglebetweennormalsneg == false)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }
                            else
                            {
                                if (anglebetweennormalsneg == true)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }

                            AccCenter = new Vector3d(cc.x, cc.y, cc.z);
                            AccRot = new Quaterniond(zaxisgd, (anglebetweenAccNormals * MathUtil.Rad2Deg));
                            MeshTransforms.Rotate(GantryAcc, AccCenter, AccRot);
                            //IOWriteResult result323 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6Erot" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")     // so this is not actually working and the SRS Cones DO NOT work properly for Prone orientations, but this really doesn't matter because the SRS Cones would only be used in Supine orientations                                                                                                 
                            {                                                                                                        // I did verify the SRS Cones to work with a Trigem case, so it works for Supine orientations  - ZM 10/19/2020 
                                MeshTransforms.Rotate(GantryAcc, g3ISO, PatOrientRot);
                            }

                            GantryAccspatial = new DMeshAABBTree3(GantryAcc);
                            GantryAccspatial.Build();

                            //IOWriteResult result58 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6EFinal" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            tempbeam.Add(new WriteMesh(GantryAcc));

                        }
                        else if (Acc == "15x15 Electron Cone")
                        {
                            diskgantrynormalMAG = diskgantrynormalV2 * 406.0;

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                cc = R - diskgantrynormalMAG;
                            }
                            else
                            {
                                cc = R + diskgantrynormalMAG;
                            }
                            //MessageBox.Show("NM Length: " + NM.Length);
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E normal: (" + diskgantrynormalMAG.x + ", " + diskgantrynormalMAG.y + ", " + diskgantrynormalMAG.z + ")");
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E cc: (" + cc.x + ", " + cc.y + ", " + cc.z + ")");
                            //MessageBox.Show("Iso: (" + ISO.x + " ," + ISO.y + " ," + ISO.z + ")");
                            //MessageBox.Show("Distance between R and cc: " + R.Distance(cc));
                            //MessageBox.Show("Distance between ISO and cc: " + ISO.Distance(cc));

                            makeRect = new TrivialRectGenerator();
                            makeRect.Height = 235.5f;
                            makeRect.Width = 235.5f;
                            makeRect.Generate();
                            GantryAcc = makeRect.MakeDMesh();
                            //IOWriteResult result322 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6Eorig" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            MeshTransforms.Translate(GantryAcc, cc);

                            //IOWriteResult result321 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6ETrans" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            // so we have made a disk representing the SRS Cone, and it should have the correct center point, but now we need to rotate it so it is oriented properly
                            AccNormal = GantryAcc.GetTriNormal(0);
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E AccNormal: (" + AccNormal.x + ", " + AccNormal.y + ", " + AccNormal.z + ")");
                            Accdotprod = Vector3d.Dot(AccNormal.Normalized, diskgantrynormalV2.Normalized);
                            anglebetweenAccNormals = Math.Acos(Accdotprod);          // in radians
                                                                                     //MessageBox.Show("Beam: " + beam.beamId + " 6E angle between normals (rad): " + anglebetweenAccNormals * (180/3.141));

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                if (anglebetweennormalsneg == false)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }
                            else
                            {
                                if (anglebetweennormalsneg == true)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }

                            AccCenter = new Vector3d(cc.x, cc.y, cc.z);
                            AccRot = new Quaterniond(zaxisgd, (anglebetweenAccNormals * MathUtil.Rad2Deg));
                            MeshTransforms.Rotate(GantryAcc, AccCenter, AccRot);
                            //IOWriteResult result323 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6Erot" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")     // so this is not actually working and the SRS Cones DO NOT work properly for Prone orientations, but this really doesn't matter because the SRS Cones would only be used in Supine orientations                                                                                                 
                            {                                                                                                        // I did verify the SRS Cones to work with a Trigem case, so it works for Supine orientations  - ZM 10/19/2020 
                                MeshTransforms.Rotate(GantryAcc, g3ISO, PatOrientRot);
                            }

                            GantryAccspatial = new DMeshAABBTree3(GantryAcc);
                            GantryAccspatial.Build();

                            //IOWriteResult result58 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6EFinal" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            tempbeam.Add(new WriteMesh(GantryAcc));
                        }
                        else if (Acc == "20x20 Electron Cone")
                        {
                            diskgantrynormalMAG = diskgantrynormalV2 * 406.0;

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                cc = R - diskgantrynormalMAG;
                            }
                            else
                            {
                                cc = R + diskgantrynormalMAG;
                            }
                            //MessageBox.Show("NM Length: " + NM.Length);
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E normal: (" + diskgantrynormalMAG.x + ", " + diskgantrynormalMAG.y + ", " + diskgantrynormalMAG.z + ")");
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E cc: (" + cc.x + ", " + cc.y + ", " + cc.z + ")");
                            //MessageBox.Show("Iso: (" + ISO.x + " ," + ISO.y + " ," + ISO.z + ")");
                            //MessageBox.Show("Distance between R and cc: " + R.Distance(cc));
                            //MessageBox.Show("Distance between ISO and cc: " + ISO.Distance(cc));

                            makeRect = new TrivialRectGenerator();
                            makeRect.Height = 285.5f;
                            makeRect.Width = 285.5f;
                            makeRect.Generate();
                            GantryAcc = makeRect.MakeDMesh();
                            //IOWriteResult result322 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6Eorig" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            MeshTransforms.Translate(GantryAcc, cc);

                            //IOWriteResult result321 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6ETrans" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            // so we have made a disk representing the SRS Cone, and it should have the correct center point, but now we need to rotate it so it is oriented properly
                            AccNormal = GantryAcc.GetTriNormal(0);
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E AccNormal: (" + AccNormal.x + ", " + AccNormal.y + ", " + AccNormal.z + ")");
                            Accdotprod = Vector3d.Dot(AccNormal.Normalized, diskgantrynormalV2.Normalized);
                            anglebetweenAccNormals = Math.Acos(Accdotprod);          // in radians
                                                                                     //MessageBox.Show("Beam: " + beam.beamId + " 6E angle between normals (rad): " + anglebetweenAccNormals * (180/3.141));

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                if (anglebetweennormalsneg == false)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }
                            else
                            {
                                if (anglebetweennormalsneg == true)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }

                            AccCenter = new Vector3d(cc.x, cc.y, cc.z);
                            AccRot = new Quaterniond(zaxisgd, (anglebetweenAccNormals * MathUtil.Rad2Deg));
                            MeshTransforms.Rotate(GantryAcc, AccCenter, AccRot);
                            //IOWriteResult result323 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6Erot" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")     // so this is not actually working and the SRS Cones DO NOT work properly for Prone orientations, but this really doesn't matter because the SRS Cones would only be used in Supine orientations                                                                                                 
                            {                                                                                                        // I did verify the SRS Cones to work with a Trigem case, so it works for Supine orientations  - ZM 10/19/2020 
                                MeshTransforms.Rotate(GantryAcc, g3ISO, PatOrientRot);
                            }

                            GantryAccspatial = new DMeshAABBTree3(GantryAcc);
                            GantryAccspatial.Build();

                            //IOWriteResult result58 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6EFinal" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            tempbeam.Add(new WriteMesh(GantryAcc));
                        }
                        else if (Acc == "25x25 Electron Cone")
                        {
                            diskgantrynormalMAG = diskgantrynormalV2 * 406.0;

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                cc = R - diskgantrynormalMAG;
                            }
                            else
                            {
                                cc = R + diskgantrynormalMAG;
                            }
                            //MessageBox.Show("NM Length: " + NM.Length);
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E normal: (" + diskgantrynormalMAG.x + ", " + diskgantrynormalMAG.y + ", " + diskgantrynormalMAG.z + ")");
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E cc: (" + cc.x + ", " + cc.y + ", " + cc.z + ")");
                            //MessageBox.Show("Iso: (" + ISO.x + " ," + ISO.y + " ," + ISO.z + ")");
                            //MessageBox.Show("Distance between R and cc: " + R.Distance(cc));
                            //MessageBox.Show("Distance between ISO and cc: " + ISO.Distance(cc));

                            makeRect = new TrivialRectGenerator();
                            makeRect.Height = 335.0f;
                            makeRect.Width = 335.0f;
                            makeRect.Generate();
                            GantryAcc = makeRect.MakeDMesh();
                            //IOWriteResult result322 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6Eorig" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            MeshTransforms.Translate(GantryAcc, cc);

                            //IOWriteResult result321 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6ETrans" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            // so we have made a disk representing the SRS Cone, and it should have the correct center point, but now we need to rotate it so it is oriented properly
                            AccNormal = GantryAcc.GetTriNormal(0);
                            //MessageBox.Show("Beam: " + beam.beamId + " 6E AccNormal: (" + AccNormal.x + ", " + AccNormal.y + ", " + AccNormal.z + ")");
                            Accdotprod = Vector3d.Dot(AccNormal.Normalized, diskgantrynormalV2.Normalized);
                            anglebetweenAccNormals = Math.Acos(Accdotprod);          // in radians
                                                                                     //MessageBox.Show("Beam: " + beam.beamId + " 6E angle between normals (rad): " + anglebetweenAccNormals * (180/3.141));

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                if (anglebetweennormalsneg == false)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }
                            else
                            {
                                if (anglebetweennormalsneg == true)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }

                            AccCenter = new Vector3d(cc.x, cc.y, cc.z);
                            AccRot = new Quaterniond(zaxisgd, (anglebetweenAccNormals * MathUtil.Rad2Deg));
                            MeshTransforms.Rotate(GantryAcc, AccCenter, AccRot);
                            //IOWriteResult result323 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6Erot" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")     // so this is not actually working and the SRS Cones DO NOT work properly for Prone orientations, but this really doesn't matter because the SRS Cones would only be used in Supine orientations                                                                                                 
                            {                                                                                                        // I did verify the SRS Cones to work with a Trigem case, so it works for Supine orientations  - ZM 10/19/2020 
                                MeshTransforms.Rotate(GantryAcc, g3ISO, PatOrientRot);
                            }

                            GantryAccspatial = new DMeshAABBTree3(GantryAcc);
                            GantryAccspatial.Build();

                            //IOWriteResult result58 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6EFinal" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            tempbeam.Add(new WriteMesh(GantryAcc));
                        }
                    }
                    else
                    {
              // Isocentric Setups ============================================================================================================================================================
                        diskgantry = makegantryhead.MakeDMesh();
                        MeshTransforms.Translate(diskgantry, Ce);
                        //MessageBox.Show("Distance between ISO and Ce: " + ISO.Distance(Ce));

                        //MessageBox.Show("Isocentric diskgantry creation");
                        //IOWriteResult result642 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\diskgantryinitial" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);
                        //IOWriteResult result642 = StandardMeshWriter.WriteFile(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\diskgantryinitial" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);

                        Vector3d diskgantrynormal = diskgantry.GetTriNormal(0);
                        double gantrydotprod = Vector3d.Dot(diskgantrynormal.Normalized, gantrynormal.Normalized);
                        double anglebetweengantrynormals = Math.Acos(gantrydotprod);     // in radians
                                                                                         // MessageBox.Show("angle between: " + anglebetweengantrynormals);
                        if (anglebetweennormalsneg == true)
                        {
                            anglebetweengantrynormals = -1 * anglebetweengantrynormals;
                        }

                        Vector3d ISOV = new Vector3d(Ce.x, Ce.y, Ce.z);
                        Quaterniond diskrot = new Quaterniond(zaxisgd, (anglebetweengantrynormals * MathUtil.Rad2Deg));
                        MeshTransforms.Rotate(diskgantry, ISOV, diskrot);

                        if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                        {
                            g3ISO = new Vector3d(ISO.x, ISO.y, ISO.z);
                            PatOrientRot = new Quaterniond(zaxisgd, 180.0);
                            MeshTransforms.Rotate(diskgantry, g3ISO, PatOrientRot);
                        }
                        //@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\
                        //IOWriteResult result42 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\diskgantry" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);
                        //IOWriteResult result42 = StandardMeshWriter.WriteFile(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\diskgantry" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);

                        tempbeam.Add(new WriteMesh(diskgantry));

                        // System.Windows.Forms.MessageBox.Show("Trigger 7");

                        // Variables used in SRS Cone generation
                        Vector3d diskgantrynormalV2 = diskgantry.GetTriNormal(0);
                        //MessageBox.Show("Beam: " + beam.beamId + " disknorm: (" + diskgantrynormalV2.x + ", " + diskgantrynormalV2.y + ", " + diskgantrynormalV2.z + ")");
                        Vector3d diskgantrynormalMAG;
                        Vector3d cc;
                        TrivialDiscGenerator makeSRSCone;
                        TrivialRectGenerator makeRect;
                        Vector3d AccNormal;
                        double Accdotprod;
                        double anglebetweenAccNormals;
                        Vector3d AccCenter;
                        Quaterniond AccRot;

                        // SRS Cone Construction 
                        if (Acc == "SRS Cone")
                        {
                            diskgantrynormalMAG = diskgantrynormalV2 * 170.0;   // this will be the normal of the diskgantry pointing towards ISO with a mag of 17 cm, the distance between the collimator surface and the surface of the SRS Cones  

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                cc = Ce - diskgantrynormalMAG;
                            }
                            else
                            {
                                cc = Ce + diskgantrynormalMAG;
                            }
                            //MessageBox.Show("SRSCone normal: (" + diskgantrynormalMAG.x + ", " + diskgantrynormalMAG.y + ", " + diskgantrynormalMAG.z + ")");
                            //MessageBox.Show("SRSCone cc: (" + cc.x + ", " + cc.y + ", " + cc.z + ")");
                            //MessageBox.Show("NM Length: " + NM.Length);

                            makeSRSCone = new TrivialDiscGenerator();
                            makeSRSCone.Radius = 33.5f;
                            makeSRSCone.StartAngleDeg = 0.0f;
                            makeSRSCone.EndAngleDeg = 360.0f;
                            makeSRSCone.Slices = 21;
                            makeSRSCone.Generate();
                            GantryAcc = makeSRSCone.MakeDMesh();
                            //IOWriteResult result580 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\SRSConeorig" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            MeshTransforms.Translate(GantryAcc, cc);
                            //IOWriteResult result581 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\SRSConeaftertrans" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            // so we have made a disk representing the SRS Cone, and it should have the correct center point, but now we need to rotate it so it is oriented properly
                            AccNormal = GantryAcc.GetTriNormal(0);
                            Accdotprod = Vector3d.Dot(AccNormal.Normalized, diskgantrynormalV2.Normalized);
                            anglebetweenAccNormals = Math.Acos(Accdotprod);          // in radians
                                                                                     // MessageBox.Show("angle between: " + anglebetweengantrynormals);
                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                if (anglebetweennormalsneg == false)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }
                            else
                            {
                                if (anglebetweennormalsneg == true)
                                {
                                    anglebetweenAccNormals = -1 * anglebetweenAccNormals;
                                }
                            }

                            AccCenter = new Vector3d(cc.x, cc.y, cc.z);
                            AccRot = new Quaterniond(zaxisgd, (anglebetweenAccNormals * MathUtil.Rad2Deg));
                            MeshTransforms.Rotate(GantryAcc, AccCenter, AccRot);

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")     // so this is not actually working and the SRS Cones DO NOT work properly for Prone orientations, but this really doesn't matter because the SRS Cones would only be used in Supine orientations                                                                                                 
                            {                                                                                                        // I did verify the SRS Cones to work with a Trigem case, so it works for Supine orientations  - ZM 10/19/2020 
                                MeshTransforms.Rotate(GantryAcc, g3ISO, PatOrientRot);
                            }

                            GantryAccspatial = new DMeshAABBTree3(GantryAcc);
                            GantryAccspatial.Build();

                            // IOWriteResult result56 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\SRSCone" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(SRSCone) }, WriteOptions.Defaults);
                            tempbeam.Add(new WriteMesh(GantryAcc));
                        }
                    }

                    DMeshAABBTree3 diskgantryspatial = new DMeshAABBTree3(diskgantry);
                    diskgantryspatial.Build();
                    reportCang = 360.0 - CouchEndAngle;
                    if (reportCang == 360.0)
                    {
                        reportCang = 0.0;
                    }

                    // Generation of geometric model is now complete for both the patient structures and the diskgantry. The collision analysis is below
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

                    bool couchex = beam.couchexists;
                    bool boardex = beam.breastboardexists;

                    // So, if the plan is using an SRS Cone, it is just going to check for collisions with that. Because to do both would probably be a major time drag. So we check that first. Literally the same analysis though.
                    // The same "MRG..." and "lastcontigreal..." variables are used for whatever tree fires here, becuase it only does either one or the other. If we were to do both, then we would need another unique set of these variables to convey the unique information for each disk between gantry angles.

                    // First, if Fast Mode is enabled, which it is by default, we just do the following

                    if (FAST == true)
                    {
                        FASTPATBOX = false;
                        FASTCOUCH = false;
                        FASTBREASTBOARD = false;
                        AccFASTPATBOX = false;
                        AccFASTCOUCH = false;
                        AccFASTBREASTBOARD = false;

                        if (Acc != null)
                        {
                            AccFASTPATBOX = GantryAccspatial.TestIntersection(FASTPATCYLSPATIAL);
                            FASTPATBOX = diskgantryspatial.TestIntersection(FASTPATCYLSPATIAL);

                            if (couchex == true)
                            {
                                AccFASTCOUCH = diskgantryspatial.TestIntersection(PCouchInteriorSpatial);
                                FASTCOUCH = GantryAccspatial.TestIntersection(PCouchInteriorSpatial);
                            }

                            if (boardex == true)
                            {
                                AccFASTBREASTBOARD = diskgantryspatial.TestIntersection(PProne_Brst_BoardSpatial);
                                FASTBREASTBOARD = GantryAccspatial.TestIntersection(PProne_Brst_BoardSpatial);
                            }

                            if (FASTPATBOX == true)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), PatObject = "FAST Patient Bounding Box", GantryObject = "Gantry Head" });
                            }

                            if (FASTCOUCH == true)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), PatObject = "FAST Couch", GantryObject = "Gantry Head" });
                            }

                            if (FASTBREASTBOARD == true)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), PatObject = "FAST Breast Board", GantryObject = "Gantry Head" });
                            }

                            if (AccFASTPATBOX == true)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), PatObject = " FAST Patient Bounding Box", GantryObject = Acc });
                            }

                            if (AccFASTCOUCH == true)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), PatObject = "FAST Couch", GantryObject = Acc });
                            }

                            if (AccFASTBREASTBOARD == true)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), PatObject = "FAST Breast Board", GantryObject = Acc });
                            }
                        }
                        else
                        {
                            FASTPATBOX = diskgantryspatial.TestIntersection(FASTPATCYLSPATIAL);

                            if (couchex == true)
                            {
                                FASTCOUCH = diskgantryspatial.TestIntersection(PCouchInteriorSpatial);

                                // IOWriteResult result157 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\Test\ScaledCouchSpatial" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(ScaledCouchspatial) }, WriteOptions.Defaults);
                            }

                            if (boardex == true)
                            {
                                FASTBREASTBOARD = diskgantryspatial.TestIntersection(PProne_Brst_BoardSpatial);
                            }

                            if (FASTPATBOX == true)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), PatObject = "FAST Patient Bounding Box", GantryObject = "Gantry Head" });
                            }

                            if (FASTCOUCH == true)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), PatObject = "FAST Couch", GantryObject = "Gantry Head" });
                            }

                            if (FASTBREASTBOARD == true)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), PatObject = "FAST Breast Board", GantryObject = "Gantry Head" });
                            }
                        }
                    }
                    else
                    {
                        if (Acc != null)
                        {
                            // couch interior collision check-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                            if (couchex == true)
                            {
                                snear_tids = GantryAccspatial.FindNearestTriangles(PCouchInteriorSpatial, null, out INTERSECTDIST);
                                //  System.Windows.Forms.MessageBox.Show("Trigger 7");
                                if (snear_tids.a == DMesh3.InvalidID || snear_tids.b == DMesh3.InvalidID)
                                {
                                    // out of range, do nothing
                                    // System.Windows.Forms.MessageBox.Show("Trigger 8");
                                }
                                else
                                {
                                    STriDist = MeshQueries.TrianglesDistance(GantryAcc, snear_tids.a, PCouchInterior, snear_tids.b);
                                    ZABSDIST = ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest);
                                    //  System.Windows.Forms.MessageBox.Show("Trigger 9");
                                    if (ZABSDIST <= 50.0)
                                    {
                                        //System.Windows.Forms.MessageBox.Show("Couch less than 50");

                                        if (MRGCINTERIOR == null)
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
                                            MRGCINTERIOR = GantryAngle;
                                            lastcontigrealCouch = true;
                                            //System.Windows.Forms.MessageBox.Show("First couch collision   Angle: " + GantryAngle);
                                        }
                                        else if ((MRGCINTERIOR >= GantryAngle - 15.0) & (MRGCINTERIOR <= GantryAngle + 15.0))
                                        {
                                            if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                            {
                                                // if at the end of the gantry angle list, 
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                                //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                            }
                                            else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                            {
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                                //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                            }
                                            else
                                            {
                                                // contiguous collisions, do not report
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGCINTERIOR, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = true, lastcontig = false, endoflist = false });
                                                MRGCINTERIOR = GantryAngle;
                                                lastcontigrealCouch = true;
                                                //System.Windows.Forms.MessageBox.Show("Couch: Within 15 degrees of last collision, meaning it is contiguous, don't report   Angle: " + GantryAngle);
                                            }
                                        }
                                        else
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
                                            MRGCINTERIOR = GantryAngle;
                                            lastcontigrealCouch = true;
                                            //System.Windows.Forms.MessageBox.Show("Couch: NOT Within 15 degrees of last collision, not contiguous, start of new collision area    Angle: " + GantryAngle);
                                        }
                                    }
                                    else if (ZABSDIST >= 50.0 & lastcontigrealCouch == true)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = false, lastcontig = true, endoflist = false });
                                        lastcontigrealCouch = false;
                                        //System.Windows.Forms.MessageBox.Show("Couch: greater than 50, but last angle was a collison, so end of collision area    Angle: " + GantryAngle);
                                    }
                                }
                            }

                            //  System.Windows.Forms.MessageBox.Show("Trigger 10");
                            //prone breast board collision check-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                            if (boardex == true)
                            {
                                snear_tids = GantryAccspatial.FindNearestTriangles(PProne_Brst_BoardSpatial, null, out INTERSECTDIST);
                                //  System.Windows.Forms.MessageBox.Show("Trigger 11");
                                if (snear_tids.a == DMesh3.InvalidID || snear_tids.b == DMesh3.InvalidID)
                                {
                                    // out of range, do nothing
                                    // System.Windows.Forms.MessageBox.Show("Trigger 12");
                                }
                                else
                                {
                                    STriDist = MeshQueries.TrianglesDistance(GantryAcc, snear_tids.a, PProne_Brst_Board, snear_tids.b);
                                    ZABSDIST = ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest);
                                    //  System.Windows.Forms.MessageBox.Show("Trigger 13");
                                    if (ZABSDIST <= 50.0)
                                    {
                                        //System.Windows.Forms.MessageBox.Show("PATBOX collision");

                                        if (MRGBBOARD == null)
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
                                            MRGBBOARD = GantryAngle;
                                            lastcontigrealBoard = true;
                                        }
                                        else if ((MRGBBOARD >= GantryAngle - 15.0) & (MRGBBOARD <= GantryAngle + 15.0))
                                        {
                                            if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                            {
                                                // if at the end of the gantry angle list, 
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                                //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                            }
                                            else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                            {
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                                //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                            }
                                            else
                                            {
                                                // contiguous collisions, do not report
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGBBOARD, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = true, lastcontig = false, endoflist = false });
                                                MRGBBOARD = GantryAngle;
                                                lastcontigrealBoard = true;
                                                // System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                            }
                                        }
                                        else
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
                                            MRGBBOARD = GantryAngle;
                                            lastcontigrealBoard = true;
                                        }
                                    }
                                    else if (ZABSDIST >= 50.0 & lastcontigrealBoard == true)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = false, lastcontig = true, endoflist = false });
                                        lastcontigrealBoard = false;
                                    }
                                }
                            }


                            //  System.Windows.Forms.MessageBox.Show("Trigger 14");
                            //PATCYLINDER collision check----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                            snear_tids = GantryAccspatial.FindNearestTriangles(PATCYLSPATIAL, null, out INTERSECTDIST);
                            //   System.Windows.Forms.MessageBox.Show("Trigger 15");
                            if (snear_tids.a == DMesh3.InvalidID || snear_tids.b == DMesh3.InvalidID)
                            {
                                // out of range, do nothing
                                //System.Windows.Forms.MessageBox.Show("PATBOX out of range");
                                // System.Windows.Forms.MessageBox.Show("Trigger 16");
                            }
                            else
                            {
                                STriDist = MeshQueries.TrianglesDistance(GantryAcc, snear_tids.a, PATCYLINDER, snear_tids.b);
                                ZABSDIST = ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest);
                                //  System.Windows.Forms.MessageBox.Show("Trigger 17");
                                if (ZABSDIST <= 50.0)
                                {
                                    // System.Windows.Forms.MessageBox.Show("PATBOX collision");

                                    if (MRGPATBOX == null)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
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
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else
                                        {
                                            // contiguous collisions, do not report
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGPATBOX, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = true, lastcontig = false, endoflist = false });
                                            MRGPATBOX = GantryAngle;
                                            lastcontigrealPATBOX = true;
                                            //System.Windows.Forms.MessageBox.Show("Patbox: Within 15 degrees of last collision, meaning it is contiguous, don't report   Angle: " + GantryAngle);
                                        }
                                        //  System.Windows.Forms.MessageBox.Show("Trigger 19");
                                        //System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                    }
                                    else
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
                                        MRGPATBOX = GantryAngle;
                                        lastcontigrealPATBOX = true;
                                        // System.Windows.Forms.MessageBox.Show("Trigger 21");
                                        //System.Windows.Forms.MessageBox.Show("Patbox: NOT Within 15 degrees of last collision, not contiguous, start of new collision area    Angle: " + GantryAngle);
                                    }
                                }
                                else if (ZABSDIST >= 50.0 & lastcontigrealPATBOX == true)
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = false, lastcontig = true, endoflist = false });
                                    lastcontigrealPATBOX = false;
                                    //System.Windows.Forms.MessageBox.Show("Patbox: greater than 50, but last angle was a collison, so end of collision area    Angle: " + GantryAngle);
                                    // System.Windows.Forms.MessageBox.Show("Trigger 22");
                                }
                            }

                            // END OF Gantry Accessory COLLISION ANALYSIS
                        }


                        // NORMAL DISKGANTRY/GANTRY HEAD SURFACE COLLISION ANALYSIS
                        // couch interior collision check-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

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
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGCINTERIOR = GantryAngle;
                                        lastcontigrealCouch = true;
                                        //System.Windows.Forms.MessageBox.Show("First couch collision   Angle: " + GantryAngle);
                                    }
                                    else if ((MRGCINTERIOR >= GantryAngle - 15.0) & (MRGCINTERIOR <= GantryAngle + 15.0))
                                    {
                                        if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                        {
                                            // if at the end of the gantry angle list, 
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else
                                        {
                                            // contiguous collisions, do not report
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGCINTERIOR, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = true, lastcontig = false, endoflist = false });
                                            MRGCINTERIOR = GantryAngle;
                                            lastcontigrealCouch = true;
                                            //System.Windows.Forms.MessageBox.Show("Couch: Within 15 degrees of last collision, meaning it is contiguous, don't report   Angle: " + GantryAngle);
                                        }
                                    }
                                    else
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGCINTERIOR = GantryAngle;
                                        lastcontigrealCouch = true;
                                        //System.Windows.Forms.MessageBox.Show("Couch: NOT Within 15 degrees of last collision, not contiguous, start of new collision area    Angle: " + GantryAngle);
                                    }
                                }
                                else if (ZABSDIST >= 50.0 & lastcontigrealCouch == true)
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = false, lastcontig = true, endoflist = false });
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
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGBBOARD = GantryAngle;
                                        lastcontigrealBoard = true;
                                    }
                                    else if ((MRGBBOARD >= GantryAngle - 15.0) & (MRGBBOARD <= GantryAngle + 15.0))
                                    {
                                        if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                        {
                                            // if at the end of the gantry angle list, 
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else
                                        {
                                            // contiguous collisions, do not report
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGBBOARD, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = true, lastcontig = false, endoflist = false });
                                            MRGBBOARD = GantryAngle;
                                            lastcontigrealBoard = true;
                                            // System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                        }
                                    }
                                    else
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGBBOARD = GantryAngle;
                                        lastcontigrealBoard = true;
                                    }
                                }
                                else if (ZABSDIST >= 50.0 & lastcontigrealBoard == true)
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = false, lastcontig = true, endoflist = false });
                                    lastcontigrealBoard = false;
                                }
                            }
                        }


                        //  System.Windows.Forms.MessageBox.Show("Trigger 14");
                        //PATCYLINDER collision check----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                        snear_tids = diskgantryspatial.FindNearestTriangles(PATCYLSPATIAL, null, out INTERSECTDIST);
                        //   System.Windows.Forms.MessageBox.Show("Trigger 15");
                        if (snear_tids.a == DMesh3.InvalidID || snear_tids.b == DMesh3.InvalidID)
                        {
                            // out of range, do nothing
                            //System.Windows.Forms.MessageBox.Show("PATBOX out of range");
                            // System.Windows.Forms.MessageBox.Show("Trigger 16");
                        }
                        else
                        {
                            STriDist = MeshQueries.TrianglesDistance(diskgantry, snear_tids.a, PATCYLINDER, snear_tids.b);
                            ZABSDIST = ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest);
                            //  System.Windows.Forms.MessageBox.Show("Trigger 17");
                            if (ZABSDIST <= 50.0)
                            {
                                // System.Windows.Forms.MessageBox.Show("PATBOX collision");

                                if (MRGPATBOX == null)
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
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
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                        //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                    }
                                    else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                        //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                    }
                                    else
                                    {
                                        // contiguous collisions, do not report
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGPATBOX, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = true, lastcontig = false, endoflist = false });
                                        MRGPATBOX = GantryAngle;
                                        lastcontigrealPATBOX = true;
                                        //System.Windows.Forms.MessageBox.Show("Patbox: Within 15 degrees of last collision, meaning it is contiguous, don't report   Angle: " + GantryAngle);
                                    }
                                    //  System.Windows.Forms.MessageBox.Show("Trigger 19");
                                    //System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                }
                                else
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
                                    MRGPATBOX = GantryAngle;
                                    lastcontigrealPATBOX = true;
                                    // System.Windows.Forms.MessageBox.Show("Trigger 21");
                                    //System.Windows.Forms.MessageBox.Show("Patbox: NOT Within 15 degrees of last collision, not contiguous, start of new collision area    Angle: " + GantryAngle);
                                }
                            }
                            else if (ZABSDIST >= 50.0 & lastcontigrealPATBOX == true)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = false, lastcontig = true, endoflist = false });
                                lastcontigrealPATBOX = false;
                                //System.Windows.Forms.MessageBox.Show("Patbox: greater than 50, but last angle was a collison, so end of collision area    Angle: " + GantryAngle);
                                // System.Windows.Forms.MessageBox.Show("Trigger 22");
                            }
                        }

                        // End OF DISK GANTRY COLLISION ANALYSIS
                    }  // ends the IF FAST 

                    //   System.Windows.Forms.MessageBox.Show("Trigger 23");

                    //  MessageBox.Show("Collision analysis done");

                }  // ====================================================================== END OF GANTRY ANGLE LOOP =======================================================================================================================================================================================================================================================================================================================================================================================

            }    // ends if counch angle start = couch angle end

            //MessageBox.Show("COUCH LOOP DONE    ");

            // this writes out the "composite" STL file of each beam. It is put on Therapy physics so everyone can access it. The GUI then uses this to diplay a picture of each beam. 
            
              IOWriteResult EVERY = StandardMeshWriter.WriteFile(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\" + beam.patientId + "_" + beam.courseId + "_" + beam.planId + "_" + "Beam_" + beam.beamId + ".stl", tempbeam, WriteOptions.Defaults); // Lahey
             // IOWriteResult EVERY = StandardMeshWriter.WriteFile(@"\\shceclipseimg\PHYSICS\New File Structure PHYSICS\Script Reports\Collision_Check_STL_files\" + beam.patientId + "_" + beam.courseId + "_" + beam.planId + "_" + "Beam_" + beam.beamId + ".stl", tempbeam, WriteOptions.Defaults); // Winchester


            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Beam " + beam.beamId + " analysis complete.");

            return collist;

        } // END OF BEAM COLLISION ANALYSIS



        //==============================================================================================================================================================================================================================================================================================================================================================================================================================================================================================================================

            

        // This method makes all of the patient-related structures. Most of it is dedicated to constructing PATBOX, the patient bounding box. BOXMAKER is called by CollisionCheck.
        // This is quite extensive because it involves manipulations to each corner of the box that are different depending on the body area of the CT scan. The indices defining the triangles and the vertex points are all manually constructed and then put together at the end, so it takes up a lot of space.
        public static List<DMesh3> BOXMAKER(BEAM beam, DMesh3 PBodyContour, DMesh3 PCouchInterior, DMesh3 PATCYLINDER, DMesh3 FASTPATCYLINDER, DMesh3 PProne_Brst_Board, bool FAST)
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
            //IOWriteResult result24 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\PBODY" + beam.beamId + ".stl", new List<WriteMesh>() { new WriteMesh(PBodyContour) }, WriteOptions.Defaults);
           // IOWriteResult result24 = StandardMeshWriter.WriteFile(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\PBODY" + beam.beamId + ".stl", new List<WriteMesh>() { new WriteMesh(PBodyContour) }, WriteOptions.Defaults);


            //@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files 

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

                //IOWriteResult result30 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\CouchInterior" + beam.beamId + ".stl", new List<WriteMesh>() { new WriteMesh(PCouchInterior) }, WriteOptions.Defaults);
                //IOWriteResult result30 = StandardMeshWriter.WriteFile(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\CouchInterior" + beam.beamId + ".stl", new List<WriteMesh>() { new WriteMesh(PCouchInterior) }, WriteOptions.Defaults);
                //@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files 


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

                //IOWriteResult result36 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\ProneBreastBoard" + beam.beamId + ".stl", new List<WriteMesh>() { new WriteMesh(PProne_Brst_Board) }, WriteOptions.Defaults);
            
                PatMeshList.Add(PProne_Brst_Board);
            }

           // System.Windows.Forms.MessageBox.Show("Start of PATBOX construction");


      //PATIENT BOUNDING BOX CONSTRUCTION ---------------------------------------------------------------------------------------------------------------------------------------------------------

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

           // MessageBox.Show("patbxshift: " + patbxshift);
           // MessageBox.Show("patbyshift: " + patbyshift);

            //need box extension to cover entire patient !!!!!!!!!!!!!!!

            //List<Vector3d> vertices = new List<Vector3d>();
            //List<Index3i> triangles = new List<Index3i>();
            // each triangle is simply a struct of 3 ints which are indices referring to the vertices which make up that triangle
            // in other words, a triangle is a collection of 3 vertices, and it is just composed of indices referencing the vertices

            //Vector3d vect = new Vector3d();

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

            // The code below makes the Cylindrical patient bounding box - 12/23/2020

            // 101 vertices around the edge of each disk making up the capped ends of the cylinder (because there is a 0 degree and a 360 degree point)
            // plus each disk has a center point, so each disk is made up of 102 points
            // so, 103 is the center point of the downward disk, and 104 is the first edge point of the downward disk
            // 400 triangles in total
            //double Radius = 0.0;
            //if(patbxshift > patbyshift)
            //{
            //    Radius = (float)patbxshift;
            //}
            //else if(patbyshift > patbxshift)
            //{
            //    Radius = (float)patbyshift;
            //}

            double SemiMajor = patbxshift + 10.0;
            double SemiMinor = patbyshift + 10.0;
            //Fast mode enabled add 5.0 cm to axes. These are only calculated if Fast mode is enabled, the objects are always made though.
            double FastSemiMajor = SemiMajor + 50.0;
            double FastSemiMinor = SemiMinor + 50.0;
            double FastPatCylFaceXcoord = 0.0;
            double FastPatCylFaceYcoord = 0.0;
            Vector3d[] FastPatCylVectList = new Vector3d[205];
            FASTPATCYLINDER = new DMesh3(MeshComponents.VertexNormals);

           // MessageBox.Show("Radius: " + Radius);
            Vector3d[] PatCylVectList = new Vector3d[205];
           // MessageBox.Show("List size 1: " + PatCylVectList.Length);
            List<Index3i> PatCylTriangleIndexList = new List<Index3i>();
            PATCYLINDER = new DMesh3(MeshComponents.VertexNormals);

            try
            {
                PatCylVectList[0] = centerofforwardface;
               // MessageBox.Show("Index trig 1");
               // MessageBox.Show("List size 2: " + PatCylVectList.Length);
                PatCylVectList[102] = centerofdownwardface;
                FastPatCylVectList[0] = centerofforwardface;
                FastPatCylVectList[102] = centerofdownwardface;

                // MessageBox.Show("Index trig 2");
                double PatCylFaceXcoord = 0.0;
                double PatCylFaceYcoord = 0.0;
                int iterate = 1;

                //Actually an Elliptical Cylinder. patbxshift id the semi-major axis and patbyshift is the semi-minor axis. 1 cm margin added to adjust for considerable curving/eccentricity to ensure entire patient is covered. Fast mode adds 5 cm anyway.
                for (double j = 0.0; j < 361.0; j += 3.6)   // 101 times
                {
                    PatCylFaceXcoord = centerofforwardface.x + (SemiMajor * Math.Cos(j * MathUtil.Deg2Rad));
                    PatCylFaceYcoord = centerofforwardface.y + (SemiMinor * Math.Sin(j * MathUtil.Deg2Rad));
                    PatCylVectList[iterate] = new Vector3d(PatCylFaceXcoord, PatCylFaceYcoord, centerofforwardface.z);
                   // MessageBox.Show("Index trig 3");
                    PatCylVectList[102 + iterate] = new Vector3d(PatCylFaceXcoord, PatCylFaceYcoord, centerofdownwardface.z);
                    //MessageBox.Show("Index trig 4");

                    if(FAST == true)
                    {
                        FastPatCylFaceXcoord = centerofforwardface.x + (FastSemiMajor * Math.Cos(j * MathUtil.Deg2Rad));
                        FastPatCylFaceYcoord = centerofforwardface.y + (FastSemiMinor * Math.Sin(j * MathUtil.Deg2Rad));
                        FastPatCylVectList[iterate] = new Vector3d(FastPatCylFaceXcoord, FastPatCylFaceYcoord, centerofforwardface.z);
                        // MessageBox.Show("Index trig 3");
                        FastPatCylVectList[102 + iterate] = new Vector3d(FastPatCylFaceXcoord, FastPatCylFaceYcoord, centerofdownwardface.z);
                        //MessageBox.Show("Index trig 4");
                    }

                    iterate++;
                }
                //NEED WRITE OUT EVERYTHING TO CHECK
                //MessageBox.Show("Iterate final value: " + iterate);

                //int cnt = 0;
                //foreach (Vector3d upcylvect in PatCylVectList)
                //{
                //    using (StreamWriter LWRITE = File.AppendText(@"C:\Users\ztm00\Desktop\Upper_cylinder_Vertices.txt"))
                //    {
                //        LWRITE.WriteLine("(" + upcylvect.x + ", " + upcylvect.y + ", " + upcylvect.z + ")     " + cnt);
                //        cnt++;
                //    }
                //}

                for (int i = 0; i < 100; i++)
                {
                    PatCylTriangleIndexList.Add(new Index3i(i + 1, i + 2, 0));
                    PatCylTriangleIndexList.Add(new Index3i(i + 103, i + 104, 102));

                    PatCylTriangleIndexList.Add(new Index3i(i + 1, i + 2, i + 103));
                    PatCylTriangleIndexList.Add(new Index3i(i + 103, i + 104, i + 2));
                }

                PatCylTriangleIndexList.Add(new Index3i(101, 1, 203));
                PatCylTriangleIndexList.Add(new Index3i(203, 103, 1));
   
                //cnt = 0;
                //foreach (Index3i tri in PatCylTriangleIndexList)
                //{
                //    using (StreamWriter LWRITE = File.AppendText(@"C:\Users\ztm00\Desktop\Upper_cylinder_Vertices.txt"))
                //    {
                //        LWRITE.WriteLine("(" + tri.a + ", " + tri.b + ", " + tri.c + ")     " + cnt);
                //        cnt++;
                //    }
                //}

                foreach (Vector3d spec in PatCylVectList)
                {
                    PATCYLINDER.AppendVertex(new NewVertexInfo(spec));
                }

                if(FAST == true)
                {
                    foreach(Vector3d fpec in FastPatCylVectList)
                    {
                        FASTPATCYLINDER.AppendVertex(new NewVertexInfo(fpec));
                    }
                }

                foreach (Index3i tri in PatCylTriangleIndexList)
                {
                    PATCYLINDER.AppendTriangle(tri);

                    if(FAST == true)
                    {
                        FASTPATCYLINDER.AppendTriangle(tri);
                    }
                }
            }
            catch(Exception e)
            {
                MessageBox.Show("Cylinder Build error \n\n\n\n" + e.ToString() + "\n\n\n" + e.StackTrace + "\n\n\n" + e.Source);
            }
               
            // CYLINDER IS WORKING PROPERLY!!!!! 

           // IOWriteResult result151 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\PATCYLINDER.stl", new List<WriteMesh>() { new WriteMesh(PATCYLINDER) }, WriteOptions.Defaults);

            //use the remesher in Gradientspace to add triangles/vertices to mesh based off of the simple box mesh. This increases the resolution of the box to make it useful for the collision analysis.
            // Remeshing settings aren't perfect, but they are fairly dialed in
            // ProgOutput.AppendText(Environment.NewLine);
            // ProgOutput.AppendText("Remeshing patient bounding box... ");
            DMesh3 PATCYLINDERCOPY = new DMesh3(PATCYLINDER);

            Remesher CY = new Remesher(PATCYLINDER);
            MeshConstraintUtil.PreserveBoundaryLoops(CY);
            CY.PreventNormalFlips = true;
            CY.SetTargetEdgeLength(50.0);
            CY.SmoothSpeedT = 0.5;
            CY.SetProjectionTarget(MeshProjectionTarget.Auto(PATCYLINDERCOPY));
            CY.ProjectionMode = Remesher.TargetProjectionMode.Inline;

            for (int k = 0; k < 8; k++)
            {
                CY.BasicRemeshPass();
            }

            //IOWriteResult result152 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\PATCYLINDERremeshed.stl", new List<WriteMesh>() { new WriteMesh(PATCYLINDER) }, WriteOptions.Defaults);

            gs.MeshAutoRepair repair = new gs.MeshAutoRepair(PATCYLINDER);
            repair.RemoveMode = gs.MeshAutoRepair.RemoveModes.None;
            repair.Apply();

            //IOWriteResult result153 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\PATCYLINDERremeshed+repaired.stl", new List<WriteMesh>() { new WriteMesh(PATCYLINDER) }, WriteOptions.Defaults);

            if(FAST == true)
            {
                DMesh3 FASTPATCYLINDERCOPY = new DMesh3(FASTPATCYLINDER);

                Remesher FCY = new Remesher(FASTPATCYLINDER);
                MeshConstraintUtil.PreserveBoundaryLoops(FCY);
                FCY.PreventNormalFlips = true;
                FCY.SetTargetEdgeLength(50.0);
                FCY.SmoothSpeedT = 0.5;
                FCY.SetProjectionTarget(MeshProjectionTarget.Auto(FASTPATCYLINDERCOPY));
                FCY.ProjectionMode = Remesher.TargetProjectionMode.Inline;

                for (int k = 0; k < 8; k++)
                {
                    FCY.BasicRemeshPass();
                }

                gs.MeshAutoRepair Frepair = new gs.MeshAutoRepair(FASTPATCYLINDER);
                Frepair.RemoveMode = gs.MeshAutoRepair.RemoveModes.None;
                Frepair.Apply();

                //IOWriteResult result155 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\FASTPATCYLINDERremeshed+repaired.stl", new List<WriteMesh>() { new WriteMesh(FASTPATCYLINDER) }, WriteOptions.Defaults);
            }

            // MessageBox.Show("Center of Forward Face (plane above head): (" + centerofforwardface.x + " ," + centerofforwardface.y + " ," + centerofforwardface.z + ")");

            /* The code below is the old code for making the rectangular patient bounding box, which is no longer used, but kept gor historical purposes
      
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

             IOWriteResult result3 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\PATBOX" + beam.beamId + ".stl", new List<WriteMesh>() { new WriteMesh(PATBOX) }, WriteOptions.Defaults);
            //tempbeam.Add(new WriteMesh(PATBOX));

           */

            PatMeshList.Add(PATCYLINDER);
            if(FAST == true)
            {
                PatMeshList.Add(FASTPATCYLINDER);
            }
            //System.Windows.Forms.MessageBox.Show("End of BOXMAKER");
            return PatMeshList;
        }


        //=================================================================================================================================================================================================================================================================================================


        public static List<CollisionAlert> CollisionCheckExecute(List<BEAM> BEAMLIST, TextBox ProgOutput, string Acc, bool FAST)
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
                    List<CollisionAlert> tempcol = BeamCollisionAnalysis(beam, ProgOutput, Acc, FAST);
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

        public string PatObject { get; set; }  

        public string GantryObject { set; get; }

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

        public bool MLCtype { get; set; }

        public string beamId { get; set; }

        public double arclength { get; set; }

        public List<CONTROLPOINT> ControlPoints { get; set; }

        //image stuff put in beam
        public Vector3d imageuserorigin { get; set; }


        public Vector3d imageorigin { get; set; }

        //this is only for use with non-isocentric setups
        public Vector3d APISource { get; set; }

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
