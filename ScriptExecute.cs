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



/*
    Collision Check - Main program
    Copyright (c) Zackary Thomas Ricci Morelli
    Release 3.1 July 8, 2021

    This program is multi-threaded using the Task Parallel Library (TPL) found in .NET Framework. It also uses traditional threading from earlier versions of .NET.
    Thread safety is achieved mostly through the use of custom classes to completley extract ESAPI (which cannot be used in a multi-threaded environment).
    The Main thread starts here when the Execute method the ESAPI Script class is called. The GUI is then called on it's own Task, and the execute method (button) of the of GUI is called on it's own Task as well.
    This ensures that the GUI is always responsive. The Execute method from the GUI then conducts the Collision analysis for each beam in parallel (at the same time), and does any ARIA database querying on a separate thread.
    This ensures the program runs smoothly.

    This program is expressely written as a plug-in script for use with Varian's Eclipse Treatment Planning System, and requires Varian's API files to run properly.
    This program also requires .NET Framework 4.6.1 to run properly.
    This program also uses the Gradientspace package (g3) to generate and manipulate 3D mesh structures, and write them as STL files.
    This program also uses the HelixToolkit package to render the STL files of each beam if a collision happens. This is not located here, but is in another class/file called 3DRender. 3DRender is used by the GUI class.

    Also, very important, in the case of a static MLC plan, this program will start a completley new process (on it's own asychronous Task) on the computer and run a complete separate program called Colliosncheck_Inforetrieval.
    Collisioncheck_inforetrieval is an executable command-line program (written by me) saved on the ARIA server which uses an Entity Framework 6 model called ARIAQUERY to query the ARIA database for gantry information that the Collision check program needs. This information is not available in the Eclipse API if the plan does not have Control points.
    The gantry information obtained by CollisonCheck_inforetrieval is written to the standard output stream of the command-line, which is actually redirected and read in to collision check. The retrieval program then ends.
    ARIAQUERY is a separate class (also made by me) that is an Entity Framework 6 (a popular microsoft package for dealing with SQL databases) model of the department's ARIA database SQL Server.
    The details of how this works are not explained for security purposes.

     all linear dimensions are expressed in millimeters and all angles are expressed in degrees (many angles are converted from radians). 
     the program is specifically based off the physical dimensions of the department's C-ARM Varian Linacs.
     This includes the API's internal vector objects and the positions it reports of various objects
     THE COORDINATE SYSTEM IS IN DICOM AND IS COMPLETLEY DEFINED BY THE TREATMENT PLANNING CT OF THE PATIENT. THIS MEANS THE COORDINATE SYSTEM IS AFFECTED BY TREATMENT ORIENTATION. COUCH KICKS ROTATE THE ENTIRE COORDINATE SYSTEM AS WELL.
     The program is designed around this and is able to produce an accurate geometric model that accounts for these coordinate system complications. You may notice that the part of this program that makes the gantry structure includes an absurd amount of vector transformations. This is because of the coordinate system.

    Description: This is the main program that is called when Collision Check is run. It includes a GUI, which is called from here.
    Specifically, The execute method of the VMS Script class calles the GUI (separate file). When the user clicks "Execute" on the GUI, the GUI then calls the CollionCheck method, located here.
    CollisonCheck calls the Boxmaker method. CollisionCheck ouputs collision information using the custom CollisonAlert class (located here).
    The User uses the GUI to select which plan to run the program on (out of the courses that they have open) and the Body area of the CT Scan and the patient's Height.
    The GUI also outputs collision information if neccesary.
*/

namespace VMS.TPS
{

    public class Script  // creates a class called Script within the VMS.TPS Namesapce
    {

        public Script() { }  // instantiates a Script class


        // Declaration space for all the functions which make up the program.
        // Execution begins with the "Execute" function.
        // gantry head 77 cm wide
        // 41.5 cm distance from iso to gantry head
        // This is called in the collision analysis



        //This is the execute function of the script class which calls the GUI. The GUI in turn calls the CollionCheck method.
        public void Execute(ScriptContext context)     
        {
            //Variable declaration space

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
            // that requires figuring out a lot of stiff here though before we call the GUI (on its own thread)

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

                    List<BEAM> Beams = new List<BEAM>();
                    foreach (Beam beam in plan.Beams)
                    {
                        bool mlctype = false;

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

                foreach(PLAN Plan in PLANS)
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

            //Starts GUI 
           // System.Windows.Forms.MessageBox.Show("Starting GUI on separate thread.");
             Task.Run(() => System.Windows.Forms.Application.Run(new GUI(PLANS, Image)));
           // System.Windows.Forms.MessageBox.Show("After GUI Call, main script thread will now return and close.");
  
            return;
        }

        
    } // end of the script class and VMS namespace

}
 
