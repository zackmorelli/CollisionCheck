using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using g3;

using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using CollisionCheck;



/*
    Collision Check - Script Execute Startup program


    Description/information:

    This program is multi-threaded using the Task Parallel Library (TPL) found in .NET Framework. It also uses traditional threading from earlier versions of .NET.
    Thread safety is achieved mostly through the use of custom classes to completley extract ESAPI (which cannot be used in a multi-threaded environment).
    The Main thread starts here when the Execute method the ESAPI Script class is called. The GUI is then called on it's own Task, and the execute method (button) of the of GUI is called on it's own Task as well.
    This ensures that the GUI is always responsive. The Execute method from the GUI then conducts the Collision analysis for each beam in parallel (at the same time).
    This ensures the program runs smoothly.

    This program is expressely written as a plug-in script for use with Varian's Eclipse Treatment Planning System, and requires Varian's API files to run properly.
    This program also requires .NET Framework 4.6.1 to run properly.
    This program also uses the Gradientspace package (g3) to generate and manipulate 3D mesh structures, and write them as STL files.
    This program also uses the HelixToolkit package to render the STL files of each beam if a collision happens. This is not located here, but is in another class/file called 3DRender. 3DRender is used by the GUI class.

     all linear dimensions are expressed in millimeters and all angles are expressed in degrees (many angles are converted from radians). 
     the program is specifically based off the physical dimensions of C-ARM Varian Linacs.
     In particular:
     the gantry head is 77 cm wide
     the distance between the isocenter and the center of the gantry/collimator head is 41.5 cm.
     the distance between the source (target in the gantry head) and the isocenter is 100 cm.
     and therfore the distance to the source to the center of the gantry head is 58.5 cm.
     These distances are always true, no matter what. the only way to change them is to physically move the linac
     This includes the API's internal vector objects and the positions it reports of various objects
     THE COORDINATE SYSTEM IS IN DICOM AND IS COMPLETLEY DEFINED BY THE TREATMENT PLANNING CT OF THE PATIENT. THIS MEANS THE COORDINATE SYSTEM IS AFFECTED BY TREATMENT ORIENTATION. COUCH KICKS ROTATE THE ENTIRE COORDINATE SYSTEM AS WELL.
     The program is designed around this and is able to produce an accurate geometric model that accounts for these coordinate system complications. You may notice that the part of this program that makes the gantry structure includes an absurd amount of vector transformations. This is because of the coordinate system.

    This is the main program that is called when Collision Check is run. It includes a GUI, which is called from here.
    Specifically, The execute method of the VMS Script class calles the GUI (separate file). When the user clicks "Execute" on the GUI, the GUI then calls the CollionCheck method, located here.
    CollisonCheck calls the Boxmaker method. CollisionCheck ouputs collision information using the custom CollisonAlert class.
    The User uses the GUI to select which plan to run the program on (out of the courses that they have open) and the Body area of the CT Scan and the patient's Height.
    The GUI also outputs collision information if neccesary.

    ==========================================================================

    Copyright (C) 2021 Zackary Thomas Ricci Morelli
    
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

    I can be contacted at: zackmorelli@gmail.com


    Release 3.2 - 7/26/2021


*/

namespace VMS.TPS
{
    public class Script  // creates a class called Script within the VMS.TPS Namesapce
    {
        public Script() { }  // instantiates a Script class

        //This is the execute function of the script class which calls the GUI. The GUI in turn calls the CollionCheck method.
        public void Execute(ScriptContext context)     
        {
            IEnumerable<PlanSetup> Plans = context.PlansInScope;
            Patient patient = context.Patient;
            Image image = context.Image;

            //  MessageBox.Show("Trig 1");
            if (context.Patient == null)
            {
                System.Windows.Forms.MessageBox.Show("Please load a patient with a treatment plan before running this script!");
                return;
            }

            //populate my own classes with ESAPI info in order to completley isolate ESAPI, that way we can multi-thread.
            // that requires figuring out a lot of stuff here though before we call the GUI (on its own thread)

            List<PLAN> PLANS = new List<PLAN>();
            string strctid = null;
            bool couchexist = false;
            bool breastboardexists = false;

            try
            {
                foreach (PlanSetup plan in Plans)
                {
                    try
                    {
                        strctid = plan.StructureSet.Id;
                    }
                    catch (NullReferenceException e)
                    {
                        System.Windows.Forms.MessageBox.Show("The plan " + plan.Id + " does not have a structure set!");
                        // no structure set, skip
                        continue;
                    }

                    string patientId = plan.Course.Patient.Id;
                    string patientsex = plan.Course.Patient.Sex;
                    string courseId = plan.Course.Id;

                    // retrieves the body structure
                    IEnumerator BR = plan.StructureSet.Structures.GetEnumerator();
                    BR.MoveNext();
                    BR.MoveNext();
                    BR.MoveNext();

                    Structure Body = (Structure)BR.Current;

                    //Structure CouchSurface = (Structure)BR.Current;

                    Structure CouchInterior = (Structure)BR.Current;

                    Structure Prone_Brst_Board = (Structure)BR.Current;

                    Vector3d bodycenter = new Vector3d();
                    Vector3d couchinteriorcenter = new Vector3d();
                    Vector3d breastboardcenter = new Vector3d();

                    foreach (Structure STR in plan.StructureSet.Structures)
                    {
                        if (STR.Id == "Body")
                        {
                            Body = STR;
                            bodycenter = new Vector3d(Body.CenterPoint.x, Body.CenterPoint.y, Body.CenterPoint.z);
                        }
                        else if (STR.Id.Contains("CouchInterior") || STR.Id.Contains("couchinterior") || STR.Id.Contains("couch interior") || STR.Id.Contains("couch_interior") || STR.Id.Contains("Couch_Interior") || STR.Id.Contains("Couch Interior"))
                        {
                            if (STR.IsEmpty == true || STR.Volume < 0.0)
                            {
                                System.Windows.Forms.MessageBox.Show("The Couch Interior structure is not contoured!");
                                continue;
                            }

                           // System.Windows.Forms.MessageBox.Show("Found couch interior");
                            couchexist = true;
                            CouchInterior = STR;
                            couchinteriorcenter = new Vector3d(CouchInterior.CenterPoint.x, CouchInterior.CenterPoint.y, CouchInterior.CenterPoint.z);
                        }
                        else if (STR.Id.Contains("Prone_Brst_Board") || STR.Id.Contains("NS_Prone_Bst_Brd") || STR.Id.Contains("Prone_Bst_Brd") || STR.Id.Contains("Prone_Brst_Brd") || STR.Id.Contains("Prone_Bst_Board") || STR.Id.Contains("Prone Brst Board") || STR.Id.Contains("Prone Bst Brd") || STR.Id.Contains("Prone Brst Brd") || STR.Id.Contains("Prone Bst Board") || STR.Id.Contains("prone_brst_board") || STR.Id.Contains("prone_bst_brd") || STR.Id.Contains("prone_brst_brd") || STR.Id.Contains("prone_bst_board") || STR.Id.Contains("prone brst board") || STR.Id.Contains("prone bst brd") || STR.Id.Contains("pron brst brd") || STR.Id.Contains("prone bst board"))
                        {
                            if (STR.IsEmpty == true || STR.Volume < 0.0)
                            {
                                System.Windows.Forms.MessageBox.Show("The Prone Breast Board structure is not contoured!");
                                continue;
                            }

                           // System.Windows.Forms.MessageBox.Show("Found breast board");
                            Prone_Brst_Board = STR;
                            breastboardexists = true;
                            breastboardcenter = new Vector3d(Prone_Brst_Board.CenterPoint.x, Prone_Brst_Board.CenterPoint.y, Prone_Brst_Board.CenterPoint.z);
                        }

                        //else if (STR.Id == "CouchSurface" || )
                        //{
                        //    CouchSurface = STR;
                        //    findCouchSurf = true;
                        //}

                        // findCouchSurf = false;

                    }

                    //System.Windows.Forms.MessageBox.Show(plan.TreatmentOrientation.ToString());

                    string PATIENTORIENTATION = null;

                    // Head first prone
                    if (plan.TreatmentOrientation == PatientOrientation.HeadFirstSupine)
                    {
                        PATIENTORIENTATION = "HeadFirstSupine";
                    }
                    else if (plan.TreatmentOrientation == PatientOrientation.HeadFirstProne)
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

                    //now we loop through all the beams in the current plan to get beam-specific information
                    List<BEAM> Beams = new List<BEAM>();
                    foreach (Beam beam in plan.Beams)
                    {
                        bool mlctype = false;

                        //this is important. for a long time I didn't realize that a fair amount of plans have beams with undefined mlcs. this was causing the program to do thimgs it shouldn't have
                        // since i taught the program to interpolate gantry angles for any beam, without needing more than the first control point, which every beam has, this isn't an issue
                        // but still, be careful about the mlc type of a beam. It isn't obvoius.
                        if (beam.MLCPlanType == MLCPlanType.Static || beam.MLCPlanType == MLCPlanType.NotDefined)
                        {
                            mlctype = false;
                        }
                        else
                        {
                            mlctype = true;
                        }

                        Vector3d iso = new Vector3d(beam.IsocenterPosition.x, beam.IsocenterPosition.y, beam.IsocenterPosition.z);
                        VVector st = new VVector();
                        Vector3d apiSource = new Vector3d();    // only for use with non-isocentric beams
                        string gantrydir = null;

                        if (beam.GantryDirection == GantryDirection.Clockwise)
                        {
                            gantrydir = "Clockwise";
                        }
                        else if (beam.GantryDirection == GantryDirection.CounterClockwise)
                        {
                            gantrydir = "CounterClockwise";
                        }
                        else if (beam.GantryDirection == GantryDirection.None)
                        {
                            gantrydir = "None";
                            // meaning not an arc
                            st = beam.GetSourceLocation(beam.ControlPoints.First().GantryAngle);
                            //System.Windows.Forms.MessageBox.Show("st : (" + st.x + ", " + st.y + ", " + st.z + ")");
                        }

                        string beamId = beam.Id;
                        bool setupfield = beam.IsSetupField;
                        double arclength = beam.ArcLength;
                        //System.Windows.Forms.MessageBox.Show("Beam: " + beam.Id);
                        //System.Windows.Forms.MessageBox.Show("MLC Type: " + beam.MLCPlanType.ToString());
                        //System.Windows.Forms.MessageBox.Show("Arc Length: " + arclength);
                        //System.Windows.Forms.MessageBox.Show("Control points: " + beam.ControlPoints.Count);
                        apiSource = new Vector3d(st.x, st.y, st.z);

                        List<CONTROLPOINT> controlpoints = new List<CONTROLPOINT>();
                        foreach (ControlPoint cp in beam.ControlPoints)
                        {
                            //System.Windows.Forms.MessageBox.Show("Gantryangle: " + cp.GantryAngle);
                            controlpoints.Add(new CONTROLPOINT { Couchangle = cp.PatientSupportAngle, Gantryangle = cp.GantryAngle });
                        }
                        //System.Windows.Forms.MessageBox.Show("controlpoints size: " + controlpoints.Count);
                        //System.Windows.Forms.MessageBox.Show("APISource : (" + apiSource.x + ", " + apiSource.y + ", " + apiSource.z + ")");
                        Beams.Add(new BEAM { MLCtype = mlctype, Isocenter = iso, APISource = apiSource, gantrydirection = gantrydir, beamId = beamId, ControlPoints = controlpoints, setupfield = setupfield, arclength = arclength });
                    }
                    PLANS.Add(new PLAN { planId = plan.Id, StructureSetId = plan.StructureSet.Id, TreatmentOrientation = PATIENTORIENTATION, Body = Body.MeshGeometry, CouchInterior = CouchInterior.MeshGeometry, ProneBreastBoard = Prone_Brst_Board.MeshGeometry, breastboardexists = breastboardexists, couchexists = couchexist, Beams = Beams, patientId = patientId, patientsex = patientsex, courseId = courseId, Bodycenter = bodycenter, BreastBoardcenter = breastboardcenter, CouchInteriorcenter = couchinteriorcenter, Bodyvects = new List<Vector3d>(), Bodyindices = new List<int>(), CouchInteriorvects = new List<Vector3d>(), CouchInteriorindices = new List<int>(), BreastBoardvects = new List<Vector3d>(), BreastBoardindices = new List<int>(), BodyBoxXsize = 1000000.0, BodyBoxYSize = 1000000.0 , BodyBoxZSize = 1000000.0});
                }

                //this loops through the PLAN list we just made to fill in all the geometric information for the body structure, breast board (if present), and couch.
                //In ESAPI, the Structure.MeshGeometry method gives access to the 3D Mesh of structures in an RT plan.
                //However, it uses the Microsoft MeshGeometry3D class.
                //The code below is a really good template for how to pull the info given in the Microsoft MeshGeometry3D class and put it into lists that can be used to create meshes in the GradientSpace class.
                //The GradientSpace meshes are much more useful because GradientSpace contains a ton of methods to do geometric and collision analysis.
                //The lists of vertex vectors and triangle indices are used to construct meshes with the GradientSpace class
                foreach (PLAN Plan in PLANS)
                {
                    // System.Windows.Forms.MessageBox.Show(Plan.planId + "START body vector conversion");
                    //System.Windows.Forms.MessageBox.Show("Body positions size: " + Plan.Body.Positions.Count);

                    double XP;
                    double YP;
                    double ZP;
                    Vector3d Vect;

                    foreach (Point3D p in Plan.Body.Positions)
                    {
                        XP = p.X;
                        YP = p.Y;
                        ZP = p.Z;
                        Vect = new Vector3d(XP, YP, ZP);
                        Plan.Bodyvects.Add(Vect);
                    }

                    foreach (int t in Plan.Body.TriangleIndices)
                    {
                        Plan.Bodyindices.Add(t);
                    }

                    if (Plan.couchexists == true)
                    {
                       // System.Windows.Forms.MessageBox.Show("couch vector conversion");
                        foreach (Point3D p in Plan.CouchInterior.Positions)
                        {
                            Plan.CouchInteriorvects.Add(new g3.Vector3d { x = p.X, y = p.Y, z = p.Z });
                        }

                        foreach (int t in Plan.CouchInterior.TriangleIndices)
                        {
                            Plan.CouchInteriorindices.Add(t);
                        }
                    }

                    if (Plan.breastboardexists == true)
                    {
                       // System.Windows.Forms.MessageBox.Show("breast board vector conversion");
                        foreach (Point3D p in Plan.ProneBreastBoard.Positions)
                        {
                            Plan.BreastBoardvects.Add(new g3.Vector3d { x = p.X, y = p.Y, z = p.Z });
                        }

                        foreach (int t in Plan.ProneBreastBoard.TriangleIndices)
                        {
                            Plan.BreastBoardindices.Add(t);
                        }
                    }

                    Plan.BodyBoxXsize = Plan.Body.Bounds.SizeX;
                    Plan.BodyBoxYSize = Plan.Body.Bounds.SizeY;
                    Plan.BodyBoxZSize = Plan.Body.Bounds.SizeZ;
                }
            }
            catch(Exception e)
            {
                System.Windows.Forms.MessageBox.Show(e.ToString() + "\n\n\n" + e.StackTrace + "\n\n\n" + e.InnerException);
            }

            IMAGE Image = new IMAGE();
            Image.imageuserorigin = new Vector3d(image.UserOrigin.x, image.UserOrigin.y, image.UserOrigin.z);
            Image.imageorigin = new Vector3d(image.Origin.x, image.Origin.y, image.Origin.z);

            System.Windows.Forms.Application.EnableVisualStyles();

            //The GUI starts running on its own thread. We pass it the PLAN list and IMAGE object, so it has all the info it needs.
            //The program then returns, ending the script execute method and ENDING THE ECLIPSE SCRIPT.
            //At this point, as far as Eclipse is concerned, the program has ended.
            //However, the GUI is still running on another Task in the Windows OS.
            //So what actaully happens when you run the script is there is like a second where nothing happens, because all the code in this script execute file is running and there is no output.
            //Then GUI starts and appears on the screen. The Eclipse script ends at the same time, like a second or second and a half after it is started. Eclipse becomes unfrozen while GUI runs on runs on its own thread.
           // System.Windows.Forms.MessageBox.Show("Starting GUI on separate thread.");
             Task.Run(() => System.Windows.Forms.Application.Run(new GUI(PLANS, Image)));
           // System.Windows.Forms.MessageBox.Show("After GUI Call, main script thread will now return and close.");
  
            return;
        }

        
    } // end of the script class and VMS namespace

}
 
