= Bezier Solution =

Online documentation & example code available at: https://github.com/yasirkula/UnityBezierSolution
E-mail: yasirkula@gmail.com

### ABOUT
This plugin helps you create bezier splines either visually in editor or by code during gameplay. Includes some utility functions like finding the closest point on the spline or travelling the spline with constant speed. It is built upon Catlike Coding's spline tutorial: https://catlikecoding.com/unity/tutorials/curves-and-splines/


### HOW TO
To create a new spline in the editor, click "GameObject - Bezier Spline". Now you can select the end points of the spline in the Scene view and move/rotate/scale or delete/duplicate them as you wish (each end point also has 2 control points that can be moved around). Most variables have explanatory tooltips.

You can tweak the Scene view gizmos via "Project Settings/yasirkula/Bezier Solution" page (on older versions, this menu is located at "Preferences" window).

The plugin comes with some additional components that may help you move objects or particles along splines. These components are located in the Utilities folder:

- BezierWalkerWithSpeed: Moves an object along a spline with constant speed. There are 3 travel modes: Once, Ping Pong and Loop. If Look At is Forward, the object will always face forwards. If it is SplineExtraData, the extra data stored in the spline's end points is used to determine the rotation. You can modify this extra data from the points' Inspector. The smoothness of the rotation can be adjusted via Rotation Lerp Modifier. Normalized T determines the starting point. Each time the object completes a lap, its On Path Completed () event is invoked. To see this component in action without entering Play mode, click the "Simulate In Editor" button.

- BezierWalkerWithTime: Travels a spline in Travel Time seconds. Movement Lerp Modifier parameter defines the smoothness applied to the position of the object. If High Quality is enabled, the spline will be traversed with constant speed but the calculations can be more expensive.

- BezierWalkerLocomotion: Allows you to move a number of objects together with this object on a spline. This component must be attached to an object with a BezierWalker component (tail objects don't need a BezierWalker, though). Look At, Movement Lerp Modifier and Rotation Lerp Modifier parameters affect the tail objects. If tail objects jitter too much, enabling High Quality may help greatly but the calculations can be more expensive.

- ParticlesFollowBezier: Moves particles of a Particle System in the direction of a spline. It is recommended to set the Simulation Space of the Particle System to Local for increased performance. This component affects particles in one of two ways:
-- Strict: particles will strictly follow the spline. They will always be aligned to the spline and will reach the end of the spline at the end of their lifetime. This mode performs slightly better than Relaxed mode
-- Relaxed: properties of the particle system like speed, Noise and Shape will affect the movement of the particles. Particles in this mode will usually look more interesting. If you want the particles to stick with the spline, though, set their speed to 0

- BezierAttachment: Snaps an object to the specified point of the spline. You can snap the object's position and/or rotation values, optionally with some offsets. Rotation can be snapped in one of two ways:
-- Use Spline Normals: spline's normal vectors will be used to determine the object's rotation
-- Use End Point Rotations: the Transform rotation values of the spline's end points will be used to determine the object's rotation

- BezierLineRenderer: Automatically positions a Line Renderer's points so that its shape matches the target spline's shape. It is possible to match the shape of only a portion of the spline by tweaking the Spline Sample Range property. If Line Renderer's "Use World Space" property is enabled, then its points will be placed at the spline's current position. Otherwise, the points will be placed relative to the Line Renderer's position and they will rotate/scale with the Line Renderer.

- BendMeshAlongBezier: Modifies a MeshFilter's mesh to bend it in the direction of a spline. If High Quality is enabled, evenly spaced bezier points will be used so that the mesh bends uniformly but the calculations will be more expensive. If Auto Refresh is enabled, the mesh will be refreshed automatically when the spline is modified (at runtime, this has the same effect with disabling the component but in edit mode, disabling the component will restore the original mesh instead). Mesh's normal and tangent vectors can optionally be recalculated in one of two ways:
-- Modify Originals: the original mesh's normal and tangent vectors will be rotated with the spline
-- Recalculate From Scratch: Unity's RecalculateNormals and/or RecalculateTangents functions will be invoked to recalculate these vectors from scratch
Note that this component doesn't add new vertices to the original mesh, so if the original mesh doesn't have enough vertices in its bend axis, then the bent mesh will have jagged edges on complex splines.