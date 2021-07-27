using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

using g3;


/*
    Collision Check - Helper Classes


    Description/information:
    This contains helper classes used by the program, mostly the classes used to store ESAPI information, which makes multithreading possible. 

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




namespace CollisionCheck
{

    //This class is used by the CollionCheck method to output the collision information to the GUI.
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
