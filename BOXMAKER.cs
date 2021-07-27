using System;
using System.Collections.Generic;
using System.Windows.Forms;

using g3;


/*
    Collision Check - BOXMAKER


    Description/information:
    BOXMAKER is a method called by the analysis method that makes all of the patient-related meshes for the 3D collison model, so everything that is not the disk that represents the gantry head, or the accesories.
    Specifically, what is done here is we take the information extracted from ESAPI about the patient-related structures in ScriptExecute and turn them into meshes of the GradientSpace class.
    However, you'll see that a majority of the code here is for building the patient bounding cylinder, which was originally a box.
    The construction of the patient-bounding cylinder has gone through a lot of change, to the point where I am able to make an elliptical Cylinder that models the patient's body fairly well.
    This cuts down on false positive collison alerts caused by clipping though the corners of the original box and works much better for the FAST mode in particular.
    There is a lot of code here that is for how the bounding-box used to work that has been commented out. I've kept it becuase I feel it is important
    There is some weird manipulation that goes into scaling the bounding-box to adjust for the are of the patient's body that the CT scan represents.

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
    class BOXMAKERclass
    {
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
            double patbzshift = beam.BodyBoxZSize / 2.0;

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

                //Actually an Elliptical Cylinder. patbxshift is the semi-major axis and patbyshift is the semi-minor axis. 1 cm margin added to adjust for considerable curving/eccentricity to ensure entire patient is covered. Fast mode adds 5 cm anyway.
                for (double j = 0.0; j < 361.0; j += 3.6)   // 101 times
                {
                    PatCylFaceXcoord = centerofforwardface.x + (SemiMajor * Math.Cos(j * MathUtil.Deg2Rad));
                    PatCylFaceYcoord = centerofforwardface.y + (SemiMinor * Math.Sin(j * MathUtil.Deg2Rad));
                    PatCylVectList[iterate] = new Vector3d(PatCylFaceXcoord, PatCylFaceYcoord, centerofforwardface.z);
                    // MessageBox.Show("Index trig 3");
                    PatCylVectList[102 + iterate] = new Vector3d(PatCylFaceXcoord, PatCylFaceYcoord, centerofdownwardface.z);
                    //MessageBox.Show("Index trig 4");

                    if (FAST == true)
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

                if (FAST == true)
                {
                    foreach (Vector3d fpec in FastPatCylVectList)
                    {
                        FASTPATCYLINDER.AppendVertex(new NewVertexInfo(fpec));
                    }
                }

                foreach (Index3i tri in PatCylTriangleIndexList)
                {
                    PATCYLINDER.AppendTriangle(tri);

                    if (FAST == true)
                    {
                        FASTPATCYLINDER.AppendTriangle(tri);
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Cylinder Build error \n\n\n\n" + e.ToString() + "\n\n\n" + e.StackTrace + "\n\n\n" + e.Source);
            }

            // CYLINDER IS WORKING PROPERLY!!!!! 

            // IOWriteResult result151 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\PATCYLINDER.stl", new List<WriteMesh>() { new WriteMesh(PATCYLINDER) }, WriteOptions.Defaults);

            // we use the remesher in Gradientspace to remesh the cylinder in a way that makes more sense than how we were able to construct it mathematically
            // In other words, it takes the 3D shape we made and remshes in a uniform way, which is very important if we want to use it to detect collisions, becuase collisions are based off vertex positions. It also smoothes it out.
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

            //After the cylinder is remeshed, it actually will have some serious problems with it, like it might not be closed. Most of it should be okay though and meshed well
            //So, what we can do is take advantage of GradientSpace's automatic mesh repair class to solve what would otherwise be a serious dillema to being able to use the cylinder
            //fortunatley this works quite well. Note that you must call the Apply() method from a repair object that you make out of the mesh for it to actaully run and alter the mesh.
            gs.MeshAutoRepair repair = new gs.MeshAutoRepair(PATCYLINDER);
            repair.RemoveMode = gs.MeshAutoRepair.RemoveModes.None;
            repair.Apply();

            //IOWriteResult result153 = StandardMeshWriter.WriteFile(@"C:\Users\ztm00\Desktop\STL Files\Test\PATCYLINDERremeshed+repaired.stl", new List<WriteMesh>() { new WriteMesh(PATCYLINDER) }, WriteOptions.Defaults);

            //This does the same remsh and repair for the FAST Cylinder
            if (FAST == true)
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
            if (FAST == true)
            {
                PatMeshList.Add(FASTPATCYLINDER);
            }
            //System.Windows.Forms.MessageBox.Show("End of BOXMAKER");
            return PatMeshList;
        }













    }
}
