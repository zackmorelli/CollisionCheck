# CollisionCheck

This program is for use with ARIA/Eclipse, which is a commerical radiation treatment planning software suite made by Varian Medical Systems which is used in Radiation Oncology. This is one of several programs which I have made while working in the Radiation Oncology department at Lahey Hospital and Medical Center in Burlington, MA. I have licensed it under GPL V3 so it is open-source and publicly available.

There is also a .docx README file in the repo that describes what the program does and how it is organized.

Collision Check is an ESAPI script that analyzes RT plans and predicts if any collisions will occur between the linac and the patient or treatment table. It works for any Varian C-arm linac.

CollisionCheck supports collision detection for EBRT plans that use isocentric and non-isocentric beams and plans that involve patient support devices (like a breast board) and gantry accessories (like electron cones and SRS cones).

This is useful because the Eclipse software itself does not have collision detection capabilities. Eclipse is very much designed for radiation treatment planning. The images Eclipse renders of RT plans are great for visualizing dose, but it does not do a good job of accuratley portraying the physical location of everything in the treatment room, particularly the location of the linac gantry in relation to the patient. 

The CollisionCheck program was tested in the clinic over time and it is able to accuratley predict collsions, however it does have limitations. Please refer to the "Collision script model-limitations V2.docx" file for more information.

CollisonCheck is a very large and complex program compared to most of the other ARIA/Eclipse projects I worked on at Lahey. I worked on it over a long period of time and it has gone through several major refactors. It is also computationally expensive, partly because it takes a conservative approach to collision prediction due to the seriosuness of this topic. That is why CollisionCheck is multi-threaded to run on multiple beams of an RT plan at once. It does a good job balancing thourogness and speed.

CollisonCheck makes heavy use of a third-party library called GradientSpace for working with 3D mesh structures.

Please refer to the .docx README file for more detailed information about how CollisionCheck works.

Please note that there are two other repos, 3D Render and CollisionCheck_InfoRetreival, that are helper programs for CollisionCheck. 

CollisionCheck_InfoRetreival is a small command-line program that CollisionCheck calls for static MLC plans. It conducts database queries on behalf of CollisionCheck in order to get information about the gantry's physical location. This is neccessary for static MLC plans because the MLC control points (available via ESAPI) don't have any useful information in this case.

3D Render is used by CollisionCheck to render crude images of the patient and table and the path of the head of the linac gantry for each beam in a given RT plan. These images are crude, but they go a long way with helping the dosimtrists understand the physical location of the patient and linac gantry, which is something that the Eclipse does not do a good job of.
