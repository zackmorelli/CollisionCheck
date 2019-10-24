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
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;


/*
    Collision Check
    Copyright (c) 2019 Radiation Oncology Department, Lahey Hospital and Medical Center
    Written by Zack Morelli

    This program is expressely written as a plug-in script for use with Varian's Eclipse Treatment Planning System, and requires Varian's API files to run properly.
    This program also requires .NET Framework 4.5.0 to run properly.

*/

namespace VMS.TPS
{
    public class Script  // creates a class called Script within the VMS.TPS Namesapce
    {

        public Script() { }  // instantiates a Script class


        // Global Variable Declaration



        // Declaration space for all the functions which make up the program.
        // Execution begins with the "Execute" function.

        // Thread Prog = new Thread(Script());

        // gantry head 75 cm wide

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


        public void Execute(ScriptContext context)     // PROGRAM START - sending a return to Execute will end the program
        {
            //Variable declaration space

            IEnumerable<PlanSetup> Plans = context.PlansInScope;

            // start of actual code

            //  MessageBox.Show("Trig 1");
            if (context.Patient == null)
            {
                MessageBox.Show("Please load a patient with a treatment plan before running this script!");
                return;
            }

            MessageBox.Show("Collision Check initiated!");

            foreach (PlanSetup plan in Plans)
            {
                bool planlocker = false;
                // get body contour and get use segment profiles to get the distance

                IEnumerator SR = plan.StructureSet.Structures.GetEnumerator();
                SR.MoveNext();
                SR.MoveNext();
                SR.MoveNext();

                Structure Body = (Structure)SR.Current;

                foreach (Structure STR in plan.StructureSet.Structures)
                {
                    if (STR.Id == "Body")
                    {
                        Body = STR;
                    }
                }

                foreach (Beam beam in plan.Beams)
                {
                    bool beamlocker = false;
                    ControlPointCollection PC = beam.ControlPoints;
                    double CouchStartAngle = PC[0].PatientSupportAngle;
                    double CouchEndAngle = PC[PC.Count - 1].PatientSupportAngle;            // count - 1 is the end becuase the index starts at 0

                    if (CouchStartAngle != CouchEndAngle)
                    {
                        MessageBox.Show("WARNING: The patient couch has a different rotation angle at the end of beam " + beam.Id + " in plan " + plan.Id + " than what the beam starts with.");
                        return;
                    }
                    else if (CouchStartAngle == CouchEndAngle)
                    {
                        if ((CouchStartAngle >= 10.0 & CouchStartAngle <= 90.0) | (CouchStartAngle <= 350.0 & CouchStartAngle >= 270.0))
                        {
                            MessageBox.Show("WARNING: The beam " + beam.Id + " in plan " + plan.Id + " has a couch kick of 10 degrees or more. This could result in a collision. A collision analysis will now be conducted.");

                            VVector ISO = beam.IsocenterPosition;
                            MessageBox.Show("ISOCENTER COORDINATES: " + ISO.ToString());

                            //used as the end points of line profiles used to determine edge/intersection point of body contour in order to create a set of points which roughly represents the patient's body
                            VVector ZEND = new VVector(ISO.x, ISO.y, (ISO.z + 50.0));
                            VVector XPOSEND = new VVector((ISO.x + 60.0), ISO.y, ISO.z);
                            VVector XNEGEND = new VVector((ISO.x - 60.0), ISO.y, ISO.z);

                            //y axis shifts from ISO used as starting point of various segment profiles
                            VVector yminus40 = new VVector(ISO.x, (ISO.y - 40.0), ISO.z);
                            VVector yplus40 = new VVector(ISO.x, (ISO.y + 40.0), ISO.z);
                            VVector yminus30 = new VVector(ISO.x, (ISO.y - 30.0), ISO.z);
                            VVector yplus30 = new VVector(ISO.x, (ISO.y + 30.0), ISO.z);
                            VVector yminus20 = new VVector(ISO.x, (ISO.y - 20.0), ISO.z);
                            VVector yplus20 = new VVector(ISO.x, (ISO.y + 20.0), ISO.z);
                            VVector yminus10 = new VVector(ISO.x, (ISO.y - 10.0), ISO.z);
                            VVector yplus10 = new VVector(ISO.x, (ISO.y + 10.0), ISO.z);
                            VVector yminus50 = new VVector(ISO.x, (ISO.y - 50.0), ISO.z);
                            VVector yplus50 = new VVector(ISO.x, (ISO.y + 50.0), ISO.z);
                            VVector yminus60 = new VVector(ISO.x, (ISO.y - 60.0), ISO.z);
                            VVector yplus60 = new VVector(ISO.x, (ISO.y + 60.0), ISO.z);
                            VVector yminus70 = new VVector(ISO.x, (ISO.y - 70.0), ISO.z);
                            VVector yplus70 = new VVector(ISO.x, (ISO.y + 70.0), ISO.z);
                            VVector yminus80 = new VVector(ISO.x, (ISO.y - 80.0), ISO.z);
                            VVector yplus80 = new VVector(ISO.x, (ISO.y + 80.0), ISO.z);
                            VVector yminus90 = new VVector(ISO.x, (ISO.y - 90.0), ISO.z);
                            VVector yplus90 = new VVector(ISO.x, (ISO.y + 90.0), ISO.z);
                            VVector yminus100 = new VVector(ISO.x, (ISO.y - 100.0), ISO.z);
                            VVector yplus100 = new VVector(ISO.x, (ISO.y + 100.0), ISO.z);
                            VVector yminus110 = new VVector(ISO.x, (ISO.y - 110.0), ISO.z);
                            VVector yplus110 = new VVector(ISO.x, (ISO.y + 110.0), ISO.z);
                            VVector yminus120 = new VVector(ISO.x, (ISO.y - 120.0), ISO.z);
                            VVector yplus120 = new VVector(ISO.x, (ISO.y + 120.0), ISO.z);
                            VVector yminus130 = new VVector(ISO.x, (ISO.y - 130.0), ISO.z);
                            VVector yplus130 = new VVector(ISO.x, (ISO.y + 130.0), ISO.z);
                            VVector yminus140 = new VVector(ISO.x, (ISO.y - 140.0), ISO.z);
                            VVector yplus140 = new VVector(ISO.x, (ISO.y + 140.0), ISO.z);
                            VVector yminus150 = new VVector(ISO.x, (ISO.y - 150.0), ISO.z);
                            VVector yplus150 = new VVector(ISO.x, (ISO.y + 150.0), ISO.z);

                            // these are preallocated memory buffers where the points of the segment profiles are stored. They also determine how many points are in the segemnt profiles. The sizes chosen give a point every 0.16 cm.
                            BitArray J = new BitArray(300);
                            BitArray K = new BitArray(380);
                            BitArray L = new BitArray(380);

                            // the three segment profiles used to find the position of the body contour along the Z and X axes, for multiple points along the y-axis
                            SegmentProfile ZPROF = Body.GetSegmentProfile(ISO, ZEND, J);
                            SegmentProfile XPOSPROF = Body.GetSegmentProfile(ISO, XPOSEND, K);    // plus 60 positive x
                            SegmentProfile XNEGPROF = Body.GetSegmentProfile(ISO, XNEGEND, L);   // minus 60 negative x

                            // ALL VECTORS REPRESENTING INTERSECTION POINTS OF SEGMENT PROFILES WITH BODY CONTOUR ARE STORED IN AN ARRAY OF VECTORS 

                            VVector[] PBODY = new VVector[93];  //Patient Body

                            // intersection vectors at ISO
                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[0] = ZPROF.EdgeCoordinates[0];
                                PBODY[1] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[2] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //starts getting the intersection vectors for all the y-axis shifts
                            // 10 cm
                            ZPROF = Body.GetSegmentProfile(yminus10, new VVector(ISO.x, ISO.y - 10.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus10, new VVector((ISO.x + 60.0), ISO.y - 10.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus10, new VVector((ISO.x + 60.0), ISO.y - 10.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[3] = ZPROF.EdgeCoordinates[0];
                                PBODY[4] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[5] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus10, new VVector(ISO.x, ISO.y + 10.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus10, new VVector((ISO.x + 60.0), ISO.y + 10.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus10, new VVector((ISO.x + 60.0), ISO.y + 10.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[6] = ZPROF.EdgeCoordinates[0];
                                PBODY[7] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[8] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //20 cm
                            ZPROF = Body.GetSegmentProfile(yminus20, new VVector(ISO.x, ISO.y - 20.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus20, new VVector((ISO.x + 60.0), ISO.y - 20.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus20, new VVector((ISO.x + 60.0), ISO.y - 20.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[9] = ZPROF.EdgeCoordinates[0];
                                PBODY[10] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[11] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus20, new VVector(ISO.x, ISO.y + 20.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus20, new VVector((ISO.x + 60.0), ISO.y + 20.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus20, new VVector((ISO.x + 60.0), ISO.y + 20.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[12] = ZPROF.EdgeCoordinates[0];
                                PBODY[13] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[14] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //30 cm
                            ZPROF = Body.GetSegmentProfile(yminus30, new VVector(ISO.x, ISO.y - 30.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus30, new VVector((ISO.x + 60.0), ISO.y - 30.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus30, new VVector((ISO.x + 60.0), ISO.y - 30.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[15] = ZPROF.EdgeCoordinates[0];
                                PBODY[16] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[17] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus30, new VVector(ISO.x, ISO.y + 30.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus30, new VVector((ISO.x + 60.0), ISO.y + 30.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus30, new VVector((ISO.x + 60.0), ISO.y + 30.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[18] = ZPROF.EdgeCoordinates[0];
                                PBODY[19] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[20] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //40 cm
                            ZPROF = Body.GetSegmentProfile(yminus40, new VVector(ISO.x, ISO.y - 40.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus40, new VVector((ISO.x + 60.0), ISO.y - 40.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus40, new VVector((ISO.x + 60.0), ISO.y - 40.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[21] = ZPROF.EdgeCoordinates[0];
                                PBODY[22] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[23] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus40, new VVector(ISO.x, ISO.y + 40.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus40, new VVector((ISO.x + 60.0), ISO.y + 40.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus40, new VVector((ISO.x + 60.0), ISO.y + 40.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[24] = ZPROF.EdgeCoordinates[0];
                                PBODY[25] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[26] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //50 cm
                            ZPROF = Body.GetSegmentProfile(yminus50, new VVector(ISO.x, ISO.y - 50.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus50, new VVector((ISO.x + 60.0), ISO.y - 50.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus50, new VVector((ISO.x + 60.0), ISO.y - 50.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[27] = ZPROF.EdgeCoordinates[0];
                                PBODY[28] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[29] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus50, new VVector(ISO.x, ISO.y + 50.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus50, new VVector((ISO.x + 60.0), ISO.y + 50.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus50, new VVector((ISO.x + 60.0), ISO.y + 50.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[30] = ZPROF.EdgeCoordinates[0];
                                PBODY[31] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[32] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //60 cm
                            ZPROF = Body.GetSegmentProfile(yminus60, new VVector(ISO.x, ISO.y - 60.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus60, new VVector((ISO.x + 60.0), ISO.y - 60.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus60, new VVector((ISO.x + 60.0), ISO.y - 60.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[33] = ZPROF.EdgeCoordinates[0];
                                PBODY[34] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[35] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus60, new VVector(ISO.x, ISO.y + 60.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus60, new VVector((ISO.x + 60.0), ISO.y + 60.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus60, new VVector((ISO.x + 60.0), ISO.y + 60.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[36] = ZPROF.EdgeCoordinates[0];
                                PBODY[37] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[38] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //70 cm
                            ZPROF = Body.GetSegmentProfile(yminus70, new VVector(ISO.x, ISO.y - 70.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus70, new VVector((ISO.x + 60.0), ISO.y - 70.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus70, new VVector((ISO.x + 60.0), ISO.y - 70.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[39] = ZPROF.EdgeCoordinates[0];
                                PBODY[40] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[41] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus70, new VVector(ISO.x, ISO.y + 70.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus70, new VVector((ISO.x + 60.0), ISO.y + 70.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus70, new VVector((ISO.x + 60.0), ISO.y + 70.0, ISO.z), L);   // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[42] = ZPROF.EdgeCoordinates[0];
                                PBODY[43] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[44] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //80 cm
                            ZPROF = Body.GetSegmentProfile(yminus80, new VVector(ISO.x, ISO.y - 80.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus80, new VVector((ISO.x + 60.0), ISO.y - 80.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus80, new VVector((ISO.x + 60.0), ISO.y - 80.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[45] = ZPROF.EdgeCoordinates[0];
                                PBODY[46] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[47] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus80, new VVector(ISO.x, ISO.y + 80.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus80, new VVector((ISO.x + 60.0), ISO.y + 80.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus80, new VVector((ISO.x + 60.0), ISO.y + 80.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[48] = ZPROF.EdgeCoordinates[0];
                                PBODY[49] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[50] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //90 cm
                            ZPROF = Body.GetSegmentProfile(yminus90, new VVector(ISO.x, ISO.y - 90.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus90, new VVector((ISO.x + 60.0), ISO.y - 90.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus90, new VVector((ISO.x + 60.0), ISO.y - 90.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[51] = ZPROF.EdgeCoordinates[0];
                                PBODY[52] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[53] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus90, new VVector(ISO.x, ISO.y + 90.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus90, new VVector((ISO.x + 60.0), ISO.y + 90.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus90, new VVector((ISO.x + 60.0), ISO.y + 90.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[54] = ZPROF.EdgeCoordinates[0];
                                PBODY[55] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[56] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //100 cm
                            ZPROF = Body.GetSegmentProfile(yminus100, new VVector(ISO.x, ISO.y - 100.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus100, new VVector((ISO.x + 60.0), ISO.y - 100.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus100, new VVector((ISO.x + 60.0), ISO.y - 100.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[57] = ZPROF.EdgeCoordinates[0];
                                PBODY[58] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[59] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus100, new VVector(ISO.x, ISO.y + 100.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus100, new VVector((ISO.x + 60.0), ISO.y + 100.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus100, new VVector((ISO.x + 60.0), ISO.y + 100.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[60] = ZPROF.EdgeCoordinates[0];
                                PBODY[61] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[62] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //110 cm
                            ZPROF = Body.GetSegmentProfile(yminus110, new VVector(ISO.x, ISO.y - 110.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus110, new VVector((ISO.x + 60.0), ISO.y - 110.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus110, new VVector((ISO.x + 60.0), ISO.y - 110.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[63] = ZPROF.EdgeCoordinates[0];
                                PBODY[64] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[65] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus110, new VVector(ISO.x, ISO.y + 110.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus110, new VVector((ISO.x + 60.0), ISO.y + 110.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus110, new VVector((ISO.x + 60.0), ISO.y + 110.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[66] = ZPROF.EdgeCoordinates[0];
                                PBODY[67] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[68] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //120 cm
                            ZPROF = Body.GetSegmentProfile(yminus120, new VVector(ISO.x, ISO.y - 120.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus120, new VVector((ISO.x + 60.0), ISO.y - 120.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus120, new VVector((ISO.x + 60.0), ISO.y - 120.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[69] = ZPROF.EdgeCoordinates[0];
                                PBODY[70] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[71] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus120, new VVector(ISO.x, ISO.y + 120.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus120, new VVector((ISO.x + 60.0), ISO.y + 120.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus120, new VVector((ISO.x + 60.0), ISO.y + 120.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[72] = ZPROF.EdgeCoordinates[0];
                                PBODY[73] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[74] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //130 cm
                            ZPROF = Body.GetSegmentProfile(yminus130, new VVector(ISO.x, ISO.y - 130.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus130, new VVector((ISO.x + 60.0), ISO.y - 130.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus130, new VVector((ISO.x + 60.0), ISO.y - 130.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[75] = ZPROF.EdgeCoordinates[0];
                                PBODY[76] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[77] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus130, new VVector(ISO.x, ISO.y + 130.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus130, new VVector((ISO.x + 60.0), ISO.y + 130.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus130, new VVector((ISO.x + 60.0), ISO.y + 130.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[78] = ZPROF.EdgeCoordinates[0];
                                PBODY[79] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[80] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //140 cm
                            ZPROF = Body.GetSegmentProfile(yminus140, new VVector(ISO.x, ISO.y - 140.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus140, new VVector((ISO.x + 60.0), ISO.y - 140.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus140, new VVector((ISO.x + 60.0), ISO.y - 140.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[81] = ZPROF.EdgeCoordinates[0];
                                PBODY[82] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[83] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus140, new VVector(ISO.x, ISO.y + 140.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus140, new VVector((ISO.x + 60.0), ISO.y + 140.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus140, new VVector((ISO.x + 60.0), ISO.y + 140.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[84] = ZPROF.EdgeCoordinates[0];
                                PBODY[85] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[86] = XNEGPROF.EdgeCoordinates[0];
                            }

                            //150 cm
                            ZPROF = Body.GetSegmentProfile(yminus150, new VVector(ISO.x, ISO.y - 150.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yminus150, new VVector((ISO.x + 60.0), ISO.y - 150.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yminus150, new VVector((ISO.x + 60.0), ISO.y - 150.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[87] = ZPROF.EdgeCoordinates[0];
                                PBODY[88] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[89] = XNEGPROF.EdgeCoordinates[0];
                            }

                            ZPROF = Body.GetSegmentProfile(yplus150, new VVector(ISO.x, ISO.y + 150.0, (ISO.z + 50.0)), J);
                            XPOSPROF = Body.GetSegmentProfile(yplus150, new VVector((ISO.x + 60.0), ISO.y + 150.0, ISO.z), K);    // plus 60 positive x
                            XNEGPROF = Body.GetSegmentProfile(yplus150, new VVector((ISO.x + 60.0), ISO.y + 150.0, ISO.z), L);    // minus 60 negative x

                            if (ZPROF.EdgeCoordinates.Count > 0)
                            {
                                PBODY[90] = ZPROF.EdgeCoordinates[0];
                                PBODY[91] = XPOSPROF.EdgeCoordinates[0];
                                PBODY[92] = XNEGPROF.EdgeCoordinates[0];
                            }
                            // end of contour intersection vector collection

                            // now, conduct actual analysis
                            VVector ZShift = new VVector(0.0, 0.0, 58.5);   // 58.5 cm from RADIATION SOURCE (Target) to Gantry head surface
                            VVector XShift = new VVector(40.0, 0.0, 0.0);   // 40.0 radius of gantry head
                            VVector YShift = new VVector(0.0, 40.0, 0.0);   // 40.0 radius of gantry head

                            foreach (ControlPoint point in PC)
                            {
                                VVector SOURCE = beam.GetSourceLocation(point.GantryAngle);
                                // these 4 points represent the gantry head
                                VVector RIGHTEDGE = SOURCE - ZShift + XShift;
                                VVector LEFTEDGE = SOURCE - ZShift - XShift;
                                VVector BACKEDGE = SOURCE + YShift;
                                VVector FRONTEDGE = SOURCE - YShift;
                                double DIST1 = 0.0;
                                double DIST2 = 0.0;
                                double DIST3 = 0.0;
                                double DIST4 = 0.0;

                                foreach (VVector VEC in PBODY)
                                {
                                    DIST1 = VVector.Distance(VEC, RIGHTEDGE);
                                    DIST2 = VVector.Distance(VEC, LEFTEDGE);
                                    DIST3 = VVector.Distance(VEC, BACKEDGE);
                                    DIST4 = VVector.Distance(VEC, FRONTEDGE);

                                    if ((DIST1 > 8.0 & DIST1 <= 15.0) || (DIST2 > 8.0 & DIST2 <= 15.0) || (DIST3 > 8.0 & DIST3 <= 15.0) || (DIST4 > 8.0 & DIST4 <= 15.0))
                                    {
                                        beamlocker = true;
                                        MessageBox.Show("COLLISION WARNING: THE BEAM " + beam.Id + " IN PLAN " + plan.Id + " AT CONTROL POINT " + point.Index + " HAS BETWEEN 8 AND 15 CM OF CLEARANCE FROM THE EDGE OF THE GANTRY HEAD TO THE APPROXIMATED BODY CONTOUR OF THE PATIENT.");
                                    }
                                    else if (DIST1 <= 8.0 || DIST2 <= 8.0 || DIST3 <= 8.0 || DIST4 <= 8.0)
                                    {
                                        beamlocker = true;
                                        MessageBox.Show("SEVERE COLLISION WARNING: THE BEAM " + beam.Id + " IN PLAN " + plan.Id + " AT CONTROL POINT " + point.Index + " HAS 8 CM OR LESS OF CLEARANCE FROM THE EDGE OF THE GANTRY HEAD TO THE APPROXIMATED BODY CONTOUR OF THE PATIENT!!");
                                    }
                                }
                            }
                        } // ends "if couch kick"
                    } // ends if counch angle start = couch angle end

                    if (beamlocker == false)
                    {
                        MessageBox.Show("This beam " + beam.Id + " has no risk of collison.");
                    }
                    else
                    {
                        planlocker = true;
                    }

                } //ends beam loop

                if (planlocker == false)
                {
                    MessageBox.Show("This Plan " + plan.Id + " has no risk of collison.");
                }

            } //ends plan loop
            MessageBox.Show("Collision Check has finished!");
        } // ends EXecute

    }

}
 
