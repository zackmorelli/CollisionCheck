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
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using CollisionCheck;
using _3DRender;



/*
    Collision Check - Main program
    Copyright (c) 2019 Radiation Oncology Department, Lahey Hospital and Medical Center
    Written by Zack Morelli

    This program is expressely written as a plug-in script for use with Varian's Eclipse Treatment Planning System, and requires Varian's API files to run properly.
    This program also requires .NET Framework 4.5.0 to run properly.

    //  all linear dimensions are expressed in millimeters and all angles are expressed in degrees. 
    // This includes the API's internal vector objects and the positions it reports of various objects

    Description: This is the main program that is called when Collision Check is run. It includes a GUI, which is called from here.
*/

namespace VMS.TPS
{

    public class Script  // creates a class called Script within the VMS.TPS Namesapce
    {

        public Script() { }  // instantiates a Script class


        // Global Variable Declaration

        public static double pmaxx = 0.0;
        public static double pminx = 0.0;
        public static double pmaxy = 0.0;
        public static double pminy = 0.0;
        public static double pmaxz = 0.0;
        public static double pminz = 0.0;
        public static double pbmaxz = 0.0;
        public static double pbminz = 5000.0;    // set to a high value to make sure it gets reset. pbminz may be positive.

        public static DMesh3 PBodyContour = new DMesh3();
        public static DMeshAABBTree3 PBodyContourSpatial;

        public static DMesh3 PCouchsurf = new DMesh3();
        public static DMeshAABBTree3 CouchSurfSpatial;

        public static DMesh3 PCouchInterior = new DMesh3();
        public static DMeshAABBTree3 PCouchInteriorSpatial;

        public static DMesh3 PProne_Brst_Board = new DMesh3();
        public static DMeshAABBTree3 PProne_Brst_BoardSpatial;

        public static DMesh3 PATBOX = new DMesh3();
        public static DMeshAABBTree3 spatial;


        // Declaration space for all the functions which make up the program.
        // Execution begins with the "Execute" function.

        // Thread Prog = new Thread(Script());

        // gantry head 77 cm wide

        // 41.5 cm distance from iso to gantry head??

        // not currently used, but still here in case we want to revert to something similiar


        // this is a method used to check that the .STL files used in this program are okay before being opened

        protected static bool IsFileLocked(FileInfo file)
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
                System.Windows.Forms.MessageBox.Show("IOException occured when attempting to open file " + file.DirectoryName + ". This file is either 1) Still being written to. 2) Being processed by another thread. 3) Does not exist." + Environment.NewLine + Environment.NewLine + "Source: " + e.Source + Environment.NewLine + "Message: " + e.Message + Environment.NewLine + "Stack Trace: " + e.StackTrace);

                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }


        public double CollisionAnalysis(ControlPointCollection PC)
        {
            double clearance = 0.0;     // in CM !!!!!

            if (PC[0].PatientSupportAngle >= 10.0 & PC[0].PatientSupportAngle <= 90.0)
            {
                // assuming couch is at 90 degrees, simple case to start. assuming the gantry angle is greater than 0 degrees (in IEC scale).

                clearance = 56.5 * Math.Sin(((90.0 - (PC[PC.Count - 1].GantryAngle + 45.0)) * Math.PI) / 180.0);       // includes conversion to radians
            }
            else if (PC[0].PatientSupportAngle <= 350.0 & PC[0].PatientSupportAngle >= 270.0)
            {
                clearance = 56.5 * Math.Sin(((90.0 - ((360.0 - PC[PC.Count - 1].GantryAngle) + 45.0)) * Math.PI) / 180.0);

            }

            // clearance represents vertical distance from far edge of gantry head to Iso plane

            return clearance;      // in CM !!!
        }

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

  

       

    

        public void Execute(ScriptContext context)     // PROGRAM START - sending a return to Execute will end the program
        {
            //Variable declaration space

            IEnumerable<PlanSetup> Plans = context.PlansInScope;
            Patient patient = context.Patient;
            Image image = context.Image;

            // extremes of the AAABBTree used to help identify false collosion alerts



            // start of actual code

            //  MessageBox.Show("Trig 1");
            if (context.Patient == null)
            {
                System.Windows.Forms.MessageBox.Show("Please load a patient with a treatment plan before running this script!");
                return;
            }

            System.Windows.Forms.Application.EnableVisualStyles();

            //Starts GUI 
            System.Windows.Forms.Application.Run(new GUI(Plans, patient, image));

            return;
        }

        public class CollisionAlert 
        {
            public string beam { get; set; }

            public int controlpoint { get; set; }

            public double gantryangle {get; set;}

            public double couchangle { get; set; }

            public double distance { get; set; }

            public string type { get; set; }   // this specifies the mesh being checked against diskgantry for collision (PATBOX, body contour, breast board, etc.)

            public Vector3d Gantrypoint { get; set; }
           
            public VVector Patpoint { get; set; }

            public string edgeclip { get; set; }

            public bool pbodyalert { get; set; }

            public bool contiguous { get; set; }

            public bool lastcontig { get; set; }
            
            public CollisionAlert()
            {
                lastcontig = false;
            }


        }

        public static void BOXMAKER (string PATEINTORIENTATION, bool findCouchInterior, bool findProneBrstBoard, Structure Body, Structure CouchInterior, Structure Prone_Brst_Board, string bodyloc, double ht, double uXISOshift, double uYISOshift, double uZISOshift, TextBox ProgOutput, PlanSetup plan, Image image, List<WriteMesh> tempbeam)
        {

            ht = ht * 10.0;    //convert from cm to mm.   IT is in mm

            Vector3d PatOrientRotCenter = new Vector3d(0, 0, 0);
            Quaterniond PatOrientRot = new Quaterniond();
            Vector3d ZaxisPatOrientRot = new Vector3d(0, 0, 1);

            // makes mesh out of patient body contour
            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Building Body Contour mesh... ");

            List<Vector3d> pvl = new List<Vector3d>();
            Vector3d pv = new Vector3d();

            List<Index3i> ptl = new List<Index3i>();
            Index3i pt = new Index3i();
            int ptmcount = 0;
            int rem = 0;

            foreach (Point3D pm in Body.MeshGeometry.Positions)
            {
                pv.x = pm.X;
                pv.y = pm.Y;
                pv.z = pm.Z;

                pvl.Add(pv);
            }

            foreach(Int32 ptm in Body.MeshGeometry.TriangleIndices)
            {
                ptmcount++;
                Math.DivRem(ptmcount, 3, out rem);

                if (rem == 2)
                {
                    pt.a = ptm;
                }
                else if(rem == 1)
                {
                    pt.b = ptm;
                }
                else if(rem == 0)
                {
                    pt.c = ptm;
                    ptl.Add(pt);
                }
            }

            PBodyContour = new DMesh3(MeshComponents.VertexNormals);
            for (int i = 0; i < pvl.Count; i++)
            {
                PBodyContour.AppendVertex(new NewVertexInfo(pvl[i]));
            }

            for (int i = 0; i < ptl.Count; i++)
            {
                PBodyContour.AppendTriangle(ptl[i]);
            }

            if(PATEINTORIENTATION == "HeadFirstProne" || PATEINTORIENTATION == "FeetFirstProne")
            {
                PatOrientRotCenter = MeshMeasurements.Centroid(PBodyContour);
                PatOrientRot = new Quaterniond(ZaxisPatOrientRot, 180.0);
                MeshTransforms.Rotate(PBodyContour, PatOrientRotCenter, PatOrientRot);
            }

            // need to move patient stuff to user origin
            // negative to move up in the y, positive to move down in the y
            // if iso is below user origin (y is less than), then need to move up

            if(uXISOshift != 0.0 || uYISOshift != 0.0 || uZISOshift != 0.0)
            {
                MeshTransforms.Translate(PBodyContour, uXISOshift, uYISOshift, uZISOshift);
            }

            //IOWriteResult result24 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\PBODY.stl", new List<WriteMesh>() { new WriteMesh(PBodyContour) }, WriteOptions.Defaults);

            tempbeam.Add(new WriteMesh(PBodyContour));

            PBodyContourSpatial = new DMeshAABBTree3(PBodyContour);
            PBodyContourSpatial.Build();

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

            //    if (uXISOshift != 0.0 || uYISOshift != 0.0 || uZISOshift != 0.0)
            //    {
            //        MeshTransforms.Translate(PCouchsurf, uXISOshift, -uYISOshift, uZISOshift);
            //    }


            //   // IOWriteResult result31 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\CouchSurface.stl", new List<WriteMesh>() { new WriteMesh(PCouchsurf) }, WriteOptions.Defaults);
            //    tempbeam.Add(new WriteMesh(PCouchsurf));

            //    CouchSurfSpatial = new DMeshAABBTree3(PCouchsurf);
            //    CouchSurfSpatial.Build();
            //}


            if (findCouchInterior == true)
            {
                // ------------------------------------------------------- makes mesh out of Couch interior
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Building Couch interior mesh... ");

                List<Vector3d> cipvl = new List<Vector3d>();
                Vector3d cipv = new Vector3d();

                List<Index3i> ciptl = new List<Index3i>();
                Index3i cipt = new Index3i();
                int ciptmcount = 0;

                foreach (Point3D pm in CouchInterior.MeshGeometry.Positions)
                {
                    cipv.x = pm.X;
                    cipv.y = pm.Y;
                    cipv.z = pm.Z;

                    cipvl.Add(cipv);
                }

                foreach (Int32 ptm in CouchInterior.MeshGeometry.TriangleIndices)
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
                for (int i = 0; i < cipvl.Count; i++)
                {
                    PCouchInterior.AppendVertex(new NewVertexInfo(cipvl[i]));
                }

                for (int i = 0; i < ciptl.Count; i++)
                {
                    PCouchInterior.AppendTriangle(ciptl[i]);
                }

                if (PATEINTORIENTATION == "HeadFirstProne" || PATEINTORIENTATION == "FeetFirstProne")
                {
                    PatOrientRotCenter = MeshMeasurements.Centroid(PBodyContour);
                    PatOrientRot = new Quaterniond(ZaxisPatOrientRot, 180.0);
                    MeshTransforms.Rotate(PCouchInterior, PatOrientRotCenter, PatOrientRot);
                }

                if (uXISOshift != 0.0 || uYISOshift != 0.0 || uZISOshift != 0.0)
                {
                    MeshTransforms.Translate(PCouchInterior, uXISOshift, -uYISOshift, uZISOshift);
                }

               // IOWriteResult result30 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\CouchInterior.stl", new List<WriteMesh>() { new WriteMesh(PCouchInterior) }, WriteOptions.Defaults);
                tempbeam.Add(new WriteMesh(PCouchInterior));

                PCouchInteriorSpatial = new DMeshAABBTree3(PCouchInterior);
                PCouchInteriorSpatial.Build();
            }

            if (findProneBrstBoard == true)
            {
                // ------------------------------------------------------- makes mesh out of Prone Breast Board
                ProgOutput.AppendText(Environment.NewLine);
                ProgOutput.AppendText("Building Prone Breast Board mesh... ");

                List<Vector3d> bbpvl = new List<Vector3d>();
                Vector3d bbpv = new Vector3d();

                List<Index3i> bbptl = new List<Index3i>();
                Index3i bbpt = new Index3i();
                int bbptmcount = 0;

                foreach (Point3D pm in Prone_Brst_Board.MeshGeometry.Positions)
                {
                    bbpv.x = pm.X;
                    bbpv.y = pm.Y;
                    bbpv.z = pm.Z;

                    bbpvl.Add(bbpv);
                }

                foreach (Int32 ptm in Prone_Brst_Board.MeshGeometry.TriangleIndices)
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
                for (int i = 0; i < bbpvl.Count; i++)
                {
                    PProne_Brst_Board.AppendVertex(new NewVertexInfo(bbpvl[i]));
                }

                for (int i = 0; i < bbptl.Count; i++)
                {
                    PProne_Brst_Board.AppendTriangle(bbptl[i]);
                }

                if (PATEINTORIENTATION == "HeadFirstProne" || PATEINTORIENTATION == "FeetFirstProne")
                {
                    PatOrientRotCenter = MeshMeasurements.Centroid(PBodyContour);
                    PatOrientRot = new Quaterniond(ZaxisPatOrientRot, 180.0);
                    MeshTransforms.Rotate(PProne_Brst_Board, PatOrientRotCenter, PatOrientRot);
                }

                if (uXISOshift != 0.0 || uYISOshift != 0.0 || uZISOshift != 0.0)
                {
                    MeshTransforms.Translate(PProne_Brst_Board, uXISOshift, -uYISOshift, uZISOshift);
                }

              //  IOWriteResult result36 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\ProneBreastBoard.stl", new List<WriteMesh>() { new WriteMesh(PProne_Brst_Board) }, WriteOptions.Defaults);
                tempbeam.Add(new WriteMesh(PProne_Brst_Board));


                PProne_Brst_BoardSpatial = new DMeshAABBTree3(PProne_Brst_Board);
                PProne_Brst_BoardSpatial.Build();
            }


            Rect3D BOX = Body.MeshGeometry.Bounds;  // retrieves bounding box of the body contour

           // MessageBox.Show("body structure center point is: (" + Body.CenterPoint.x + " ," + Body.CenterPoint.y + " ," + Body.CenterPoint.z + ")");

          //   VVector dicomvec = image.DicomToUser(Body.CenterPoint, plan);

             // MessageBox.Show("body structure center point is at USER: (" + dicomvec.x + " ," + dicomvec.y + " ," + dicomvec.z + ")");


            // THIS CODE SNIPPET BELOW WILL ROTATE THE CENTER POINT OF THE BOUNDING BOX SO THAT THE PATBOX WILL BE LOCATED WHERE THE TABLE AND PATIENT ARE FOR THAT BEAM BASED ON THE COUCH ANGLE

            /*
            double ANgle = 0.0;
            double patboxxprime = 0.0;
            double patboxxzprime = 0.0;

            if (CouchAngle >= 270.0 & CouchAngle <= 360.0)
            {
                //  rotated to the right - counterclockwise rotation on xz plane, positive theta
                ANgle = 360.0 - CouchAngle;
                patboxxprime = ((Body.CenterPoint.x * Math.Cos(((ANgle * Math.PI) / 180.0))) - (Body.CenterPoint.z * Math.Sin(((ANgle * Math.PI) / 180.0))));
                patboxxzprime = ((Body.CenterPoint.x * Math.Sin(((ANgle * Math.PI) / 180.0))) + (Body.CenterPoint.z * Math.Cos(((ANgle * Math.PI) / 180.0))));
            }
            else if (CouchAngle >= 0.0 & CouchAngle <= 90.0)
            {
                // rotated to the left - clockwise rotation on xz plane, negative theta
                ANgle = -CouchAngle;
                patboxxprime = ((Body.CenterPoint.x * Math.Cos(((ANgle * Math.PI) / 180.0))) - (Body.CenterPoint.z * Math.Sin(((ANgle * Math.PI) / 180.0))));
                patboxxzprime = ((Body.CenterPoint.x * Math.Sin(((ANgle * Math.PI) / 180.0))) + (Body.CenterPoint.z * Math.Cos(((ANgle * Math.PI) / 180.0))));
            }
            */

            //  MessageBox.Show("body structure center point is at DICOM: (" + Body.CenterPoint.x + " ," + Body.CenterPoint.y + " ," + Body.CenterPoint.z + ")");

            // VVector dicomvec = image.DicomToUser(Body.CenterPoint, plan);

            //  MessageBox.Show("body structure center point is at USER: (" + dicomvec.x + " ," + dicomvec.y + " ," + dicomvec.z + ")");

            // MessageBox.Show("PATBOX origin (top Right Corner) is at DICOM: (" + BOX.X + " ," + BOX.Y + " ," + BOX.Z + ")");
            //  MessageBox.Show("PATBOX origin (top Right Corner) is at USER: (" + BOX.X + " ," + BOX.Y + " ," + BOX.Z + ")");

            //  MessageBox.Show("PATBOX SIZE is: (" + BOX.SizeX + " ," + BOX.SizeY + " ," + BOX.SizeZ + ")");

            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Building extended patient bounding box ...");

           // MessageBox.Show("ht is: " + ht);


            double LT = BOX.SizeZ;
           // MessageBox.Show("LT is: " + LT);

            double headdownshift = ht - LT;
            double thoraxupshift = (ht - LT) * 0.24;
            double thoraxdownshift = (ht - LT) * 0.78;
            double abdomenupshift = (ht - LT) * 0.35;
            double abdomendownshift = (ht - LT) * 0.7;
            double pelvisupshift = (ht - LT) * 0.55;
            double pelvisdownshift = (ht - LT) * 0.55;
            double legsupshift = (ht - LT) * 0.75;
            double legsdownshift = (ht - LT) * 0.31;

            // MessageBox.Show("headdownshift is: " + headdownshift);

            // find the 8 corners of the Rect3D, use them to make a separate mesh. use Body.CenterPoint as origin to maintain coordinate system
            double patbxshift = BOX.SizeX / 2.0;
            double patbyshift = BOX.SizeY / 2.0;  
            double patbzshift = BOX.SizeZ / 2.0;  

            //need to to box extension to cover entire patient !!!!!!!!!!!!!!!

            List<Vector3d> vertices = new List<Vector3d>();
            List<Index3i> triangles = new List<Index3i>();
            // each triangle is simply a struct of 3 ints which are indices referring to the vertices which make up that triangle
            // in other words, a triangle is a collection of 3 vertices, and it is just composed of indices referencing the vertices

            Vector3d vect = new Vector3d();

            Vector3d centerofforwardface = new Vector3d(Body.CenterPoint.x, Body.CenterPoint.y, Body.CenterPoint.z + patbzshift);
            Vector3d centerofdownwardface = new Vector3d(Body.CenterPoint.x, Body.CenterPoint.y, Body.CenterPoint.z - patbzshift);

          //  MessageBox.Show("Center of downward face (y) (before shift): " + centerofdownwardface.y);

            if (bodyloc == "Head")
            {
                centerofdownwardface.z = centerofdownwardface.z - headdownshift;
              //  MessageBox.Show("Center of downward face (y) (after shift): " + centerofdownwardface.y);


            }
            else if (bodyloc == "Thorax")
            {
                centerofforwardface.z = centerofforwardface.z + thoraxupshift;
                centerofdownwardface.z = centerofdownwardface.z - thoraxdownshift;
            }
            else if (bodyloc == "Abdomen")
            {
                centerofforwardface.z = centerofforwardface.z + abdomenupshift;
                centerofdownwardface.z = centerofdownwardface.z - abdomendownshift;
            }
            else if (bodyloc == "Pelvis")
            {
                centerofforwardface.z = centerofforwardface.z + pelvisupshift;
                centerofdownwardface.z = centerofdownwardface.z - pelvisdownshift;
            }
            else if (bodyloc == "Legs")
            {
                centerofforwardface.z = centerofforwardface.z + legsupshift;
                centerofdownwardface.z = centerofdownwardface.z - legsdownshift;
            }

            // MessageBox.Show("Center of Forward Face (plane above head): (" + centerofforwardface.x + " ," + centerofforwardface.y + " ," + centerofforwardface.z + ")");

            Vector3d centeroftopface = new Vector3d(Body.CenterPoint.x, Body.CenterPoint.y - patbyshift, Body.CenterPoint.z);
            Vector3d centerofbottomface = new Vector3d(Body.CenterPoint.x, Body.CenterPoint.y + patbyshift, Body.CenterPoint.z);
            Vector3d centerofrightface = new Vector3d(Body.CenterPoint.x + patbxshift, Body.CenterPoint.y, Body.CenterPoint.z);
            Vector3d centerofleftface = new Vector3d(Body.CenterPoint.x - patbxshift, Body.CenterPoint.y, Body.CenterPoint.z);

            vertices.Add(centeroftopface);         // 0
            vertices.Add(centerofbottomface);      //  1
            vertices.Add(centerofrightface);        // 2
            vertices.Add(centerofleftface);         // ...
            vertices.Add(centerofforwardface);
            vertices.Add(centerofdownwardface);

            // top upper right corner
            vect.x = Body.CenterPoint.x + patbxshift;
            if (bodyloc == "Head")
            {
                vect.z = Body.CenterPoint.z + patbzshift;
            }
            else if (bodyloc == "Thorax")
            {
                vect.z = Body.CenterPoint.z + patbzshift + thoraxupshift;
            }
            else if (bodyloc == "Abdomen")
            {
                vect.z = Body.CenterPoint.z + patbzshift + abdomenupshift;
            }
            else if (bodyloc == "Pelvis")
            {
                vect.z = Body.CenterPoint.z + patbzshift + pelvisupshift;
            }
            else if (bodyloc == "Legs")
            {
                vect.z = Body.CenterPoint.z + patbzshift + legsupshift;
            }

            vect.y = Body.CenterPoint.y - patbyshift;
            vertices.Add(vect);
            Vector3d tur = new Vector3d(vect.x, vect.y, vect.z);

            // top upper left corner
            vect.x = Body.CenterPoint.x - patbxshift;
            if (bodyloc == "Head")
            {
                vect.z = Body.CenterPoint.z + patbzshift;
            }
            else if (bodyloc == "Thorax")
            {
                vect.z = Body.CenterPoint.z + patbzshift + thoraxupshift;
            }
            else if (bodyloc == "Abdomen")
            {
                vect.z = Body.CenterPoint.z + patbzshift + abdomenupshift;
            }
            else if (bodyloc == "Pelvis")
            {
                vect.z = Body.CenterPoint.z + patbzshift + pelvisupshift;
            }
            else if (bodyloc == "Legs")
            {
                vect.z = Body.CenterPoint.z + patbzshift + legsupshift;
            }

            vect.y = Body.CenterPoint.y - patbyshift;
            vertices.Add(vect);
            Vector3d tul = new Vector3d(vect.x, vect.y, vect.z);

            // top bottom right corner
            vect.x = Body.CenterPoint.x + patbxshift;
            if (bodyloc == "Head")
            {
                vect.z = Body.CenterPoint.z - patbzshift - headdownshift;
            }
            else if (bodyloc == "Thorax")
            {
                vect.z = Body.CenterPoint.z - patbzshift - thoraxdownshift;
            }
            else if (bodyloc == "Abdomen")
            {
                vect.z = Body.CenterPoint.z - patbzshift - abdomendownshift;
            }
            else if (bodyloc == "Pelvis")
            {
                vect.z = Body.CenterPoint.z - patbzshift - pelvisdownshift;
            }
            else if (bodyloc == "Legs")
            {
                vect.z = Body.CenterPoint.z - patbzshift - legsdownshift;
            }
            vect.y = Body.CenterPoint.y - patbyshift;
            vertices.Add(vect);
            Vector3d tbr = new Vector3d(vect.x, vect.y, vect.z);

            // top bottom left corner
            vect.x = Body.CenterPoint.x - patbxshift;
            if (bodyloc == "Head")
            {
                vect.z = Body.CenterPoint.z - patbzshift - headdownshift;
            }
            else if (bodyloc == "Thorax")
            {
                vect.z = Body.CenterPoint.z - patbzshift - thoraxdownshift;
            }
            else if (bodyloc == "Abdomen")
            {
                vect.z = Body.CenterPoint.z - patbzshift - abdomendownshift;
            }
            else if (bodyloc == "Pelvis")
            {
                vect.z = Body.CenterPoint.z - patbzshift - pelvisdownshift;
            }
            else if (bodyloc == "Legs")
            {
                vect.z = Body.CenterPoint.z - patbzshift - legsdownshift;
            }

            vect.y = Body.CenterPoint.y - patbyshift;
            vertices.Add(vect);
            Vector3d tbl = new Vector3d(vect.x, vect.y, vect.z);

            // lower upper right corner
            vect.x = Body.CenterPoint.x + patbxshift;
            if (bodyloc == "Head")
            {
                vect.z = Body.CenterPoint.z + patbzshift;
            }
            else if (bodyloc == "Thorax")
            {
                vect.z = Body.CenterPoint.z + patbzshift + thoraxupshift;
            }
            else if (bodyloc == "Abdomen")
            {
                vect.z = Body.CenterPoint.z + patbzshift + abdomenupshift;
            }
            else if (bodyloc == "Pelvis")
            {
                vect.z = Body.CenterPoint.z + patbzshift + pelvisupshift;
            }
            else if (bodyloc == "Legs")
            {
                vect.z = Body.CenterPoint.z + patbzshift + legsupshift;
            }

            vect.y = Body.CenterPoint.y + patbyshift;
            vertices.Add(vect);
            Vector3d lur = new Vector3d(vect.x, vect.y, vect.z);

            // lower upper left corner
            vect.x = Body.CenterPoint.x - patbxshift;
            if (bodyloc == "Head")
            {
                vect.z = Body.CenterPoint.z + patbzshift;
            }
            else if (bodyloc == "Thorax")
            {
                vect.z = Body.CenterPoint.z + patbzshift + thoraxupshift;
            }
            else if (bodyloc == "Abdomen")
            {
                vect.z = Body.CenterPoint.z + patbzshift + abdomenupshift;
            }
            else if (bodyloc == "Pelvis")
            {
                vect.z = Body.CenterPoint.z + patbzshift + pelvisupshift;
            }
            else if (bodyloc == "Legs")
            {
                vect.z = Body.CenterPoint.z + patbzshift + legsupshift;
            }

            vect.y = Body.CenterPoint.y + patbyshift;
            vertices.Add(vect);
            Vector3d lul = new Vector3d(vect.x, vect.y, vect.z);

            // lower bottom right corner
            vect.x = Body.CenterPoint.x + patbxshift;
            if (bodyloc == "Head")
            {
                vect.z = Body.CenterPoint.z - patbzshift - headdownshift;
            }
            else if (bodyloc == "Thorax")
            {
                vect.z = Body.CenterPoint.z - patbzshift - thoraxdownshift;
            }
            else if (bodyloc == "Abdomen")
            {
                vect.z = Body.CenterPoint.z - patbzshift - abdomendownshift;
            }
            else if (bodyloc == "Pelvis")
            {
                vect.z = Body.CenterPoint.z - patbzshift - pelvisdownshift;
            }
            else if (bodyloc == "Legs")
            {
                vect.z = Body.CenterPoint.z - patbzshift - legsdownshift;
            }

            vect.y = Body.CenterPoint.y + patbyshift;
            vertices.Add(vect);
            Vector3d lbr = new Vector3d(vect.x, vect.y, vect.z);

            // lower bottom leftcorner
            vect.x = Body.CenterPoint.x - patbxshift;
            if (bodyloc == "Head")
            {
                vect.z = Body.CenterPoint.z - patbzshift - headdownshift;
            }
            else if (bodyloc == "Thorax")
            {
                vect.z = Body.CenterPoint.z - patbzshift - thoraxdownshift;
            }
            else if (bodyloc == "Abdomen")
            {
                vect.z = Body.CenterPoint.z - patbzshift - abdomendownshift;
            }
            else if (bodyloc == "Pelvis")
            {
                vect.z = Body.CenterPoint.z - patbzshift - pelvisdownshift;
            }
            else if (bodyloc == "Legs")
            {
                vect.z = Body.CenterPoint.z - patbzshift - legsdownshift;
            }

            vect.y = Body.CenterPoint.y + patbyshift;
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

            //  DMesh3 PATBOX2 = DMesh3Builder.Build(IEnumerable<Vector3d> vertices, triangles);

            //PATBOX.CheckValidity();

            //IOWriteResult result = StandardMeshWriter.WriteFile(@"\\Wvvrnimbp01ss\va_data$\filedata\ProgramData\Vision\PublishedScripts\PATBOX.stl", new List<WriteMesh>() { new WriteMesh(PATBOX) }, WriteOptions.Defaults);

            //IOWriteResult result2 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\PATBOX.stl", new List<WriteMesh>() { new WriteMesh(PATBOX) }, WriteOptions.Defaults);

            DMesh3 PATBOXCOPY = PATBOX;

            // use the remesher to add triangles/vertices to mesh based off of the simple box mesh

            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("Remeshing patient bounding box... ");

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


            // Remeshing settings aren't perfect, bu they are fairly dialed in

            if (PATEINTORIENTATION == "HeadFirstProne" || PATEINTORIENTATION == "FeetFirstProne")
            {
                PatOrientRotCenter = MeshMeasurements.Centroid(PATBOX);
                PatOrientRot = new Quaterniond(ZaxisPatOrientRot, 180.0);
                MeshTransforms.Rotate(PATBOX, PatOrientRotCenter, PatOrientRot);
            }

            if (uXISOshift != 0.0 || uYISOshift != 0.0 || uZISOshift != 0.0)
            {
                MeshTransforms.Translate(PATBOX, uXISOshift, uYISOshift, uZISOshift);
            }


            // we apply Isocenter shifts to the square gantry model to ensure it is centered at (0,0,0) of the eclipse coordinate system, i.e. we account for the shifts to the user origin from the CT scan DICOM coordinates
            // but sometimes, for some plans, the User origin (0,0,0) in the Eclipse coordiante system is not the Isocenter of the beam
            // I think the actual isocenter of a beam is given by what ESAPI calls the Field reference point.
            // I can align the gantry model to the correct iso of the beam by performing a simple translation based off the difference in position of the UserOrigin and the FieldReferencePoint
            // probably is a real life setup thing for prone breast plans for example


           // IOWriteResult result3 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\PATBOXremeshed.stl", new List<WriteMesh>() { new WriteMesh(PATBOX) }, WriteOptions.Defaults);
            tempbeam.Add(new WriteMesh(PATBOX));

            spatial = new DMeshAABBTree3(PATBOX);
            spatial.Build();

           // return spatial;
        }

        // START OF COLLISION CHECK PROGRAM. BOXMAKER IS CALLED FROM WITHIN.--------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        public static List<CollisionAlert> CollisionCheck(PlanSetup plan, string bodyloc, double ht, TextBox ProgOutput, Image image, ProgressBar ProgBar)
        {
            // declaration space for outputs and things used between boxmaker and collision check

            double uXISOshift = 0.0;   // these aren't Iso shifts, they are patient object shifts, for when Iso and user origin are different
            double uYISOshift = 0.0;
            double uZISOshift = 0.0;

            List<CollisionAlert> collist = new List<CollisionAlert>();
            CollisionAlert colcomp = new CollisionAlert();
            string strctid;

            double INTERSECTDIST = 50.0;
            Index2i snear_tids = new Index2i(-1, -1);
            DistTriangle3Triangle3 STriDist;
            double ZABSDIST;
            double reportCang;

            //bool findCouchSurf = false;
            bool findCouchInterior = false;
            bool findProneBrstBoard = false;

            // Now we get the structures we need. Typical safety stuff. First, check if there is a structure set

            try
            {
                strctid = plan.StructureSet.Id;
            }
            catch (NullReferenceException e)
            {
                System.Windows.Forms.MessageBox.Show("The plan " + plan.Id + " does not have a structure set!");
                // no structure set, skip
            }

            // retrieves the body structure
            IEnumerator BR = plan.StructureSet.Structures.GetEnumerator();
            BR.MoveNext();
            BR.MoveNext();
            BR.MoveNext();

            Structure Body = (Structure)BR.Current;

            //IEnumerator CSR = plan.StructureSet.Structures.GetEnumerator();
            //CSR.MoveNext();
            //CSR.MoveNext();
            //CSR.MoveNext();

            //Structure CouchSurface = (Structure)CSR.Current;

            IEnumerator CIR = plan.StructureSet.Structures.GetEnumerator();
            CIR.MoveNext();
            CIR.MoveNext();
            CIR.MoveNext();

            Structure CouchInterior = (Structure)CIR.Current;

            IEnumerator CPR = plan.StructureSet.Structures.GetEnumerator();
            CPR.MoveNext();
            CPR.MoveNext();
            CPR.MoveNext();

            Structure Prone_Brst_Board = (Structure)CPR.Current;

            foreach (Structure STR in plan.StructureSet.Structures)
            { 
                if (STR.Id == "Body")
                {
                    Body = STR;
                }
                else if (STR.Id.Contains("CouchInterior") || STR.Id.Contains("couchinterior") || STR.Id.Contains("couch interior") || STR.Id.Contains("couch_interior") || STR.Id.Contains("Couch_Interior") || STR.Id.Contains("Couch Interior"))
                {
                    if (STR.IsEmpty == true || STR.Volume < 0.0)
                    {
                        System.Windows.Forms.MessageBox.Show("The Couch Interior structure is not contoured!");
                        continue;
                    }

                    CouchInterior = STR;
                    findCouchInterior = true;
                }
                else if (STR.Id.Contains("Prone_Brst_Board") || STR.Id.Contains("Prone_Bst_Brd") || STR.Id.Contains("Prone_Brst_Brd") || STR.Id.Contains("Prone_Bst_Board") || STR.Id.Contains("Prone Brst Board") || STR.Id.Contains("Prone Bst Brd") || STR.Id.Contains("Prone Brst Brd") || STR.Id.Contains("Prone Bst Board") || STR.Id.Contains("prone_brst_board") || STR.Id.Contains("prone_bst_brd") || STR.Id.Contains("prone_brst_brd") || STR.Id.Contains("prone_bst_board") || STR.Id.Contains("prone brst board") || STR.Id.Contains("prone bst brd") || STR.Id.Contains("pron brst brd") || STR.Id.Contains("prone bst board"))
                {
                    if (STR.IsEmpty == true || STR.Volume < 0.0)
                    {
                        System.Windows.Forms.MessageBox.Show("The Prone Breast Board structure is not contoured!");
                        continue;
                    }

                    Prone_Brst_Board = STR;
                    findProneBrstBoard = true;
                }

                //else if (STR.Id == "CouchSurface" || )
                //{
                //    CouchSurface = STR;
                //    findCouchSurf = true;
                //}

               // findCouchSurf = false;

            }

            string PATIENTORIENTATION = null;

            // Head first prone
            if(plan.TreatmentOrientation == PatientOrientation.HeadFirstSupine)
            {
                PATIENTORIENTATION = "HeadFirstSupine";
            }
            else if(plan.TreatmentOrientation == PatientOrientation.HeadFirstProne)
            {
                PATIENTORIENTATION = "HeadFirstProne";
            }
            else if (plan.TreatmentOrientation == PatientOrientation.FeetFirstSupine)
            {
                PATIENTORIENTATION = "FeetFirstSupine";
            }
            else if (plan.TreatmentOrientation == PatientOrientation.FeetFirstProne)
            {
                PATIENTORIENTATION = "FeetFirstProne";
            }

            // No correction needed for Feet First vs. Head First, but the 180 degree flip is needed for both Prone orientations vs. Supine (Program built off of HFS).


            // start of beam loop---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            foreach (Beam beam in plan.Beams)
            {   

                if(beam.IsSetupField == true )
                {
                    continue;
                }

                // ANGLES ARE IN DEGREES
                List<double> GAngleList = new List<double>();
                int gantrylistCNT = 0;
                ControlPointCollection PC = beam.ControlPoints;
                double CouchStartAngle = PC[0].PatientSupportAngle;
                double CouchEndAngle = PC[PC.Count - 1].PatientSupportAngle;            // count - 1 is the end becuase the index starts at 0
                List<WriteMesh> tempbeam = new List<WriteMesh>();

                double? MRGPATBOX = null;
                double? MRGBCONTOUR = null;
                double? MRGCSURFACE = null;
                double? MRGCINTERIOR = null;
                double? MRGBBOARD = null;
                bool lastcontigreal = false;
                

                if (CouchStartAngle != CouchEndAngle)
                {
                    System.Windows.Forms.MessageBox.Show("WARNING: The patient couch has a different rotation angle at the end of beam " + beam.Id + " in plan " + plan.Id + " than what the beam starts with.");
                    
                }
                else if (CouchStartAngle == CouchEndAngle)
                {

                     ProgOutput.AppendText(Environment.NewLine);
                     ProgOutput.AppendText("Starting analysis of beam " + beam.Id + " in plan " + plan.Id + ".");
                     // MessageBox.Show("Starting analysis of beam " + beam.Id + " in plan " + plan.Id + " .");

                    VVector ISO = beam.IsocenterPosition;
                    VVector UserOrigin = image.UserOrigin;
                    VVector Origin = image.Origin;

                    // need to move patient stuff to user origin
                    // negative to move up in the y, positive to move down in the y
                    // if iso is below user origin (y is less than), then need to move up

                    // This accounts for the UserOrigin not being at ISO.
                    if (ISO.Equals(UserOrigin) == false)
                    {
                        if(ISO.y < UserOrigin.y)
                        {
                            uYISOshift = -1 * (UserOrigin.y - ISO.y);
                        }
                        else if(ISO.y > UserOrigin.y)
                        {
                            uYISOshift = (ISO.y - UserOrigin.y);
                        }

                        if (ISO.x < UserOrigin.x)
                        {
                            uYISOshift =  (UserOrigin.x - ISO.x);
                        }
                        else if (ISO.x > UserOrigin.x)
                        {
                            uYISOshift = -1 * (ISO.x - UserOrigin.x);
                        }

                        if (ISO.z < UserOrigin.z)
                        {
                            uZISOshift = (UserOrigin.z - ISO.z);
                        }
                        else if (ISO.z > UserOrigin.z)
                        {
                            uZISOshift = -1 * (ISO.z - UserOrigin.z);
                        }

                    }

                 //    MessageBox.Show("uXISOshift is at: " + uXISOshift );
                  //   MessageBox.Show("uYISOshift point is at: " + uYISOshift);
                 //    MessageBox.Show("uZISOshift point is at: " + uZISOshift);


                    BOXMAKER(PATIENTORIENTATION, findCouchInterior, findProneBrstBoard, Body, CouchInterior, Prone_Brst_Board, bodyloc, ht, uXISOshift, uYISOshift, uZISOshift , ProgOutput, plan, image, tempbeam);

                   // MessageBox.Show("Isocenter point is at: (" + ISO.x + " ," + ISO.y + " ," + ISO.z + ")");
                   // MessageBox.Show("Image origin is at: (" + Origin.x + " ," + Origin.y + " ," + Origin.z + ")");
                   // MessageBox.Show("User Origin at: (" + UserOrigin.x + " ," + UserOrigin.y + " ," + UserOrigin.z + ")");


                    // source position creation
                    double myZ = 0.0;
                    double myX = 0.0;
                    double myY = 0.0;
                
                    double ANGLE = 0.0;
                    double Gangle = 0.0;
                    // double sourceINVERSExtrans = 0.0;
                    // double sourceINVERSEztrans = 0.0;

                    string gantryXISOshift = null;
                    // string gantryYISOshift = null;

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


                    if (beam.MLCPlanType == MLCPlanType.Static)
                    {
                        double GantryStartAngle = 500.0;
                        double GantryEndAngle = 500.0;
                        string ArcDirection = null;

                        ProgOutput.AppendText(Environment.NewLine);
                        ProgOutput.AppendText("This is a static MLC beam with no control points. Attempting to get Gantry information for this beam from the ARIA database (this might take a minute)... ");

                        System.Windows.Forms.MessageBox.Show("This is a static MLC beam with no control points. The program will get the gantry information it needs for this beam from the ARIA database.\nA blank terminal window will appear while it does this. A dialogue box will appear that will tell you that the program is busy because it is waiting for the other program to query the database.\nYou will have to click on 'switch to' several times until it is done. The GUI window will reappear when the program is finished.");

                        ProcessStartInfo processinfo = new ProcessStartInfo(@"\\wvvrnimbp01ss\va_data$\filedata\ProgramData\Vision\Stand-alone Programs\CollisionCheck_InfoRetrieval\CollisionCheck_InfoRetrieval.exe", plan.Course.Patient.Id + " " + plan.Course.Id + " " + beam.Id + " " + plan.Id);        // path name of the Collision retrieval program
                        processinfo.UseShellExecute = false;
                        processinfo.ErrorDialog = false;
                        processinfo.RedirectStandardOutput = true;

                        Process GantryAngleRetrieve = new Process();
                        GantryAngleRetrieve.StartInfo = processinfo;
                    
                        bool processstarted = GantryAngleRetrieve.Start();

                        StreamReader GantryAngleRetrieveOutput = GantryAngleRetrieve.StandardOutput;
                        GantryAngleRetrieve.WaitForExit();

                        ProgOutput.AppendText(Environment.NewLine);
                        ProgOutput.AppendText("Aria retrieval complete! Building list of gantry angles...");

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
                        if (beam.ArcLength == 0)
                        {
                            ProgOutput.AppendText(Environment.NewLine);
                            ProgOutput.AppendText("Static gantry IMRT beam. Retrieving gantry angle from first MLC control point ... ");
                            GAngleList.Add(PC.First().GantryAngle);
                        }
                        else
                        {
                            ProgOutput.AppendText(Environment.NewLine);
                            ProgOutput.AppendText("Moving gantry IMRT beam. Building list of gantry angles from control points ... ");

                            foreach (ControlPoint point in PC)
                            {
                                GAngleList.Add(point.GantryAngle);


                                //  if(PATIENTORIENTATION == "HeadFirstProne")
                                //  {
                                //      GantryAngle = point.GantryAngle - 180.0;
                                //  }

                                // Math.DivRem(point.Index, 6, out int res);

                                //if (point.Index == 1 || point.Index == PC.Count || res == 0)
                                //{

                                //   if (res == 0)
                                //  {
                                //      ProgOutput.AppendText(Environment.NewLine);
                                //     ProgOutput.AppendText("Control Point " + point.Index + "/" + PC.Count);
                                // }

                            }
                        }
                       // System.Windows.Forms.MessageBox.Show("Trigger 1");
                    }
                    // System.Windows.Forms.MessageBox.Show("Trigger 2");

                    ProgBar.Visible = true;
                    ProgBar.Minimum = 0;
                    ProgBar.Value = 1;
                    ProgBar.Step = 1;

                    if(GAngleList.Count <= 10)
                    {
                        ProgBar.Maximum = GAngleList.Count;
                    }
                    else
                    {
                        ProgBar.Maximum = (GAngleList.Count / 5);
                    }

                    foreach (double GantryAngle in GAngleList)
                    {
                        gantrylistCNT++;
                        // System.Windows.Forms.MessageBox.Show("Gantry ANGLE :  " + GantryAngle + "  ");
                        //  MessageBox.Show("couch ANGLE :  " + CouchEndAngle + "  ");
                        if (GAngleList.Count <= 10)
                        {
                            ProgOutput.AppendText(Environment.NewLine);
                            ProgOutput.AppendText("Gantry Angle: " + gantrylistCNT + "/" + GAngleList.Count);
                            ProgBar.PerformStep();
                        }
                        else
                        {
                            if (gantrylistCNT % 5 == 0)
                            {
                                ProgOutput.AppendText(Environment.NewLine);
                                ProgOutput.AppendText("Gantry Angle: " + gantrylistCNT + "/" + GAngleList.Count);
                                ProgBar.PerformStep();
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

                        VVector APISOURCE = beam.GetSourceLocation(GantryAngle);  // negative Y
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


                            myZ = ISO.z;
                            myX = 1000 * Math.Cos((((GantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.x;    // - 90 degrees is because the polar coordinate system has 0 degrees on the right side
                            myY = 1000 * Math.Sin((((-GantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.y;   // need negative because y axis is inverted
                                                                                                          // THIS WORKS!



                            /*

                                 if (PATIENTORIENTATION == "HeadFirstSupine")
                                 {

                                     myZ = ISO.z;
                                     myX = 1000 * Math.Cos((((GantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.x;    // - 90 degrees is because the polar coordinate system has 0 degrees on the right side
                                     myY = 1000 * Math.Sin((((-GantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.y;   // need negative because y axis is inverted
                                                                                                                      // THIS WORKS!

                                 }
                                 if (PATIENTORIENTATION == "HeadFirstProne")
                                 {
                                     ISO.x = -ISO.x;
                                     ISO.y = -ISO.y;
                                     myZ = ISO.z;
                                     myX = 1000 * Math.Cos((((GantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.x;    // - 90 degrees is because the polar coordinate system has 0 degrees on the right side
                                     myY = 1000 * Math.Sin((((GantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.y;   // need negative because y axis is inverted

                                 }

                         */


                            VVector mySOURCE = new VVector(myX, myY, myZ);

                            // MessageBox.Show("mySOURCE: (" + mySOURCE.x + " ," + mySOURCE.y + " ," + mySOURCE.z + ")");

                            /*
                                // SOURCE INVERSE COORDINATE SYSTEM TRANSFORMATION FOR COUCH ANGLE 
                                // keep in mind couch angles are flipped from what is displayed for a given beam in Eclipse
                                if (CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
                                {
                                    // this is really for 0 to 90 couch angle
                                    //  MessageBox.Show("REAL couch ANGLE :  " + realangle + "  ");
                                    MessageBox.Show("Trig couch 0 to 360");
                                    MessageBox.Show("CouchEndAngle: " + CouchEndAngle);

                                    ANGLE = 360.0 - CouchEndAngle;

                                    sourceINVERSExtrans = (SOURCE.x * Math.Cos(((-ANGLE * Math.PI) / 180.0))) - (SOURCE.z * Math.Sin(((-ANGLE * Math.PI) / 180.0)));
                                    MessageBox.Show("SourecInversextrans: " + sourceINVERSExtrans);
                                    sourceINVERSEztrans = (SOURCE.x * Math.Sin(((-ANGLE * Math.PI) / 180.0))) + (SOURCE.z * Math.Cos(((-ANGLE * Math.PI) / 180.0)));

                                }
                                else if (CouchEndAngle >= 0.0 & CouchEndAngle <= 90.0)
                                {
                                    ANGLE = CouchEndAngle;
                                    MessageBox.Show("CouchEndAngle: " + CouchEndAngle);

                                    MessageBox.Show("Trig couch 0 to 90");

                                    sourceINVERSExtrans = (SOURCE.x * Math.Cos(((ANGLE * Math.PI) / 180.0))) - (SOURCE.z * Math.Sin(((ANGLE * Math.PI) / 180.0)));
                                    MessageBox.Show("SourecInversextrans: " + sourceINVERSExtrans);
                                    sourceINVERSEztrans = (SOURCE.x * Math.Sin(((ANGLE * Math.PI) / 180.0))) + (SOURCE.z * Math.Cos(((ANGLE * Math.PI) / 180.0)));

                                }
                            APISOURCE.x = sourceINVERSExtrans;
                            APISOURCE.z = sourceINVERSEztrans;
                             */
                            //  VVector convSOURCE = plan.StructureSet.Image.DicomToUser(SOURCE, plan);
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

                            VVector gantrycenter = mySOURCE;    // this will represent the center of the gantry head's surface once the transforms below are performed
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

                            VVector origgantrycenter = gantrycenter;
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
                            VVector RIGHTEDGE = origgantrycenter;
                            VVector LEFTEDGE = origgantrycenter;
                            VVector BACKEDGE = origgantrycenter;
                            VVector FRONTEDGE = origgantrycenter;

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
                                // SUPERGANTRY.AppendTriangle(tri);
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

                            IOWriteResult result42 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\DiskGantry\diskgantry" + beam.Id + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);
                            tempbeam.Add(new WriteMesh(diskgantry));

                        // System.Windows.Forms.MessageBox.Show("Trigger 7");
                        // IOWriteResult result5 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\CollisionCheck\SquareGantry\Gantry" + beam.Id + GantryAngle + ".stl", new List<WriteMesh>() { new WriteMesh(GANTRY) }, WriteOptions.Defaults);

                        // MessageBox.Show("Trig8");

                        // MessageBox.Show("number of triangles: " + cbht);

                        // ProgOutput.AppendText(Environment.NewLine);
                        // ProgOutput.AppendText("Gantry mesh Construction Done");

                        //  DMesh3 PATBOX2 = DMesh3Builder.Build(IEnumerable<Vector3d> vertices, triangles);

                        //PATBOX.CheckValidity();

                        //IOWriteResult result = StandardMeshWriter.WriteFile(@"\\Wvvrnimbp01ss\va_data$\filedata\ProgramData\Vision\PublishedScripts\PATBOX.stl", new List<WriteMesh>() { new WriteMesh(PATBOX) }, WriteOptions.Defaults);

                        //  DMeshAABBTree3 GANTRYspatial = new DMeshAABBTree3(GANTRY);
                        //  GANTRYspatial.Build();
                       // System.Windows.Forms.MessageBox.Show("Trigger 1");
                        DMeshAABBTree3 diskgantryspatial = new DMeshAABBTree3(diskgantry);
                        diskgantryspatial.Build();
                        reportCang = 360.0 - CouchEndAngle;
                        if (reportCang == 360.0)
                        {
                            reportCang = 0.0;
                        }

                        //      MessageBox.Show("Trig3");
                        // string boxedgeflag = null;
                        // bool closebody = false;
                        // bool coli = true;

                        /*
                        snear_tids = diskgantryspatial.FindNearestTriangles(spatial, null, out INTERSECTDIST);
                        STriDist = MeshQueries.TrianglesDistance(diskgantry, snear_tids.a, PATBOX, snear_tids.b);
                        System.Windows.Forms.MessageBox.Show("Abs Distance method S: " + ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest));

                        Index2i near_tids = MeshQueries.FindNearestTriangles_LinearSearch(diskgantry, PATBOX, out INTERSECTDIST);

                        DistTriangle3Triangle3 TriDist = MeshQueries.TrianglesDistance(diskgantry, near_tids.a, PATBOX, near_tids.b);


                        System.Windows.Forms.MessageBox.Show("Abs Distance method S: " + ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest));

                        System.Windows.Forms.MessageBox.Show("Abs Distance method: " + ABSDISTANCE(TriDist.Triangle0Closest, TriDist.Triangle1Closest));
                        */

                       // System.Windows.Forms.MessageBox.Show("Trigger 2");

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

                       // System.Windows.Forms.MessageBox.Show("Trigger 6");
                        // couch interior collision check-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                        if (findCouchInterior == true)
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
                                    //System.Windows.Forms.MessageBox.Show("PATBOX collision");

                                    if (MRGPATBOX == null)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Interior", contiguous = false });
                                        MRGPATBOX = GantryAngle;
                                        lastcontigreal = false;
                                    }
                                    else if ((MRGPATBOX >= GantryAngle - 15.0) & (MRGPATBOX <= GantryAngle + 15.0))
                                    {
                                        // contiguous collisions, do not report
                                        collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round((double)MRGPATBOX, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Interior", contiguous = true });
                                        MRGPATBOX = GantryAngle;
                                        lastcontigreal = true;
                                        // System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                    }
                                    else
                                    {
                                        if (lastcontigreal == true)
                                        {
                                            // System.Windows.Forms.MessageBox.Show("Last contig fire");
                                            collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Interior", contiguous = false, lastcontig = true });
                                            MRGPATBOX = GantryAngle;
                                            lastcontigreal = false;
                                        }
                                        else
                                        {
                                            collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Interior", contiguous = false });
                                            MRGPATBOX = GantryAngle;
                                            lastcontigreal = false;
                                        }
                                    }
                                }
                                else if (lastcontigreal == true)
                                {
                                    collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Couch Interior", contiguous = false, lastcontig = true });
                                    lastcontigreal = false;
                                }
                            }
                        }
                        //  System.Windows.Forms.MessageBox.Show("Trigger 10");
                        //prone breast board collision check-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                        if (findProneBrstBoard == true)
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

                                    if (MRGPATBOX == null)
                                    {
                                            collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Prone Breast Board", contiguous = false });
                                            MRGPATBOX = GantryAngle;
                                            lastcontigreal = false;
                                    }
                                    else if ((MRGPATBOX >= GantryAngle - 15.0) & (MRGPATBOX <= GantryAngle + 15.0))
                                    {
                                            // contiguous collisions, do not report
                                            collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round((double)MRGPATBOX, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Prone Breast Board", contiguous = true });
                                            MRGPATBOX = GantryAngle;
                                            lastcontigreal = true;
                                            // System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                    }
                                    else
                                    {
                                        if (lastcontigreal == true)
                                        {
                                                // System.Windows.Forms.MessageBox.Show("Last contig fire");
                                                collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Prone Breast Board", contiguous = false, lastcontig = true });
                                                MRGPATBOX = GantryAngle;
                                                lastcontigreal = false;
                                        }
                                        else
                                        {
                                                collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Prone Breast Board", contiguous = false });
                                                MRGPATBOX = GantryAngle;
                                                lastcontigreal = false;
                                        }
                                    }
                                }
                                else if (lastcontigreal == true)
                                {
                                        collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Prone Breast Board", contiguous = false, lastcontig = true });
                                        lastcontigreal = false;
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
                                //    System.Windows.Forms.MessageBox.Show("Trigger 17");
                            if (ZABSDIST <= 50.0)
                            {
                                    // System.Windows.Forms.MessageBox.Show("PATBOX collision");

                                if (MRGPATBOX == null)
                                {
                                        collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = false });
                                        MRGPATBOX = GantryAngle;
                                        lastcontigreal = false;
                                   // System.Windows.Forms.MessageBox.Show("Trigger 18");
                                }
                                else if ((MRGPATBOX >= GantryAngle - 15.0) & (MRGPATBOX <= GantryAngle + 15.0))
                                {
                                    if (GAngleList.Count - gantrylistCNT < 5)
                                    {
                                        // if at the end of the gantry angle list, 
                                        collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GAngleList.Last(), 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = false, lastcontig = true });

                                    }
                                    else
                                    {
                                        // contiguous collisions, do not report
                                        collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round((double)MRGPATBOX, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = true });
                                        MRGPATBOX = GantryAngle;
                                        lastcontigreal = true;
                                    }
                                    //  System.Windows.Forms.MessageBox.Show("Trigger 19");
                                    //System.Windows.Forms.MessageBox.Show("Lastcontigreal set true");
                                }
                                else
                                {
                                    if (lastcontigreal == true)
                                    {
                                            // System.Windows.Forms.MessageBox.Show("Last contig fire");
                                            collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = false, lastcontig = true });
                                            MRGPATBOX = GantryAngle;
                                            lastcontigreal = false;
                                      //  System.Windows.Forms.MessageBox.Show("Trigger 20");
                                    }
                                    else
                                    {
                                            collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = false });
                                            MRGPATBOX = GantryAngle;
                                            lastcontigreal = false;
                                       // System.Windows.Forms.MessageBox.Show("Trigger 21");
                                    }
                                }
                            }
                            else if (lastcontigreal == true)
                            {
                                    collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(reportCang, 0, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "PATBOX", contiguous = false, lastcontig = true });
                                    lastcontigreal = false;
                                    // System.Windows.Forms.MessageBox.Show("Trigger 22");
                            }
                        }
                        
                        //   System.Windows.Forms.MessageBox.Show("Trigger 23");


                        /*

                                //body contour collision check
                                snear_tids = diskgantryspatial.FindNearestTriangles(PBodyContourSpatial, null, out INTERSECTDIST);
                                if (snear_tids.a == DMesh3.InvalidID || snear_tids.b == DMesh3.InvalidID)
                                {
                                    // out of range, do nothing
                                    System.Windows.Forms.MessageBox.Show("body contour out of range");
                                }   
                                else
                                {
                                    STriDist = MeshQueries.TrianglesDistance(diskgantry, snear_tids.a, PBodyContour, snear_tids.b);
                                    ZABSDIST = ABSDISTANCE(STriDist.Triangle0Closest, STriDist.Triangle1Closest);
                                    if (ZABSDIST <= 50.0)
                                    {
                                        if (collist.Contains(new CollisionAlert { type = "Body Contour" }))
                                        {
                                            if (collist.FindLast(l => l.type == "Body Contour").gantryangle >= GantryAngle - 10.0 && collist.FindLast(l => l.type == "Body Contour").gantryangle <= GantryAngle + 10.0)
                                            {
                                                //repeat, assume same contiguous collision and do not print out
                                                collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = ZABSDIST, type = "Body Contour", contiguous = true });
                                            }
                                            else
                                            {
                                            collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Body Contour", contiguous = false });
                                            }
                                        }
                                        else
                                        {
                                        collist.Add(new CollisionAlert { beam = beam.Id, gantryangle = Math.Round(GantryAngle, 0, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(ZABSDIST, 0, MidpointRounding.AwayFromZero), type = "Body Contour", contiguous = false });
                                        }
                                    }
                                }

                        */




                        //so segment intersections work, just need to figure out what to do with it

                        /*
                                if(closebody == false & pintersect.point0.z < pbmaxz & pintersect.point0.z > pbminz)
                                {
                                    coli = false;
                                }
                        */

                        /*
                                if ((pintersect.point0.x >= pminx & pintersect.point0.x <= (pminx + 30.0)) & (pintersect.point1.x >= pminx & pintersect.point1.x <= (pminx + 30.0)) & (pintersect.point0.y >= pminy & pintersect.point0.y <= (pminy + 30.0)) & (pintersect.point1.y >= pminy & pintersect.point1.y <= (pminy + 30.0)))
                                {
                                    boxedgeflag = "upper left";
                                }
                                else if ((pintersect.point0.x <= pmaxx & pintersect.point0.x >= (pmaxx - 30.0)) & (pintersect.point1.x <= pmaxx & pintersect.point1.x >= (pmaxx - 30.0)) & (pintersect.point0.y >= pminy & pintersect.point0.y <= (pminy + 30.0)) & (pintersect.point1.y >= pminy & pintersect.point1.y <= (pminy + 30.0)))
                                {
                                    boxedgeflag = "upper right";
                                }
                                else if ((pintersect.point0.x <= pmaxx & pintersect.point0.x >= (pmaxx - 30.0)) & (pintersect.point1.x <= pmaxx & pintersect.point1.x >= (pmaxx - 30.0)) & (pintersect.point0.y <= pmaxy & pintersect.point0.y >= (pmaxy - 30.0)) & (pintersect.point1.y <= pmaxy & pintersect.point1.y >= (pmaxy - 30.0)))
                                {
                                    boxedgeflag = "lower right";
                                }
                                else if ((pintersect.point0.x >= pminx & pintersect.point0.x <= (pminx + 30.0)) & (pintersect.point1.x >= pminx & pintersect.point1.x <= (pminx + 30.0)) & (pintersect.point0.y <= pmaxy & pintersect.point0.y >= (pmaxy - 30.0)) & (pintersect.point1.y <= pmaxy & pintersect.point1.y >= (pmaxy - 30.0)))
                                {
                                    boxedgeflag = "lower left";
                                }
                        */

                        // Meshint = pintersect.point;                             





                        //foreach (int vert in PATBOX.VertexIndices())
                        //{

                        /*

                           for (int ci = 0; ci <= PATBOX.MaxVertexID; ci++)
                           { 
                           if(PATBOX.IsVertex(ci) == false)
                           {
                               continue;
                           }


                           Vector3d distpoint = PATBOX.GetVertex(ci);

                           patlist.Add(distpoint);



                           VVector Vect = new VVector(distpoint.x, distpoint.y, distpoint.z);

                           DistRightEdge = VVector.Distance(Vect, RIGHTEDGE);
                           DistLeftEdge = VVector.Distance(Vect, LEFTEDGE);
                           DistBackEdge = VVector.Distance(Vect, BACKEDGE);
                           DistFrontEdge = VVector.Distance(Vect, FRONTEDGE);
                           Distgf = VVector.Distance(Vect, gantrycenter);

                           if (DistRightEdge <= 50.0)
                           {
                               collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(GantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(DistRightEdge, 1, MidpointRounding.AwayFromZero), distpoint = "Right Edge", Gantrypoint = RIGHTEDGE, Patpoint = Vect });



                           }
                           else if (DistLeftEdge <= 50.0)
                           {
                               collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(GantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(DistLeftEdge, 1, MidpointRounding.AwayFromZero), distpoint = "Left Edge", Gantrypoint = LEFTEDGE, Patpoint = Vect });



                           }
                           else if (DistBackEdge <= 50.0)
                           {
                               collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(GantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(DistBackEdge, 1, MidpointRounding.AwayFromZero), distpoint = "Back Edge", Gantrypoint = BACKEDGE, Patpoint = Vect });



                           }
                           else if (DistFrontEdge <= 50.0)
                           {
                               collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(GantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(DistFrontEdge, 1, MidpointRounding.AwayFromZero), distpoint = "Front Edge", Gantrypoint = FRONTEDGE, Patpoint = Vect });



                           }
                           else if (Distgf <= 50.0)
                           {
                               collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(GantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(Distgf, 1, MidpointRounding.AwayFromZero), distpoint = "Center", Gantrypoint = gantrycenter, Patpoint = Vect });



                           }

                           // ProgOutput.AppendText(Environment.NewLine);
                           // ProgOutput.AppendText("vert: " + vert + "/" + PATBOX.VertexCount);

                       }

                    */


                        /*
                           colcomp = collist.FindLast(
                           delegate (CollisionAlert ca)
                           {
                               return ca.distpoint == "Right Edge" & ca.controlpoint == (point.Index - 1) & VVector.Distance(Vect, ca.Patpoint) <= 70.0);
                           });
                        */






                        //  MessageBox.Show("PATBOX vert  loop done");

                    }  // ends gantry angle loop
                           
                }    // ends if counch angle start = couch angle end

                //      MessageBox.Show("COUCH LOOP DONE    ");

                IOWriteResult EVERY = StandardMeshWriter.WriteFile(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\" + plan.Course.Patient.Id + "_" + plan.Course.Id + "_" + plan.Id + "_" + "Beam_" + beam.Id + ".stl", tempbeam , WriteOptions.Defaults);
              //  FileInfo file = new FileInfo(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\" + plan.Course.Patient.Id + "_" + plan.Course.Id + "_" + plan.Id + "_" + "Beam_" + beam.Id + ".stl");

             //   bool ftest = IsFileLocked(file);

             //   if (ftest == false)
             //   {
             //       MainWindow window = new MainWindow(@"\\ntfs16\Therapyphysics\Treatment Planning Systems\Eclipse\Scripting common files\Collision_Check_STL_files\" + plan.Course.Patient.Id + "_" + plan.Course.Id + "_" + plan.Id + "_" + "Beam_" + beam.Id + ".stl");
             //       window.Show();
             //   }
                

                

            } // ends beam loop

          //  MessageBox.Show("Beam loop done");

            return collist;
        } // ends collision check

    }

}
 
