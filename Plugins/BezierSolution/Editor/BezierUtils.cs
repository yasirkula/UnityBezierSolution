using UnityEngine;
using UnityEditor;

namespace BezierSolution.Extras
{
	public static class BezierUtils
	{
		private static readonly Color SPLINE_DETAILED_COLOR = new Color( 0.8f, 0.6f, 0.8f );

		private static readonly Color SPLINE_GIZMO_COLOR = new Color( 0.8f, 0.6f, 0.8f );
		private const int SPLINE_GIZMO_SMOOTHNESS = 10;

		private static readonly Color NORMAL_END_POINT_COLOR = Color.white;
		private static readonly Color SELECTED_END_POINT_COLOR = Color.yellow;
		private static readonly Color SELECTED_END_POINT_CONNECTED_POINTS_COLOR = Color.green;

		private static readonly Color AUTO_CONSTRUCT_SPLINE_BUTTON_COLOR = new Color( 0.65f, 1f, 0.65f );
		private static readonly GUIContent AUTO_CONSTRUCT_ALWAYS_TEXT = new GUIContent( "Always", "In Editor, apply this method automatically as spline's points change" );

		private const float SPLINE_THICKNESS = 8f;
		private const float END_POINT_SIZE = 0.075f;
		private const float END_POINT_SIZE_SELECTED = 0.075f * 1.5f;
		private const float END_POINT_CONTROL_POINTS_SIZE = 0.05f;

		private const string PRECEDING_CONTROL_POINT_LABEL = "  <--";
		private const string FOLLOWING_CONTROL_POINT_LABEL = "  -->";

		[MenuItem( "GameObject/Bezier Spline", priority = 35 )]
		private static void NewSpline( MenuCommand command )
		{
			GameObject spline = new GameObject( "BezierSpline", typeof( BezierSpline ) );
			Undo.RegisterCreatedObjectUndo( spline, "Create Spline" );
			if( command.context )
				Undo.SetTransformParent( spline.transform, ( (GameObject) command.context ).transform, "Create Spline" );

			spline.transform.localPosition = new Vector3( 0f, 0f, 0f );
			spline.transform.localRotation = Quaternion.identity;
			spline.transform.localScale = new Vector3( 1f, 1f, 1f );

			Selection.activeTransform = spline.transform;
		}

		[DrawGizmo( GizmoType.NonSelected | GizmoType.Pickable )]
		private static void DrawSplineGizmo( BezierSpline spline, GizmoType gizmoType )
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

		[DrawGizmo( GizmoType.Selected | GizmoType.NonSelected )]
		private static void DrawPointExtraDataFrustumGizmo( BezierPoint point, GizmoType gizmoType )
		{
			if( !BezierPointEditor.VisualizeExtraDataAsFrustum )
				return;

			Quaternion rotation = point.extraData;
			if( Mathf.Approximately( rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w, 1f ) )
			{
				Matrix4x4 temp = Gizmos.matrix;
				Gizmos.matrix = Matrix4x4.TRS( point.position, rotation, new Vector3( 1f, 1f, 1f ) );
				Gizmos.DrawFrustum( new Vector3( 0f, 0f, 0f ), 60f, 0.18f, 0.01f, 1.5f );
				Gizmos.matrix = temp;
			}
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

		public static void DrawSplineInspectorGUI( BezierSpline[] splines )
		{
			if( splines.Length == 0 )
				return;

			for( int i = 0; i < splines.Length; i++ )
			{
				if( splines[i].Count < 2 )
				{
					if( GUILayout.Button( "Initialize Spline" ) )
					{
						Object[] selection = Selection.objects;
						for( int j = 0; j < splines.Length; j++ )
						{
							BezierSpline spline = splines[j];
							if( spline.Count < 2 )
							{
								bool isSplineSelected = false;
								for( int k = 0; k < selection.Length; k++ )
								{
									if( selection[k] == spline || selection[k] == spline.transform || selection[k] == spline.gameObject )
									{
										isSplineSelected = true;
										break;
									}
								}

								spline.Reset();

								// Try to continue showing spline's scene gizmos after initialization by keeping
								// either the spline or a point of it selected
								if( !isSplineSelected )
								{
									System.Array.Resize( ref selection, selection.Length + 1 );
									selection[selection.Length - 1] = spline[0].gameObject;
								}
							}
						}

						Selection.objects = selection;
						GUIUtility.ExitGUI();
					}

					return;
				}
			}

			Color c = GUI.color;

			bool hasMultipleDifferentValues = false;
			bool loop = splines[0].loop;
			for( int i = 1; i < splines.Length; i++ )
			{
				if( splines[i].loop != loop )
				{
					hasMultipleDifferentValues = true;
					break;
				}
			}

			EditorGUI.showMixedValue = hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			loop = EditorGUILayout.Toggle( "Loop", loop );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					Undo.RecordObject( splines[i], "Toggle Loop" );
					splines[i].loop = loop;
				}

				SceneView.RepaintAll();
			}

			hasMultipleDifferentValues = false;
			bool drawGizmos = splines[0].drawGizmos;
			for( int i = 1; i < splines.Length; i++ )
			{
				if( splines[i].drawGizmos != drawGizmos )
				{
					hasMultipleDifferentValues = true;
					break;
				}
			}

			EditorGUI.showMixedValue = hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			drawGizmos = EditorGUILayout.Toggle( "Draw Runtime Gizmos", drawGizmos );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					Undo.RecordObject( splines[i], "Toggle Draw Gizmos" );
					splines[i].drawGizmos = drawGizmos;
				}

				SceneView.RepaintAll();
			}

			if( drawGizmos )
			{
				hasMultipleDifferentValues = false;
				Color gizmoColor = splines[0].gizmoColor;
				for( int i = 1; i < splines.Length; i++ )
				{
					if( splines[i].gizmoColor != gizmoColor )
					{
						hasMultipleDifferentValues = true;
						break;
					}
				}

				EditorGUI.showMixedValue = hasMultipleDifferentValues;
				EditorGUI.BeginChangeCheck();
				gizmoColor = EditorGUILayout.ColorField( "    Gizmo Color", gizmoColor );
				if( EditorGUI.EndChangeCheck() )
				{
					for( int i = 0; i < splines.Length; i++ )
					{
						Undo.RecordObject( splines[i], "Change Gizmo Color" );
						splines[i].gizmoColor = gizmoColor;
					}

					SceneView.RepaintAll();
				}

				hasMultipleDifferentValues = false;
				int gizmoSmoothness = splines[0].gizmoSmoothness;
				for( int i = 1; i < splines.Length; i++ )
				{
					if( splines[i].gizmoSmoothness != gizmoSmoothness )
					{
						hasMultipleDifferentValues = true;
						break;
					}
				}

				EditorGUI.showMixedValue = hasMultipleDifferentValues;
				EditorGUI.BeginChangeCheck();
				gizmoSmoothness = EditorGUILayout.IntSlider( "    Gizmo Smoothness", gizmoSmoothness, 1, 30 );
				if( EditorGUI.EndChangeCheck() )
				{
					for( int i = 0; i < splines.Length; i++ )
					{
						Undo.RecordObject( splines[i], "Change Gizmo Smoothness" );
						splines[i].gizmoSmoothness = gizmoSmoothness;
					}

					SceneView.RepaintAll();
				}
			}

			EditorGUI.showMixedValue = false;

			EditorGUILayout.Space();

			GUI.color = AUTO_CONSTRUCT_SPLINE_BUTTON_COLOR;
			ShowAutoConstructButton( splines, "Construct Linear Path", SplineAutoConstructMode.Linear );
			ShowAutoConstructButton( splines, "Auto Construct Spline", SplineAutoConstructMode.Smooth1 );
			ShowAutoConstructButton( splines, "Auto Construct Spline (method #2)", SplineAutoConstructMode.Smooth2 );
			GUI.color = c;
		}

		public static void DrawBezierPoint( BezierPoint point, int pointIndex, bool isSelected )
		{
			Color c = Handles.color;
			Event e = Event.current;

			Handles.color = isSelected ? SELECTED_END_POINT_COLOR : NORMAL_END_POINT_COLOR;
			float size = isSelected ? END_POINT_SIZE_SELECTED : END_POINT_SIZE;

			if( e.alt || e.button > 0 || ( isSelected && !e.control ) )
				Handles.DotHandleCap( 0, point.position, Quaternion.identity, HandleUtility.GetHandleSize( point.position ) * size, EventType.Repaint );
			else if( Handles.Button( point.position, Quaternion.identity, HandleUtility.GetHandleSize( point.position ) * size, size, Handles.DotHandleCap ) )
			{
				if( !e.shift && !e.control )
					Selection.activeTransform = point.transform;
				else
				{
					Object[] selection = Selection.objects;
					if( !isSelected )
					{
						// If point's spline is included in current selection, remove the spline
						// from selection since its Scene handles interfere with points' scene handles
						BezierSpline spline = point.GetComponentInParent<BezierSpline>();
						bool splineIncludedInSelection = false;
						if( spline )
						{
							for( int i = 0; i < selection.Length; i++ )
							{
								if( selection[i] == spline || selection[i] == spline.transform || selection[i] == spline.gameObject )
								{
									selection[i] = point.gameObject;
									splineIncludedInSelection = true;
									break;
								}
							}
						}

						if( !splineIncludedInSelection )
						{
							System.Array.Resize( ref selection, selection.Length + 1 );
							selection[selection.Length - 1] = point.gameObject;
						}
					}
					else
					{
						for( int i = 0; i < selection.Length; i++ )
						{
							if( selection[i] == point || selection[i] == point.transform || selection[i] == point.gameObject )
							{
								if( selection.Length == 1 )
								{
									// When all points are deselected, select the spline automatically
									BezierSpline spline = point.GetComponentInParent<BezierSpline>();
									if( spline )
									{
										selection[0] = spline.gameObject;
										break;
									}
								}

								for( int j = i + 1; j < selection.Length; j++ )
									selection[j - 1] = selection[j];

								System.Array.Resize( ref selection, selection.Length - 1 );
								break;
							}
						}
					}

					Selection.objects = selection;
				}
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

		private static void ShowAutoConstructButton( BezierSpline[] splines, string label, SplineAutoConstructMode mode )
		{
			GUILayout.BeginHorizontal();
			if( GUILayout.Button( label ) )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierSpline spline = splines[i];
					Undo.RecordObject( spline, label );
					for( int j = 0; j < spline.Count; j++ )
						Undo.RecordObject( spline[j], label );

					switch( mode )
					{
						case SplineAutoConstructMode.Linear: spline.ConstructLinearPath(); break;
						case SplineAutoConstructMode.Smooth1: spline.AutoConstructSpline(); break;
						case SplineAutoConstructMode.Smooth2: spline.AutoConstructSpline2(); break;
					}

					spline.Internal_AutoConstructMode = SplineAutoConstructMode.None;
				}

				SceneView.RepaintAll();
			}

			bool autoConstructEnabled = false;
			for( int i = 0; i < splines.Length; i++ )
			{
				if( splines[i].Internal_AutoConstructMode == mode )
				{
					autoConstructEnabled = true;
					break;
				}
			}

			EditorGUI.BeginChangeCheck();
			autoConstructEnabled = GUILayout.Toggle( autoConstructEnabled, AUTO_CONSTRUCT_ALWAYS_TEXT, GUI.skin.button, GUILayout.Width( 100f ) );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierSpline spline = splines[i];
					Undo.RecordObject( spline, "Change Autoconstruct Mode" );
					for( int j = 0; j < spline.Count; j++ )
						Undo.RecordObject( spline[j], "Change Autoconstruct Mode" );

					if( autoConstructEnabled )
					{
						spline.Internal_AutoConstructMode = mode;

						switch( mode )
						{
							case SplineAutoConstructMode.Linear: spline.ConstructLinearPath(); break;
							case SplineAutoConstructMode.Smooth1: spline.AutoConstructSpline(); break;
							case SplineAutoConstructMode.Smooth2: spline.AutoConstructSpline2(); break;
						}
					}
					else
						spline.Internal_AutoConstructMode = SplineAutoConstructMode.None;
				}

				SceneView.RepaintAll();
			}
			GUILayout.EndHorizontal();
		}

		public static void DrawSeparator()
		{
			GUILayout.Box( "", GUILayout.Height( 2f ), GUILayout.ExpandWidth( true ) );
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