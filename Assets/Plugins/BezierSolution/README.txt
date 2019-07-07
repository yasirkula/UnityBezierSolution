= Bezier Solution =

Online documentation available at: https://github.com/yasirkula/UnityBezierSolution
E-mail: yasirkula@gmail.com

1. ABOUT
This plugin helps you create bezier splines either visually in editor or by code during gameplay. Includes some utility functions like finding the closest point on the spline or travelling the spline with constant speed.

2. HOW TO
To create a new spline in the editor, follow "GameObject - Bezier Spline". Now you can select the end points of the spline in the Scene view and translate/rotate/scale or delete/duplicate them as you wish (each end point has 2 control points, which can also be translated). Here are some of the interesting properties of a spline:

- Loop: connects the first end point and the last end point of the spline
- Draw Runtime Gizmos: draws the spline during gameplay. Can be tweaked and customized using DrawGizmos/HideGizmos functions
- Handle Mode: control points of end points are handled in one of 3 ways: Free mode allows moving control points independently, Mirrored mode places the control points opposite to each other and Aligned mode ensures that both control points are aligned on a line that passes through the end point (unlike Mirrored mode, their distance to end point may differ)
- Construct Linear Path: constructs a completely linear path between the end points by using Free handle mode and adjusting the control points of end points
- Auto Construct Spline: auto adjusts the control points of end points to form a smooth spline that goes through the end points you set. There are 2 different implementations for it, with each giving a slightly different output

Framework comes with 3 additional components that may help you move objects or particles along splines. These components are located in the Utilities folder:

- BezierWalkerWithSpeed: Moves an object along a spline with constant speed. There are 3 travel modes: Once, Ping Pong and Loop. If Look Forward is selected, the object will always face forward and the smoothness of the rotation can be adjusted using the Rotation Lerp Modifier. Each time the object completes a lap, its On Path Completed () event is invoked.
- BezierWalkerWithTime: Travels a spline in Travel Time seconds. Movement Lerp Modifier parameter defines the smoothness applied to the position of the object.
- ParticlesFollowBezier: Moves particles of a Particle System in the direction of a spline. It is recommended to set the Simulation Space of the Particle System to World for increased performance. This component affects particles in one of two ways:
-- Strict: particles will strictly follow the spline. They will always be aligned to the spline and will reach the end of the spline at the end of their lifetime. This mode performs slightly better than Relaxed mode
-- Relaxed: properties of the particle system like speed, Noise and Shape will affect the movement of the particles. Particles in this mode will usually look more interesting. If you want the particles to stick with the spline, though, set their speed to 0