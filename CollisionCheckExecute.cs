using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;


/*
    Collision Check - CollisionCheckExecute


    Description/information:
    This houses the method which handles the parallel multithreading for each beam in the plan.
    GUI calls the method kept here, which in turn uses a parallel for each loop, which is part of the TPL, to run the collision analysis method for each beam in BEAMLIST simultaneously, or however the OS decides to handle it.
    It also makes the CollisionAlert list and returns it to GUI.

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
    public class CollisionCheckExecuteClass
    {
        public static List<CollisionAlert> CollisionCheckExecute(List<BEAM> BEAMLIST, TextBox ProgOutput, string Acc, bool FAST)
        {
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
                    // CollisionCheckAnalysis is the actual collision engine where
                    List<CollisionAlert> tempcol = CollisionCheckAnalysis.BeamCollisionAnalysis(beam, ProgOutput, Acc, FAST);
                    collist.Add(tempcol);
                }); // ends beam loop
            }
            catch (Exception e)
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
        }
    }
}
