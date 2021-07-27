using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using MessageBox = System.Windows.Forms.MessageBox;

using g3;


/*
    Collision Check - CollisionCheckAnalysis


    Description/information:
    This is the actual collision calculation calculation engine of the program. Collision check execute calls this in a parallel foreach loop so that it runs for every beam.
    BOXMAKER is called early in the program to generate all the patient-related structures of the 3D collision model.
    The gantry-related objects are then constructed here and transformed so they are in the same coordinate system (because a couch kick is effectively a corrdinate system transformation).
    Once all the objects have been made as GradientSpace meshes and positioned properly relative to each other, methods of the GradientSpace package are used to calculate distances
    and determine if a collision has ocurred. this information is then stored using the collisionalert class for output back to the GUI.

    ==========================================================================

    Copyright (C) 2021 Zackary Thomas Ricci Morelli


    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<https://www.gnu.org/licenses/>.

    I can be contacted at: zackmorelli@gmail.com


    Release 3.2 - 7/26/2021


*/

namespace CollisionCheck
{
    public class CollisionCheckAnalysis
    {
        //These first 3 methods are no longer used, but I've kept them here because they might be helpful.
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
            //wes start by declaring a unch of stuff that is used througout this large method
            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Beam " + beam.beamId + " analysis running....");
            
            List<CollisionAlert> collist = new List<CollisionAlert>();

            double INTERSECTDIST = -1000.0;
            Index2i snear_tids = new Index2i(-1, -1);
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
                    PatMeshList = BOXMAKERclass.BOXMAKER(beam, PBodyContour, PCouchInterior, PATCYLINDER, FASTPATCYLINDER, PProne_Brst_Board, FAST);
                }
                catch(Exception e)
                {
                    System.Windows.Forms.MessageBox.Show(e.ToString());
                }

               // System.Windows.Forms.MessageBox.Show("after Patbox");
               // System.Windows.Forms.MessageBox.Show("PatMeshListSize: " + PatMeshList.Count);

                //We pass BOXMAKER a bunch of empty variables when we call it
                //but now we go through the list it returns to pick through the structures it actually made, based of what was actually present in the CT scan
                // we also make Axis-Aligned Bounding Box (AABB) Trees out of the meshes, which all end in "spatial", which are hierarchial objects which can effiecently perform collision queries
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

                //So now that everything from BOXMAKER is in order, and we have declared all our variables, we can start thinking about what is going on with the gantry
                //Remember, we aren't in a beam loop here, but this method is in a parallel foreach loop for all the beams, so this method happens for each beam
                //What the first section of code below does is go through the calulation engine that determines the position vectors that are used to make the disk which represents the gantry head
                //using arbitrary gantry angles of 3, 6, and 9 degrees. However, it does this calculation using the real couch angle of the beam. Keep in mind that the position of the gantry disk is dependent on gantry angle and couch angle.
                //We do this calculation for three arbitrary gantry angles, or 3 disks, so that we can define a plane using the center points of the disks. We can then calculate the normal vector of that plane in order to determine
                //the z-axis of this particular beam at its specific couch angle. This is important because we need a vector that defines the z-axis so we can properly position the actual gantry disk of this beam that we'll calculate later.
                //So I know this seems ridiculous that we go through all this work to determine of z-axis, but that is whta we have to do since the couch angle is variable.
                //There are more comments that describe how the gantry disk calculation engine works in the section that does the real calculation 


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
                // This calculates the gantry center point for three gantry angles so we can use them to define a plane whose normal will be used in the diskgantry rotation.
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
                // So now that we have our z-axis defined, we need to figure out the gantry angles that we need to run through the calculation engine with
                // in order to generate an accurate model of this beam. We need to spend some time figuring this out becuase there are different ypes of beams
                // that the program can run on. 

                //System.Windows.Forms.MessageBox.Show("MLCType: " + beam.MLCtype);
                //System.Windows.Forms.MessageBox.Show("Arc Length: " + beam.ArcLength);
                if (beam.arclength == 0)
                {
                    // So this beam is not an arc, meaning we only need to evaluate it at one gantry angle, its start angle, which we know for every beam, whether it is static MLC or not
                    // No real point in running in Fast mode if we are only evaluating one gantry angle, so we force change it here
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
                    //So this determines the Source position at 0 couch angle
                    myZ = ISO.z;
                    myX = 1000 * Math.Cos((((GantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.x;    // - 90 degrees is because the polar coordinate system has 0 degrees on the right side
                    myY = 1000 * Math.Sin((((-GantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.y;   // need negative because y axis is inverted
                                                                                                  // THIS WORKS!

                    Vector3d mySOURCE = new Vector3d();
                    if (Acc != null && Acc != "SRS Cone")
                    {
                        //Non-isocentric beam. Here we use the Source position from the API, which has already had a couch transform performed on it.
                        mySOURCE = beam.APISource;
                    }
                    else
                    {
                        //Isocentric beam.Here we use the non-transformed source position
                        mySOURCE = new Vector3d(myX, myY, myZ);
                        //MessageBox.Show("Isocentric beam");
                    }

                    //MessageBox.Show("SOURCE : (" + convSOURCE.x + " ," + convSOURCE.y + " ," + convSOURCE.z + ")");

                    // this determines the position of gantrycenterpoint, the center of the disk representing the gantry head, (from mySOURCE) at all gantry angles at couch 0 degrees
                    // First we calculate the X and Y translations needed to move from Source to the center of the gantry head.
                    //this is based off the fact that the distance between the source and the center of the gantry head is always 58.5 cm.

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
                    //We than apply the transforms.
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

                    //these represent the tranforms needed to find the left and right side of the gantry head
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


                    //the gantrycenter now represents the center point of the gantry for all gantry angles at 0 degrees couch angle
                    //and we calculated the front, back, left, and right edges of the gantry head at 0 couch
                    // A coordinate transformation for couch angle is performed next
                    //once the gantry centerpoint and the patient are in the same coordinate system, the edges of the gantry are found from there.

                    //System.Windows.Forms.MessageBox.Show("Trigger 4");
                    //  MessageBox.Show(beam.beamId + " gantrycenter before transform is: (" + gantrycenter.x + " ," + gantrycenter.y + " ," + gantrycenter.z + ")");

                    //this is something we need later on to rotate the disks into the correct plane
                    bool anglebetweennormalsneg = false;
                    if (GantryAngle >= 0 & GantryAngle <= 180)
                    {
                        anglebetweennormalsneg = true;
                    }

                    if (Acc != null && Acc != "SRS Cone")
                    {
                        //Non-isocentric beam. For the non-isocentric beam we don't want to do the couch angle transform
                        //becuase we have found the ganry head psotions using the source position that was already transformed
                        // so we skip right to constructing the mesh
                        goto GantryConstruction;
                    }

                    //=========================================================================================================================================================================================================================
                    //===================COORDINATE SYSTEM TRANSFORMATION FOR COUCH ANGLE

                    //Regular Couch angle transformation for Isocentric beams
                    if (CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
                    {
                        // this is really for 0 to 90 couch angle on the linac. for some reason couch angle is reversed in ESAPI
                        //  MessageBox.Show("REAL couch ANGLE :  " + realangle + "  ");
                        //  MessageBox.Show("TRIGGER ROT 0 to 270");
                        ANGLE = 360.0 - CouchEndAngle;

                        if(UserOrigin != ISO)
                        {
                            // The Iso is not at the user origin for this plan (breast plan for example). this makes things complicated
                            //instead of simply rotating the coordinate system, we must translate the gantry to the user origin, rotate it, and then translate back
                            //unfortunatley, I originally designed the program with the assumption that the user origin and Iso are the same
                            //so I implemented this fix instead of rehauling the whole thing
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
                        //this is really 0 to 270 couch angle on the linac
                         ANGLE = CouchEndAngle;

                        if (UserOrigin != ISO)
                        {
                            // The Iso is not at the user origin for this plan (breast plan for example). this makes things complicated
                            //instead of simply rotating the coordinate system, we must translate the gantry to the user origin, rotate it, and then translate back
                            //unfortunatley, I originally designed the program with the assumption that the user origin and Iso are the same
                            //so I implemented this fix instead of rehauling the whole thing
                            //THIS WORKS TESTED 5/24/2021

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

                    // Now that the gantry center point is in the same coordinate system as the patient objects, we can calculate the left, right, front, and back edges
                    // of the simply based off physical dimensions gantry head based.
                    // Note that the previos calculation we did for the edge positions was neccessary because the non-isocentric beams require that.

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
                    //Now that we have all the position vectors we need to represent the gantry head in the same coordinate system
                    // as everything else, the gantry construction starts below

                    GantryConstruction:

                    Vector3d Ri = new Vector3d(RIGHTEDGE.x, RIGHTEDGE.y, RIGHTEDGE.z);  //0           5     9    13
                    Vector3d Le = new Vector3d(LEFTEDGE.x, LEFTEDGE.y, LEFTEDGE.z);      //1          6     10   14
                    Vector3d Ba = new Vector3d(BACKEDGE.x, BACKEDGE.y, BACKEDGE.z);      //2          7     11   15
                    Vector3d Fr = new Vector3d(FRONTEDGE.x, FRONTEDGE.y, FRONTEDGE.z);    //3         8     12   16
                    Vector3d Ce = new Vector3d(gantrycenter.x, gantrycenter.y, gantrycenter.z);   //4

                    //System.Windows.Forms.MessageBox.Show("Trigger 6");
                    // MessageBox.Show("Trig");

                    //So first we use the four points we have to make a square model of the gantry
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

                    //but the gantry isn't actaully a square, its a circle
                    //So what we do is use methods in GradientSpace to make a disk. broken up into 72 triangles
                    //We then move that disk to gantrycenter
                    // we then calculate the angle bewtween the disk and the square gantry in order to rotate the disk into place
                    //But before we do that, there is a bunch of code to deal with non-isocentric beams and collimator accessories.
                    //I should point out that the Brainlab cranial SRS cone is an accessory, but that is used with isocentric beams

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

                    //So first we calculate where the diskganry is for any non-isocentric beam. then we figure out where the square representing the face of the electron cone is.
                    //It is important that both objects are included in the model

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

                        //I don't have an explanation for this, but unfortunatley the way we originally found the gantrycenter from the Source position
                        //does not work for non-isocentric beams, which have fixed SSDs. It might be becuase it only works at 0 degrees couch angle and many electron plans
                        //have couch kicks. Fortunatley, the only thing that is wrong about the square gantry's position for non-isocentric beams is its distance
                        //from the Source and Iso, or in other words basically its vertical position. These distances should of course be fixed at 58.5 and 41.5 cm, respectively,
                        // but for some reason that simply does not hold up with the non-isocentric beams. However, the square gantry still has the correct dimensions,
                        // and more importantly the correct angle, or plane in space that it occupies. So all we need to do to make it work for non-isocentric beams
                        // is translate its center point to the correct position. That is why I don't have a whole alternate code path from the beggining for
                        //the non-isocentric beams, becuase it is not like the whole thing is wrong.

                        // so we do some brute-force geometry here to force the square gantry we have already made into the correct position.
                        // we know the distance between the Iso and gantryhead must be 41.5 cm. 
                        // So if we take the Iso position, and move it up 41.5, that gives us the gantryhead position.
                        // This would work easily in the Y direction with the gantry at 0 degrees and the couch at 0 degrees
                        // but, the 41.5 cm distance will be broken up into X, Y, and Z components depending on gantry and couch angle, so we need to do it for 
                        //all three dimensions. The way we do this is by taking the differences in three dimensions and scaling them by 0.415 (becuase 100 * 0.415 = 41.5)
                        // the sum of these three numbers should be 100.

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

                        //So after translating the square gantry to correct for the vertical distance issue with the non-isocentric beams, we translate the disk gantry
                        //to the same place and then rotate to be in-line with the square gantry, per the normal method used with the ioscentric beams

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

                        //So that ends the disk gantry generation for non-isocentric beams

                        //Now we'll make the planes representing the electron cones
                        // first will declare a bunch of common variables used in Electron Cone generation
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
                        //All the cones are 40.6 cm away from the gantryhead
                        if (Acc == "6x6 Electron Cone")
                        {
                            //take the normal vector of the disk gantry and multiply it by 406 to get a vector pointing to the center of the cone face (from the disk gantry)
                            diskgantrynormalMAG = diskgantrynormalV2 * 406.0;

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")
                            {
                                //notice we reuse the R vector which represents the center of the gantryhead for non-isocentric beams
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

                            //this makes a square with the dimensiosn of each cone to represent the face of the cone
                            makeRect = new TrivialRectGenerator();
                            makeRect.Height = 151.5f;
                            makeRect.Width = 151.5f;
                            makeRect.Generate();
                            GantryAcc = makeRect.MakeDMesh();
                            //IOWriteResult result322 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6Eorig" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);

                            //translates the square to the center point we calculated
                            MeshTransforms.Translate(GantryAcc, cc);

                            //IOWriteResult result321 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SRSCone\6ETrans" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GantryAcc) }, WriteOptions.Defaults);
                            // now we need to rotate it so it is oriented properly
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

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")                                                                                                      
                            {                                                                                                        
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

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")                                                                                                      
                            {                                                                                                         
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
                            // now we need to rotate it so it is oriented properly
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

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")                                                                                                      
                            {                                                                                                         
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
                            // now we need to rotate it so it is oriented properly
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

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")                                                                                                      
                            {                                                                                                         
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
                            // now we need to rotate it so it is oriented properly
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

                            if (beam.TreatmentOrientation == "HeadFirstProne" || beam.TreatmentOrientation == "FeetFirstProne")                                                                                                      
                            {                                                                                                        
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
                        // Now we do the normal isocentric beams
                        // Isocentric Setups ============================================================================================================================================================
                        //Make disk agntry and move it to gantrycenter
                        diskgantry = makegantryhead.MakeDMesh();
                        MeshTransforms.Translate(diskgantry, Ce);
                        //MessageBox.Show("Distance between ISO and Ce: " + ISO.Distance(Ce));

                        //MessageBox.Show("Isocentric diskgantry creation");
                        //IOWriteResult result642 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\diskgantryinitial" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);
                        //IOWriteResult result642 = StandardMeshWriter.WriteFile(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\diskgantryinitial" + beam.beamId + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);

                        //rotate the diskgantry to be in line with the square gantry
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

                        // Now will make the SRS cone, if applicable
                        // we declare a bunch of variables outside the if statement though

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

                    // this fixes the couch angle used for output to the user, since it is reversed in ESAPI
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

                    // First, if Fast Mode is enabled, we just do the following
                    if (FAST == true)
                    {
                        FASTPATBOX = false;
                        FASTCOUCH = false;
                        FASTBREASTBOARD = false;
                        AccFASTPATBOX = false;
                        AccFASTCOUCH = false;
                        AccFASTBREASTBOARD = false;

                        //So TestIntersection is a GradientSpace method that literally just determines if two AABB trees are intersecting each other. no distances involved.
                        //The calculation it does is pretty optimized due to the hierarchial nature of the AABB tree. this is why FAST mode is fast.
                        //keep in mind the patient-bounding cylinder is a little bigger in fast mode to try and accomodate for the fact that we want to find situations where
                        //the gantry is within 5 cm of hitting something

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
                        //now we do the standard collision analysis where we calculate distances using the FindNearestTriangles method
                        //FindNearestTriangles finds the two triangles on the two given AABB trees that are closest to each other
                        //and calculates a distance between them. The distance is given as an "out" variable. 
                        //the return is the indices of the triangles it found.

                        //The additional logic you'll see here is complicated.
                        // what it is trying to do is find areas of the arc where it is closer than 5 cm and give us a start and stop alert
                        //becuase we don't want to be inundated with collison alerts for a segment of an arc that is colliding like crazy
                        //it makes the output hard to read
                        //it also tries to catch if the beam is still colliding when it ends

                        //STriDist = MeshQueries.TrianglesDistance(GantryAcc, snear_tids.a, PCouchInterior, snear_tids.b);
                        //ZABSDIST = ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest);

                        //First we test the accessories, if there is one
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
                                     System.Windows.Forms.MessageBox.Show("Invalid Triangle mesh indices");
                                }
                                else
                                {
                                    //  System.Windows.Forms.MessageBox.Show("Trigger 9");
                                    if (INTERSECTDIST <= 50.0)
                                    {
                                        //System.Windows.Forms.MessageBox.Show("Couch less than 50");

                                        if (MRGCINTERIOR == null)
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
                                            MRGCINTERIOR = GantryAngle;
                                            lastcontigrealCouch = true;
                                            //System.Windows.Forms.MessageBox.Show("First couch collision   Angle: " + GantryAngle);
                                        }
                                        else if ((MRGCINTERIOR >= GantryAngle - 13.0) & (MRGCINTERIOR <= GantryAngle + 13.0))
                                        {
                                            if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                            {
                                                // if at the end of the gantry angle list, 
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                                //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                            }
                                            else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                            {
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                                //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                            }
                                            else
                                            {
                                                // contiguous collisions, do not report
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGCINTERIOR, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = true, lastcontig = false, endoflist = false });
                                                MRGCINTERIOR = GantryAngle;
                                                lastcontigrealCouch = true;
                                                //System.Windows.Forms.MessageBox.Show("Couch: Within 15 degrees of last collision, meaning it is contiguous, don't report   Angle: " + GantryAngle);
                                            }
                                        }
                                        else
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
                                            MRGCINTERIOR = GantryAngle;
                                            lastcontigrealCouch = true;
                                            //System.Windows.Forms.MessageBox.Show("Couch: NOT Within 15 degrees of last collision, not contiguous, start of new collision area    Angle: " + GantryAngle);
                                        }
                                    }
                                    else if (INTERSECTDIST >= 50.0 & lastcontigrealCouch == true)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = Acc, contiguous = false, lastcontig = true, endoflist = false });
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
                                    System.Windows.Forms.MessageBox.Show("Invalid Triangle mesh indices");
                                }
                                else
                                {
                                    //  System.Windows.Forms.MessageBox.Show("Trigger 13");
                                    if (INTERSECTDIST <= 50.0)
                                    {
                                        //System.Windows.Forms.MessageBox.Show("PATBOX collision");

                                        if (MRGBBOARD == null)
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
                                            MRGBBOARD = GantryAngle;
                                            lastcontigrealBoard = true;
                                        }
                                        else if ((MRGBBOARD >= GantryAngle - 13.0) & (MRGBBOARD <= GantryAngle + 13.0))
                                        {
                                            if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                            {
                                                // if at the end of the gantry angle list, 
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                                //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                            }
                                            else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                            {
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                                //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                            }
                                            else
                                            {
                                                // contiguous collisions, do not report
                                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGBBOARD, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = true, lastcontig = false, endoflist = false });
                                                MRGBBOARD = GantryAngle;
                                                lastcontigrealBoard = true;
                                                // System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                            }
                                        }
                                        else
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
                                            MRGBBOARD = GantryAngle;
                                            lastcontigrealBoard = true;
                                        }
                                    }
                                    else if (INTERSECTDIST >= 50.0 & lastcontigrealBoard == true)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = Acc, contiguous = false, lastcontig = true, endoflist = false });
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
                                System.Windows.Forms.MessageBox.Show("Invalid Triangle mesh indices");
                            }
                            else
                            {
                                //  System.Windows.Forms.MessageBox.Show("Trigger 17");
                                if (INTERSECTDIST <= 50.0)
                                {
                                    // System.Windows.Forms.MessageBox.Show("PATBOX collision");
                                    if (MRGPATBOX == null)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
                                        MRGPATBOX = GantryAngle;
                                        lastcontigrealPATBOX = true;
                                        //System.Windows.Forms.MessageBox.Show("First patbox collision   Angle: " + GantryAngle);
                                        // System.Windows.Forms.MessageBox.Show("Trigger 18");
                                    }
                                    else if ((MRGPATBOX >= GantryAngle - 13.0) & (MRGPATBOX <= GantryAngle + 13.0))
                                    {
                                        if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                        {
                                            // if at the end of the gantry angle list, 
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else
                                        {
                                            // contiguous collisions, do not report
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGPATBOX, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = true, lastcontig = false, endoflist = false });
                                            MRGPATBOX = GantryAngle;
                                            lastcontigrealPATBOX = true;
                                            //System.Windows.Forms.MessageBox.Show("Patbox: Within 15 degrees of last collision, meaning it is contiguous, don't report   Angle: " + GantryAngle);
                                        }
                                        //  System.Windows.Forms.MessageBox.Show("Trigger 19");
                                        //System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                    }
                                    else
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = false, lastcontig = false, endoflist = false });
                                        MRGPATBOX = GantryAngle;
                                        lastcontigrealPATBOX = true;
                                        // System.Windows.Forms.MessageBox.Show("Trigger 21");
                                        //System.Windows.Forms.MessageBox.Show("Patbox: NOT Within 15 degrees of last collision, not contiguous, start of new collision area    Angle: " + GantryAngle);
                                    }
                                }
                                else if (INTERSECTDIST >= 50.0 & lastcontigrealPATBOX == true)
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = Acc, contiguous = false, lastcontig = true, endoflist = false });
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
                                 System.Windows.Forms.MessageBox.Show("Invalid Triangle mesh indices");
                            }
                            else
                            {
                                //  System.Windows.Forms.MessageBox.Show("Trigger 9");
                                if (INTERSECTDIST <= 50.0)
                                {
                                    //System.Windows.Forms.MessageBox.Show("Couch less than 50");
                                    if (MRGCINTERIOR == null)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGCINTERIOR = GantryAngle;
                                        lastcontigrealCouch = true;
                                        //System.Windows.Forms.MessageBox.Show("First couch collision   Angle: " + GantryAngle);
                                    }
                                    else if ((MRGCINTERIOR >= GantryAngle - 13.0) & (MRGCINTERIOR <= GantryAngle + 13.0))
                                    {
                                        if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                        {
                                            // if at the end of the gantry angle list, 
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else
                                        {
                                            // contiguous collisions, do not report
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGCINTERIOR, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = true, lastcontig = false, endoflist = false });
                                            MRGCINTERIOR = GantryAngle;
                                            lastcontigrealCouch = true;
                                            //System.Windows.Forms.MessageBox.Show("Couch: Within 15 degrees of last collision, meaning it is contiguous, don't report   Angle: " + GantryAngle);
                                        }
                                    }
                                    else
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGCINTERIOR = GantryAngle;
                                        lastcontigrealCouch = true;
                                        //System.Windows.Forms.MessageBox.Show("Couch: NOT Within 15 degrees of last collision, not contiguous, start of new collision area    Angle: " + GantryAngle);
                                    }
                                }
                                else if (INTERSECTDIST >= 50.0 & lastcontigrealCouch == true)
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Couch", GantryObject = "Gantry Head", contiguous = false, lastcontig = true, endoflist = false });
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
                                 System.Windows.Forms.MessageBox.Show("Invalid Triangle mesh indices");
                            }
                            else
                            {
                                //  System.Windows.Forms.MessageBox.Show("Trigger 13");
                                if (INTERSECTDIST <= 50.0)
                                {
                                    //System.Windows.Forms.MessageBox.Show("PATBOX collision");

                                    if (MRGBBOARD == null)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGBBOARD = GantryAngle;
                                        lastcontigrealBoard = true;
                                    }
                                    else if ((MRGBBOARD >= GantryAngle - 13.0) & (MRGBBOARD <= GantryAngle + 13.0))
                                    {
                                        if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                        {
                                            // if at the end of the gantry angle list, 
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                            //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                        }
                                        else
                                        {
                                            // contiguous collisions, do not report
                                            collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGBBOARD, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = true, lastcontig = false, endoflist = false });
                                            MRGBBOARD = GantryAngle;
                                            lastcontigrealBoard = true;
                                            // System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                        }
                                    }
                                    else
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
                                        MRGBBOARD = GantryAngle;
                                        lastcontigrealBoard = true;
                                    }
                                }
                                else if (INTERSECTDIST >= 50.0 & lastcontigrealBoard == true)
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Prone Breast Board", GantryObject = "Gantry Head", contiguous = false, lastcontig = true, endoflist = false });
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
                             System.Windows.Forms.MessageBox.Show("Invalid triangle mesh indices");
                        }
                        else
                        {
                            //  System.Windows.Forms.MessageBox.Show("Trigger 17");
                            if (INTERSECTDIST <= 50.0)
                            {
                                // System.Windows.Forms.MessageBox.Show("PATBOX collision");

                                if (MRGPATBOX == null)
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
                                    MRGPATBOX = GantryAngle;
                                    lastcontigrealPATBOX = true;
                                    //System.Windows.Forms.MessageBox.Show("First patbox collision   Angle: " + GantryAngle);
                                    // System.Windows.Forms.MessageBox.Show("Trigger 18");
                                }
                                else if ((MRGPATBOX >= GantryAngle - 13.0) & (MRGPATBOX <= GantryAngle + 13.0))
                                {
                                    if (cp == true & ((GAngleList.Count - gantrylistCNT) <= 3))
                                    {
                                        // if at the end of the gantry angle list, 
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                        //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                    }
                                    else if (cp == false & ((GAngleList.Count - gantrylistCNT) <= 4))
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = true });
                                        //System.Windows.Forms.MessageBox.Show("Couch: End of gantry angle list   Angle: " + GantryAngle);
                                    }
                                    else
                                    {
                                        // contiguous collisions, do not report
                                        collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round((double)MRGPATBOX, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = true, lastcontig = false, endoflist = false });
                                        MRGPATBOX = GantryAngle;
                                        lastcontigrealPATBOX = true;
                                        //System.Windows.Forms.MessageBox.Show("Patbox: Within 15 degrees of last collision, meaning it is contiguous, don't report   Angle: " + GantryAngle);
                                    }
                                    //  System.Windows.Forms.MessageBox.Show("Trigger 19");
                                    //System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                }
                                else
                                {
                                    collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = false, lastcontig = false, endoflist = false });
                                    MRGPATBOX = GantryAngle;
                                    lastcontigrealPATBOX = true;
                                    // System.Windows.Forms.MessageBox.Show("Trigger 21");
                                    //System.Windows.Forms.MessageBox.Show("Patbox: NOT Within 15 degrees of last collision, not contiguous, start of new collision area    Angle: " + GantryAngle);
                                }
                            }
                            else if (INTERSECTDIST >= 50.0 & lastcontigrealPATBOX == true)
                            {
                                collist.Add(new CollisionAlert { beam = beam.beamId, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), rotationdirection = beam.gantrydirection, couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(INTERSECTDIST, 0, MidpointRounding.AwayFromZero), PatObject = "Patient Bounding Box", GantryObject = "Gantry Head", contiguous = false, lastcontig = true, endoflist = false });
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

            // this writes out the "composite" STL file of each beam. It is put on Therapy physics so everyone can access it. The GUI then uses this to display a picture of each beam. 
            
              IOWriteResult EVERY = StandardMeshWriter.WriteFile(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\" + beam.patientId + "_" + beam.courseId + "_" + beam.planId + "_" + "Beam_" + beam.beamId + ".stl", tempbeam, WriteOptions.Defaults); // Lahey
              //IOWriteResult EVERY = StandardMeshWriter.WriteFile(@"\\shceclipseimg\PHYSICS\New File Structure PHYSICS\Script Reports\Collision_Check_STL_files\" + beam.patientId + "_" + beam.courseId + "_" + beam.planId + "_" + "Beam_" + beam.beamId + ".stl", tempbeam, WriteOptions.Defaults); // Winchester

            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Beam " + beam.beamId + " analysis complete.");

            return collist;

        } // END OF BEAM COLLISION ANALYSIS       
    }
}
