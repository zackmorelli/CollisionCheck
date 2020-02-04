using System;
using System.Media;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
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

        
        // Declaration space for all the functions which make up the program.
        // Execution begins with the "Execute" function.

        // Thread Prog = new Thread(Script());

        // gantry head 77 cm wide

        // 41.5 cm distance from iso to gantry head??

        // not currently used, but still here in case we want to revert to something similiar
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

        public double ABSDISTANCE(Vector3d V1, Vector3d V2)
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
                MessageBox.Show("Please load a patient with a treatment plan before running this script!");
                return;
            }

            System.Windows.Forms.Application.EnableVisualStyles();

            //Starts GUI 
            System.Windows.Forms.Application.Run(new CollisionCheck.GUI(Plans, patient, image));

            return;
        }

        public class CollisionAlert : IEquatable<CollisionAlert>
        {
            public string beam { get; set; }

            public int controlpoint { get; set; }

            public double gantryangle {get; set;}

            public double couchangle { get; set; }

            public double distance { get; set; }

            public string distpoint { get; set; }

            public Vector3d Gantrypoint { get; set; }
           
            public VVector Patpoint { get; set; }

            public string edgeclip { get; set; }

            public bool pbodyalert { get; set; }


            public bool Equals(CollisionAlert other)
            {
                throw new NotImplementedException();
            }
        }

        public static DMeshAABBTree3 BOXMAKER (bool findCouchSurf, bool findCouchInterior, bool findProneBrstBoard, Structure Body, Structure CouchSurface, Structure CouchInterior, Structure Prone_Brst_Board, string bodyloc, double ht, TextBox ProgOutput, PlanSetup plan, Image image)
        {

            ht = ht * 10.0;    //convert from cm to mm.   IT is in mm


            // makes mesh out of patient body contour
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

            IOWriteResult result24 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\TestBolus\PBODY.stl", new List<WriteMesh>() { new WriteMesh(PBodyContour) }, WriteOptions.Defaults);

            PBodyContourSpatial = new DMeshAABBTree3(PBodyContour);
            PBodyContourSpatial.Build();

            if (findCouchSurf == true)
            {
                // -------------------------------------------------------------------- makes mesh out of Couch surface
                List<Vector3d> cspvl = new List<Vector3d>();
                Vector3d cspv = new Vector3d();

                List<Index3i> csptl = new List<Index3i>();
                Index3i cspt = new Index3i();
                int csptmcount = 0;

                foreach (Point3D pm in CouchSurface.MeshGeometry.Positions)
                {
                    cspv.x = pm.X;
                    cspv.y = pm.Y;
                    cspv.z = pm.Z;

                    cspvl.Add(cspv);
                }

                foreach (Int32 ptm in CouchSurface.MeshGeometry.TriangleIndices)
                {
                    csptmcount++;
                    Math.DivRem(csptmcount, 3, out rem);

                    if (rem == 2)
                    {
                        cspt.a = ptm;
                    }
                    else if (rem == 1)
                    {
                        cspt.b = ptm;
                    }
                    else if (rem == 0)
                    {
                        cspt.c = ptm;
                        csptl.Add(cspt);
                    }
                }

                PCouchsurf = new DMesh3(MeshComponents.VertexNormals);
                for (int i = 0; i < cspvl.Count; i++)
                {
                    PCouchsurf.AppendVertex(new NewVertexInfo(cspvl[i]));
                }

                for (int i = 0; i < csptl.Count; i++)
                {
                    PBodyContour.AppendTriangle(csptl[i]);
                }

                IOWriteResult result31 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\TestBolus\CouchSurface.stl", new List<WriteMesh>() { new WriteMesh(PCouchsurf) }, WriteOptions.Defaults);

                CouchSurfSpatial = new DMeshAABBTree3(PCouchsurf);
                CouchSurfSpatial.Build();
            }


            if (findCouchInterior == true)
            {
                // ------------------------------------------------------- makes mesh out of Couch interior
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

                IOWriteResult result30 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\TestBolus\CouchInterior.stl", new List<WriteMesh>() { new WriteMesh(PCouchInterior) }, WriteOptions.Defaults);

                PCouchInteriorSpatial = new DMeshAABBTree3(PCouchInterior);
                PCouchInteriorSpatial.Build();
            }

            if (findProneBrstBoard == true)
            {
                // ------------------------------------------------------- makes mesh out of Prone Breast Board
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

                IOWriteResult result36 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\TestBolus\ProneBreastBoard.stl", new List<WriteMesh>() { new WriteMesh(PProne_Brst_Board) }, WriteOptions.Defaults);

                PProne_Brst_BoardSpatial = new DMeshAABBTree3(PProne_Brst_Board);
                PProne_Brst_BoardSpatial.Build();
            }








            Rect3D BOX = Body.MeshGeometry.Bounds;  // retrieves bounding box of the body contour

           // MessageBox.Show("body structure center point is: (" + Body.CenterPoint.x + " ," + Body.CenterPoint.y + " ," + Body.CenterPoint.z + ")");

             VVector dicomvec = image.DicomToUser(Body.CenterPoint, plan);

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
            ProgOutput.AppendText("Starting PATBOX Construction");

           // MessageBox.Show("ht is: " + ht);


            double LT = BOX.SizeZ;
           // MessageBox.Show("LT is: " + LT);

            double headdownshift = ht - LT;
            double thoraxupshift = (ht - LT) * 0.22;
            double thoraxdownshift = (ht - LT) * 0.78;
            double abdomenupshift = (ht - LT) * 0.3;
            double abdomendownshift = (ht - LT) * 0.7;
            double pelvisupshift = (ht - LT) * 0.45;
            double pelvisdownshift = (ht - LT) * 0.55;

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
                vect.z = Body.CenterPoint.z - patbzshift - abdomendownshift;
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

            DMesh3 PATBOX = new DMesh3(MeshComponents.VertexNormals);
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

            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("PATBOX Construction Done");

            //  DMesh3 PATBOX2 = DMesh3Builder.Build(IEnumerable<Vector3d> vertices, triangles);

            //PATBOX.CheckValidity();

            //IOWriteResult result = StandardMeshWriter.WriteFile(@"\\Wvvrnimbp01ss\va_data$\filedata\ProgramData\Vision\PublishedScripts\PATBOX.stl", new List<WriteMesh>() { new WriteMesh(PATBOX) }, WriteOptions.Defaults);

            //IOWriteResult result2 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\TestBolus\PATBOX.stl", new List<WriteMesh>() { new WriteMesh(PATBOX) }, WriteOptions.Defaults);

            DMesh3 PATBOXCOPY = PATBOX;

            // use the remesher to add triangles/vertices to mesh based off of the simple box mesh

            Remesher R = new Remesher(PATBOX);
            MeshConstraintUtil.PreserveBoundaryLoops(R);
            R.PreventNormalFlips = true;
            R.SetTargetEdgeLength(40.0);
            R.SmoothSpeedT = 0.5;
            R.SetProjectionTarget(MeshProjectionTarget.Auto(PATBOXCOPY));
            R.ProjectionMode = Remesher.TargetProjectionMode.Inline;

            for (int k = 0; k < 6; k++)
            {
                R.BasicRemeshPass();
            }

            ProgOutput.AppendText(Environment.NewLine);
            ProgOutput.AppendText("PATBOX Remeshing Done");

            // Remeshing settings aren't perfect, bu they are fairly dialed in

            IOWriteResult result3 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\TestBolus\PATBOXremeshed.stl", new List<WriteMesh>() { new WriteMesh(PATBOX) }, WriteOptions.Defaults);

            List<Vector3d> patlist = new List<Vector3d>();      //for patbox
            List<Vector3d> patlistb = new List<Vector3d>();   //for pbody

            for (int ci = 0; ci <= PATBOX.MaxVertexID; ci++)
            {
                if (PATBOX.IsVertex(ci) == false)
                {
                    continue;
                }

                Vector3d distpoint = PATBOX.GetVertex(ci);

                patlist.Add(distpoint);

            }

            for (int cb = 0; cb <= PBodyContour.MaxVertexID; cb++)
            {
                if (PBodyContour.IsVertex(cb) == false)
                {
                    continue;
                }

                Vector3d distpointb = PBodyContour.GetVertex(cb);

                patlistb.Add(distpointb);

            }

            foreach (Vector3d V in patlist)
            {
                if (V.x > pmaxx)
                {
                    pmaxx = V.x;
                }

                if (V.x < pminx)
                {
                    pminx = V.x;
                }

                if (V.y > pmaxy)
                {
                    pmaxy = V.y;
                }

                if (V.y < pminy)
                {
                    pminy = V.y;
                }

                if (V.z > pmaxz)
                {
                    pmaxz = V.z;
                }

                if (V.z < pminz)
                {
                    pminz = V.z;
                }
            }

            foreach (Vector3d Vb in patlistb)
            {

                if (Vb.z > pbmaxz)
                {
                    pbmaxz = Vb.z;
                }

                if (Vb.z < pbminz)
                {
                    pbminz = Vb.z;
                }
            }


            

      /*
            MessageBox.Show("pmaxx is: (" + pmaxx + ")");
            MessageBox.Show("pminx is: (" + pminx + ")");
            MessageBox.Show("pmaxy is: (" + pmaxy + ")");
            MessageBox.Show("pminy is: (" + pminy + ")");
            MessageBox.Show("pmaxz is: (" + pmaxz + ")");
            MessageBox.Show("pminz is: (" + pminz + ")");
            MessageBox.Show("pbmaxz is: (" + pbmaxz + ")");
            MessageBox.Show("pbminz is: (" + pbminz + ")");
      */




            DMeshAABBTree3 spatial = new DMeshAABBTree3(PATBOX);
            spatial.Build();


            return spatial;

        }


        public static List<CollisionAlert> CollisionCheck(PlanSetup plan, string bodyloc, double ht, TextBox ProgOutput, Image image)
        {
            // declaration space for outputs and things used between boxmaker and collision check

            List<CollisionAlert> collist = new List<CollisionAlert>();
            CollisionAlert colcomp = new CollisionAlert();

            bool findCouchSurf = false;
            bool findCouchInterior = false;
            bool findProneBrstBoard = false;


            // MessageBox.Show("Collision Check initiated! Note, because this program doen't know where within the body the Isocenter is, it has ben over-engineered to compensate. Most likely this program will give false collision alerts for beams with extreme couch kicks (close to 270 degrees or 90 degrees) at gantry angles close to 270 or 90. Any other collsion alert should be taken seriously.");               
            // retrieves the body structure
            IEnumerator BR = plan.StructureSet.Structures.GetEnumerator();
            BR.MoveNext();
            BR.MoveNext();
            BR.MoveNext();

            Structure Body = (Structure)BR.Current;

            IEnumerator CSR = plan.StructureSet.Structures.GetEnumerator();
            CSR.MoveNext();
            CSR.MoveNext();
            CSR.MoveNext();

            Structure CouchSurface = (Structure)CSR.Current;

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
                else if (STR.Id == "CouchSurface")
                {
                    CouchSurface = STR;
                    findCouchSurf = true;
                }
                else if (STR.Id == "CouchInterior")
                {
                    CouchInterior = STR;
                    findCouchInterior = true;
                }
                else if (STR.Id == "Prone_Brst_Board")
                {
                    Prone_Brst_Board = STR;
                    findProneBrstBoard = true;
                }
            }

            string PATIENTORIENTATION = null;

            // Head first prone
            if(plan.TreatmentOrientation == PatientOrientation.HeadFirstProne)
            {
                PATIENTORIENTATION = "HeadFirstProne";
            }

            DMeshAABBTree3 spatial = BOXMAKER(findCouchSurf, findCouchInterior, findProneBrstBoard, Body, CouchSurface, CouchInterior, Prone_Brst_Board, bodyloc, ht, ProgOutput, plan, image);

            // start of beam loop
            foreach (Beam beam in plan.Beams)
            {   

                if(beam.IsSetupField == true )
                {
                    continue;
                }


                // ANGLES ARE IN DEGREES
                ControlPointCollection PC = beam.ControlPoints;
                double CouchStartAngle = PC[0].PatientSupportAngle;
                double CouchEndAngle = PC[PC.Count - 1].PatientSupportAngle;            // count - 1 is the end becuase the index starts at 0

                if (CouchStartAngle != CouchEndAngle)
                {
                    MessageBox.Show("WARNING: The patient couch has a different rotation angle at the end of beam " + beam.Id + " in plan " + plan.Id + " than what the beam starts with.");
                    return collist;
                }
                else if (CouchStartAngle == CouchEndAngle)
                {

                     ProgOutput.AppendText(Environment.NewLine);
                     ProgOutput.AppendText("Starting analysis of beam " + beam.Id + " in plan " + plan.Id + " .");
                     // MessageBox.Show("Starting analysis of beam " + beam.Id + " in plan " + plan.Id + " .");

                     VVector ISO = beam.IsocenterPosition;
                     // MessageBox.Show("Isocenter point is at: (" + ISO.x + " ," + ISO.y + " ," + ISO.z + ")");

                     double DistRightEdge = 0.0;
                            double DistLeftEdge = 0.0;
                            double DistBackEdge = 0.0;
                            double DistFrontEdge = 0.0;
                            double Distgf = 0.0;

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
                    DMeshAABBTree3.IntersectionsQueryResult intersectlist = new DMeshAABBTree3.IntersectionsQueryResult();
                    Vector3d Meshint = new Vector3d();


                    foreach (ControlPoint point in PC)
                    {

                        double ActGantryAngle = point.GantryAngle;

                      //  if(PATIENTORIENTATION == "HeadFirstProne")
                      //  {
                      //      ActGantryAngle = point.GantryAngle - 180.0;
                      //  }

                            // Math.DivRem(point.Index, 3, out int res);

                            //if (point.Index == 1 || point.Index == PC.Count || res == 0)
                            //{

                            ProgOutput.AppendText(Environment.NewLine);
                            ProgOutput.AppendText("Control Point " + point.Index + "/" + PC.Count);                                                 

                            //  MessageBox.Show("Control Point count: " + TL + ")");
                             // MessageBox.Show("Gantry ANGLE :  " + ActGantryAngle + "  ");
                            //  MessageBox.Show("couch ANGLE :  " + CouchEndAngle + "  ");

                            //  MessageBox.Show("real couch ANGLE :  " + realangle + "  ");

                            VVector APISOURCE = beam.GetSourceLocation(ActGantryAngle);  // negative Y
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

                            myZ = ISO.z;
                            myX = 1000 * Math.Cos((((ActGantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.x;    // - 90 degrees is because the polar coordinate system has 0 degrees on the right side
                            myY = 1000 * Math.Sin((((-ActGantryAngle - 90.0) * Math.PI) / 180.0)) + ISO.y;   // need negative because y axis is inverted
                            // THIS WORKS!

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

                            if (ActGantryAngle > 270.0)
                            {
                                Gangle = 90.0 - (ActGantryAngle - 270.0);

                                gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                                gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                            }
                            else if (ActGantryAngle >= 0.0 & ActGantryAngle <= 90.0)
                            {
                                Gangle = ActGantryAngle;
                                gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                                gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                            }
                            else if (ActGantryAngle > 90.0 & ActGantryAngle <= 180.0)
                            {
                                Gangle = 90.0 - (ActGantryAngle - 90.0);

                                gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                                gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                            }
                            else if (ActGantryAngle > 180.0 & ActGantryAngle <= 270.0)
                            {
                                //  MessageBox.Show("Trig 5");
                                Gangle = ActGantryAngle - 180.0;

                                gfxp = 585.0 * Math.Sin((Gangle * Math.PI) / 180.0);
                                gfyp = 585.0 * Math.Cos((Gangle * Math.PI) / 180.0);
                            }

                            thetap = 90.0 - Gangle;

                            // MessageBox.Show("thetap is: " + thetap);

                            VVector gantrycenter = mySOURCE;    // this will represent the center of the gantry head's surface once the transforms below are performed
                                                    // For couch zero degrees

                            if (ActGantryAngle >= 270.0 | (ActGantryAngle >= 0.0 & ActGantryAngle <= 90.0))
                            {
                                gantrycenter.y = gantrycenter.y + gfyp;
                                //  MessageBox.Show("gf.y is: " + gf.y);
                            }
                            else if (ActGantryAngle < 270.0 & ActGantryAngle > 90.0)
                            {
                                gantrycenter.y = gantrycenter.y - gfyp;
                            }

                            // this just determines if the original xshift to gf is positive or negative
                            if (ActGantryAngle >= 0.0 & ActGantryAngle <= 180.0)
                            {
                                gantrycenter.x = gantrycenter.x - gfxp;
                                gantryXISOshift = "POS";
                            }
                            else if (ActGantryAngle > 180.0)
                            {
                                gantrycenter.x = gantrycenter.x + gfxp;
                                gantryXISOshift = "NEG";
                            }

                            VVector origgantrycenter = gantrycenter;

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
                            else if(CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
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

                            if (ActGantryAngle >= 0.0 & ActGantryAngle <= 90.0)
                            {
                                //  MessageBox.Show("Trigger gantry angle between 0 and 90 gantry points calculation");
                                RIGHTEDGE.y = RIGHTEDGE.y + ypp;
                                LEFTEDGE.y = LEFTEDGE.y - ypp;
                                RIGHTEDGE.x = RIGHTEDGE.x + xpp;
                                LEFTEDGE.x = LEFTEDGE.x - xpp;
                            }
                            else if (ActGantryAngle > 270.0)
                            {
                                //MessageBox.Show("Trigger gantry angle > 270 gantry points calculation");
                                RIGHTEDGE.y = RIGHTEDGE.y - ypp;
                                LEFTEDGE.y = LEFTEDGE.y + ypp;
                                RIGHTEDGE.x = RIGHTEDGE.x + xpp;
                                LEFTEDGE.x = LEFTEDGE.x - xpp;
                            }
                            else if (ActGantryAngle > 90.0 & ActGantryAngle <= 180.0)
                            {
                                RIGHTEDGE.y = RIGHTEDGE.y + ypp;
                                LEFTEDGE.y = LEFTEDGE.y - ypp;
                                RIGHTEDGE.x = RIGHTEDGE.x - xpp;
                                LEFTEDGE.x = LEFTEDGE.x + xpp;
                            }
                            else if (ActGantryAngle > 180.0 & ActGantryAngle <= 270.0)
                            {
                                RIGHTEDGE.y = RIGHTEDGE.y - ypp;
                                LEFTEDGE.y = LEFTEDGE.y + ypp;
                                RIGHTEDGE.x = RIGHTEDGE.x - xpp;
                                LEFTEDGE.x = LEFTEDGE.x + xpp;
                            }


                            // GANTRY EDGE POINTS COORDINATE SYSTEM TRANSFORMATION FOR COUCH ANGLE
                            if (CouchEndAngle >= 270.0 & CouchEndAngle <= 360.0)
                            {
                                // this is really for 0 to 90 couch angle
                                //  MessageBox.Show("REAL couch ANGLE :  " + realangle + "  ");
                                //  MessageBox.Show("TRIGGER ROT 0 to 270");
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

                       // MessageBox.Show("Trig");

                    /*
                        Vector3d[] diskgantry2pointcollection = new Vector3d[4];
                        diskgantry2pointcollection[0] = Ri;
                        diskgantry2pointcollection[1] = Le;
                        diskgantry2pointcollection[2] = Ba;
                        diskgantry2pointcollection[3] = Fr;

                        Frame3f diskgantry2origin = new Frame3f(Ce);
                        TrivialDiscGenerator makegantryhead = new TrivialDiscGenerator();
                        Curve3Axis3RevolveGenerator makegantryhead2 = new Curve3Axis3RevolveGenerator
                        {
                            Axis = diskgantry2origin,
                            Curve = diskgantry2pointcollection,
                            Capped = true,
                            Slices = 1
                        };

                        makegantryhead2.Generate();
                       // MessageBox.Show("Trig1");

                        makegantryhead.Radius = 382.5f;
                        makegantryhead.StartAngleDeg = 0.0f;
                        makegantryhead.EndAngleDeg = 360.0f;
                        makegantryhead.Slices = 72;
                        makegantryhead.Generate();
                       // MessageBox.Show("Trig1.5");

                        DMesh3 diskgantry = makegantryhead.MakeDMesh();
                        DMesh3 diskgantry2 = makegantryhead2.MakeDMesh();
                       // MessageBox.Show("Trig2");

                        MeshTransforms.Translate(diskgantry, Ce);
                        Vector3d apiSOURCE = new Vector3d(APISOURCE.x, APISOURCE.y, APISOURCE.z);

                        Quaterniond diskgantryrotate = new Quaterniond(Ce, apiSOURCE);
                        Vector3d origin = new Vector3d(0, 0, 0);

                        MeshTransforms.Rotate(diskgantry, origin, diskgantryrotate);
                       // MessageBox.Show("Trig3");

                        IOWriteResult result42 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\diskGantry" + beam.Id + point.Index + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry) }, WriteOptions.Defaults);
                        IOWriteResult result76 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\diskGantry2" + beam.Id + point.Index + ".stl", new List<WriteMesh>() { new WriteMesh(diskgantry2) }, WriteOptions.Defaults);

                      //  MessageBox.Show("Trig7");

                    */

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

                        /*

                                DMesh3 SUPERGANTRY = new DMesh3(MeshComponents.VertexNormals);
                                SUPERGANTRY.AppendVertex(new NewVertexInfo(Ri));
                                SUPERGANTRY.AppendVertex(new NewVertexInfo(Le));
                                SUPERGANTRY.AppendVertex(new NewVertexInfo(Ba));
                                SUPERGANTRY.AppendVertex(new NewVertexInfo(Fr));
                                SUPERGANTRY.AppendVertex(new NewVertexInfo(Ce));

                            List<Index3i> SuperGrangle = new List<Index3i>();
                            Index3i Supershangle = new Index3i();

                            Quaterniond gantryrot = new Quaterniond(Ce, 10.0);

                            Vector3d newRi = MeshTransforms.Rotate(Ri, origin, gantryrot);
                            Vector3d newLe = MeshTransforms.Rotate(Le, origin, gantryrot);
                            Vector3d newBa = MeshTransforms.Rotate(Ba, origin, gantryrot);
                            Vector3d newFr = MeshTransforms.Rotate(Fr, origin, gantryrot);

                            SUPERGANTRY.AppendVertex(new NewVertexInfo(newRi));
                            SUPERGANTRY.AppendVertex(new NewVertexInfo(newLe));
                            SUPERGANTRY.AppendVertex(new NewVertexInfo(newBa));
                            SUPERGANTRY.AppendVertex(new NewVertexInfo(newFr));

                            //first rotation
                            Supershangle = new Index3i(4, 0, 5);
                            SuperGrangle.Add(Supershangle);

                            Supershangle = new Index3i(4, 1, 6 );
                            SuperGrangle.Add(Supershangle);

                            Supershangle = new Index3i(4, 2, 7);
                            SuperGrangle.Add(Supershangle);

                            Supershangle = new Index3i(4, 3, 8);
                            SuperGrangle.Add(Supershangle);

                            for (int i = 0; i <= 7; i++)
                            {
                                newRi = MeshTransforms.Rotate(newRi, origin, gantryrot);
                                newLe = MeshTransforms.Rotate(newLe, origin, gantryrot);
                                newBa = MeshTransforms.Rotate(newBa, origin, gantryrot);
                                newFr = MeshTransforms.Rotate(newFr, origin, gantryrot);

                                SUPERGANTRY.AppendVertex(new NewVertexInfo(newRi));
                                SUPERGANTRY.AppendVertex(new NewVertexInfo(newLe));
                                SUPERGANTRY.AppendVertex(new NewVertexInfo(newBa));
                                SUPERGANTRY.AppendVertex(new NewVertexInfo(newFr));

                                Supershangle = new Index3i(4, 5 + (i * 4), 9 + (i * 4));
                                SuperGrangle.Add(Supershangle);

                                Supershangle = new Index3i(4, 6 + (i * 4), 10 + (i * 4));
                                SuperGrangle.Add(Supershangle);

                                Supershangle = new Index3i(4, 7 + (i * 4), 11 + (i * 4));
                                SuperGrangle.Add(Supershangle);

                                Supershangle = new Index3i(4, 8 + (i * 4), 12 + (i * 4));
                                SuperGrangle.Add(Supershangle);
                            }

                            // last points at 80 degrees with original points of the next one
                            Supershangle = new Index3i(4, 37, 2);
                            SuperGrangle.Add(Supershangle);

                            Supershangle = new Index3i(4, 38, 3);
                            SuperGrangle.Add(Supershangle);

                            Supershangle = new Index3i(4, 39, 1);
                            SuperGrangle.Add(Supershangle);

                            Supershangle = new Index3i(4, 40, 0);
                            SuperGrangle.Add(Supershangle);

                            foreach (Index3i tri in SuperGrangle)
                            {
                                SUPERGANTRY.AppendTriangle(tri);
                            }

                    */

                        //    MessageBox.Show("Trig8");

                            // MessageBox.Show("number of triangles: " + cbht);

                            // ProgOutput.AppendText(Environment.NewLine);
                            // ProgOutput.AppendText("Gantry mesh Construction Done");

                            //  DMesh3 PATBOX2 = DMesh3Builder.Build(IEnumerable<Vector3d> vertices, triangles);

                            //PATBOX.CheckValidity();

                            //IOWriteResult result = StandardMeshWriter.WriteFile(@"\\Wvvrnimbp01ss\va_data$\filedata\ProgramData\Vision\PublishedScripts\PATBOX.stl", new List<WriteMesh>() { new WriteMesh(PATBOX) }, WriteOptions.Defaults);

                            IOWriteResult result5 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\Gantry" + beam.Id + point.Index + ".stl", new List<WriteMesh>() { new WriteMesh(GANTRY) }, WriteOptions.Defaults);
                           // IOWriteResult result30 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\TestBolus\superGantry" + beam.Id + point.Index + ".stl", new List<WriteMesh>() { new WriteMesh(SUPERGANTRY) }, WriteOptions.Defaults);
                            //    DMesh3 SUPERGANTRYCOPY = SUPERGANTRY;

                                // use the remesher to add triangles/vertices to mesh based off of the simple box mesh

                            //    Remesher R = new Remesher(SUPERGANTRY);
                                Remesher JK = new Remesher(GANTRY);
                                //  MeshConstraintUtil.PreserveBoundaryLoops(R);
                             //   R.PreventNormalFlips = true;
                                JK.PreventNormalFlips = true;
                             //   R.SetTargetEdgeLength(30.0);
                             //   R.SmoothSpeedT = 0.4;
                                JK.SetTargetEdgeLength(30.0);
                                JK.SmoothSpeedT = 0.15;
                              //  R.SetProjectionTarget(MeshProjectionTarget.Auto(SUPERGANTRYCOPY));
                              //  R.ProjectionMode = Remesher.TargetProjectionMode.Inline;

                                for (int k = 0; k < 5; k++)
                                {
                                   // R.BasicRemeshPass();
                                    JK.BasicRemeshPass();
                                }

                               // ProgOutput.AppendText(Environment.NewLine);
                               // ProgOutput.AppendText("Gantry Remeshing Done");
                                // Remeshing settings aren't perfect, bu they are fairly dialed in

                              //  IOWriteResult result3 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\TestBolus\superGantryremeshed" + beam.Id + point.Index + ".stl", new List<WriteMesh>() { new WriteMesh(SUPERGANTRY) }, WriteOptions.Defaults);
                                IOWriteResult result89 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\Gantryremeshed" + beam.Id + point.Index + ".stl", new List<WriteMesh>() { new WriteMesh(GANTRY) }, WriteOptions.Defaults);

                                DMeshAABBTree3 GANTRYspatial = new DMeshAABBTree3(GANTRY);
                                GANTRYspatial.Build();

                          //      MessageBox.Show("Trig3");

                            string boxedgeflag = null;
                            bool closebody = false;
                            bool coli = true;


                            if (findCouchSurf == true)
                            {
                                if (GANTRYspatial.TestIntersection(CouchSurfSpatial) == true)
                                {
                                    // MessageBox.Show("gspatial / couch surface collision");
                                    collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(ActGantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distpoint = "Intersection with Couch", pbodyalert = closebody });
                                }
                            }

                            if (findCouchInterior == true)
                            {
                                if (GANTRYspatial.TestIntersection(PCouchInteriorSpatial) == true)
                                {
                                    // MessageBox.Show("gspatial / couch interior collision");
                                    collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(ActGantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distpoint = "Intersection with Couch.", pbodyalert = closebody });
                                }
                            }

                            if (findProneBrstBoard == true)
                            {
                                if (GANTRYspatial.TestIntersection(PProne_Brst_BoardSpatial) == true)
                                {
                                    // MessageBox.Show("gspatial / Prone Breast Board collision");
                                    collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(ActGantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distpoint = "Intersection with Prone Breast Board.", pbodyalert = closebody });
                                }
                            }


                            if (GANTRYspatial.TestIntersection(spatial) == true)
                            {
                                    // MessageBox.Show("gspatial / spatial(patient box) collision");

                                    if (GANTRYspatial.TestIntersection(PBodyContourSpatial) == true)
                                    {
                                        closebody = true;
                                    }

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

                                    
                                    if(coli == true)
                                    {
                                        collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(ActGantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distpoint = "Intersection with patient bounding box.", pbodyalert = closebody });
                                    }   

                            }                  

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
                                   collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(ActGantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(DistRightEdge, 1, MidpointRounding.AwayFromZero), distpoint = "Right Edge", Gantrypoint = RIGHTEDGE, Patpoint = Vect });



                               }
                               else if (DistLeftEdge <= 50.0)
                               {
                                   collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(ActGantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(DistLeftEdge, 1, MidpointRounding.AwayFromZero), distpoint = "Left Edge", Gantrypoint = LEFTEDGE, Patpoint = Vect });



                               }
                               else if (DistBackEdge <= 50.0)
                               {
                                   collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(ActGantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(DistBackEdge, 1, MidpointRounding.AwayFromZero), distpoint = "Back Edge", Gantrypoint = BACKEDGE, Patpoint = Vect });



                               }
                               else if (DistFrontEdge <= 50.0)
                               {
                                   collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(ActGantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(DistFrontEdge, 1, MidpointRounding.AwayFromZero), distpoint = "Front Edge", Gantrypoint = FRONTEDGE, Patpoint = Vect });



                               }
                               else if (Distgf <= 50.0)
                               {
                                   collist.Add(new CollisionAlert { beam = beam.Id, controlpoint = point.Index, gantryangle = Math.Round(ActGantryAngle, 1, MidpointRounding.AwayFromZero), couchangle = Math.Round(CouchEndAngle, 1, MidpointRounding.AwayFromZero), distance = Math.Round(Distgf, 1, MidpointRounding.AwayFromZero), distpoint = "Center", Gantrypoint = gantrycenter, Patpoint = Vect });



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

                        //}     // ends index control point counting loop
                    }   // ends control point loop   







                        //  MessageBox.Show("CONTROL POINT loop done");

                }    // ends if counch angle start = couch angle end

                    //      MessageBox.Show("COUCH LOOP DONE    ");

            } // ends beam loop

          //  MessageBox.Show("Beam loop done");

            return collist;
        } // ends collision check

    }

}
 
