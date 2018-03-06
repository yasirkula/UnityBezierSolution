using UnityEngine;
using UnityEditor;

namespace BezierSolution
{
	public static class BezierUtils
	{
		private static Color SPLINE_DETAILED_COLOR = new Color( 0.8f, 0.6f, 0.8f );

		private static Color SPLINE_GIZMO_COLOR = new Color( 0.8f, 0.6f, 0.8f );
		private const int SPLINE_GIZMO_SMOOTHNESS = 10;

		private static Color NORMAL_END_POINT_COLOR = Color.white;
		private static Color SELECTED_END_POINT_COLOR = Color.yellow;
		private static Color SELECTED_END_POINT_CONNECTED_POINTS_COLOR = Color.green;

		private static Color AUTO_CONSTRUCT_SPLINE_BUTTON_COLOR = new Color( 0.65f, 1f, 0.65f );

		private const float SPLINE_THICKNESS = 8f;
		private const float END_POINT_SIZE = 0.075f;
		private const float END_POINT_CONTROL_POINTS_SIZE = 0.05f;

		private const string PRECEDING_CONTROL_POINT_LABEL = "  <--";
		private const string FOLLOWING_CONTROL_POINT_LABEL = "  -->";

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
			if( spline.Count < 2 )
				return;

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

		public static void DrawSplineDetailed( BezierSpline spline )
		{
			if( spline.Count < 2 )
				return;

			BezierPoint endPoint0 = null, endPoint1 = null;
			for( int i = 0; i < spline.Count - 1; i++ )
			{
				endPoint0 = spline[i];
				endPoint1 = spline[i + 1];

				DrawBezier( endPoint0, endPoint1 );
			}

			if( spline.loop && endPoint1 != null )
				DrawBezier( endPoint1, spline[0] );

			// Draw tangent lines on scene view
			//Color _tmp = Handles.color;
			//Handles.color = Color.cyan;
			//for( float i = 0f; i < 1f; i += 0.05f )
			//{
			//	Handles.DrawLine( spline.GetPoint( i ), spline.GetPoint( i ) + spline.GetTangent( i ) );
			//}
			//Handles.color = _tmp;
		}

		public static void DrawSplineInspectorGUI( BezierSpline spline )
		{
			if( spline.Count < 2 )
			{
				if( GUILayout.Button( "Initialize Spline" ) )
					spline.Reset();

				return;
			}
			
			Color c = GUI.color;

			EditorGUI.BeginChangeCheck();
			bool loop = EditorGUILayout.Toggle( "Loop", spline.loop );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( spline, "Toggle Loop" );
				spline.loop = loop;

				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			bool drawGizmos = EditorGUILayout.Toggle( "Draw Runtime Gizmos", spline.drawGizmos );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( spline, "Toggle Draw Gizmos" );
				spline.drawGizmos = drawGizmos;

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();

			GUI.color = AUTO_CONSTRUCT_SPLINE_BUTTON_COLOR;

			if( GUILayout.Button( "Construct Linear Path" ) )
			{
				for( int i = 0; i < spline.Count; i++ )
					Undo.RecordObject( spline[i], "Construct Linear Path" );

				spline.ConstructLinearPath();
				SceneView.RepaintAll();
			}

			if( GUILayout.Button( "Auto Construct Spline" ) )
			{
				for( int i = 0; i < spline.Count; i++ )
					Undo.RecordObject( spline[i], "Auto Construct Spline" );

				spline.AutoConstructSpline();
				SceneView.RepaintAll();
			}

			if( GUILayout.Button( "Auto Construct Spline (method #2)" ) )
			{
				for( int i = 0; i < spline.Count; i++ )
					Undo.RecordObject( spline[i], "Auto Construct Spline" );

				spline.AutoConstructSpline2();
				SceneView.RepaintAll();
			}

			GUI.color = c;
		}

		public static void DrawBezierPoint( BezierPoint point, int pointIndex, bool isSelected )
		{
			Color c = Handles.color;

			if( isSelected )
			{
				Handles.color = SELECTED_END_POINT_COLOR;
				Handles.DotHandleCap( 0, point.position, Quaternion.identity, HandleUtility.GetHandleSize( point.position ) * END_POINT_SIZE * 1.5f, EventType.Repaint );
			}
			else
			{
				Handles.color = NORMAL_END_POINT_COLOR;

				if( Event.current.alt || Event.current.button > 0 )
					Handles.DotHandleCap( 0, point.position, Quaternion.identity, HandleUtility.GetHandleSize( point.position ) * END_POINT_SIZE, EventType.Repaint );
				else if( Handles.Button( point.position, Quaternion.identity, HandleUtility.GetHandleSize( point.position ) * END_POINT_SIZE, END_POINT_SIZE, Handles.DotHandleCap ) )
					Selection.activeTransform = point.transform;
			}

			Handles.color = c;

			Handles.DrawLine( point.position, point.precedingControlPointPosition );
			Handles.DrawLine( point.position, point.followingControlPointPosition );

			if( isSelected )
				Handles.color = SELECTED_END_POINT_CONNECTED_POINTS_COLOR;
			else
				Handles.color = NORMAL_END_POINT_COLOR;

			Handles.RectangleHandleCap( 0, point.precedingControlPointPosition, SceneView.lastActiveSceneView.rotation, HandleUtility.GetHandleSize( point.precedingControlPointPosition ) * END_POINT_CONTROL_POINTS_SIZE, EventType.Repaint );
			Handles.RectangleHandleCap( 0, point.followingControlPointPosition, SceneView.lastActiveSceneView.rotation, HandleUtility.GetHandleSize( point.followingControlPointPosition ) * END_POINT_CONTROL_POINTS_SIZE, EventType.Repaint );

			Handles.color = c;

			Handles.Label( point.position, "Point" + pointIndex );
			Handles.Label( point.precedingControlPointPosition, PRECEDING_CONTROL_POINT_LABEL );
			Handles.Label( point.followingControlPointPosition, FOLLOWING_CONTROL_POINT_LABEL );
		}

		private static void DrawBezier( BezierPoint endPoint0, BezierPoint endPoint1 )
		{
			Handles.DrawBezier( endPoint0.position, endPoint1.position,
								endPoint0.followingControlPointPosition,
								endPoint1.precedingControlPointPosition,
								SPLINE_DETAILED_COLOR, null, SPLINE_THICKNESS );
		}
	}
}