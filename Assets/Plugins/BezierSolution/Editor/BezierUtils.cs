using UnityEngine;
using UnityEditor;

public static class BezierUtils
{
	private static Color SPLINE_GIZMO_COLOR = new Color( 0.8f, 0.6f, 0.8f );
	private static int SPLINE_GIZMO_SMOOTHNESS = 10;

	[MenuItem( "GameObject/Bezier Spline", priority = 35 )]
	static void NewSpline()
	{
		GameObject spline = new GameObject( "BezierSpline", typeof( BezierSpline ) );
		Undo.RegisterCreatedObjectUndo( spline, "Create Spline" );
		Selection.activeTransform = spline.transform;
	}

	[DrawGizmo( GizmoType.NonSelected | GizmoType.Pickable )]
	static void DrawSplineGizmo( BezierSpline spline, GizmoType gizmoType )
	{
		Gizmos.color = SPLINE_GIZMO_COLOR;

		Vector3 lastPos = spline[0].position;
		float increaseAmount = 1f / ( spline.Count * SPLINE_GIZMO_SMOOTHNESS );

		for( float i = increaseAmount; i < 1f; i += increaseAmount )
		{
			Vector3 pos = spline.GetPoint( i );
			Gizmos.DrawLine( lastPos, pos );
			lastPos = pos;
		}

		Gizmos.DrawLine( lastPos, spline.GetPoint( 1f ) );
	}
}