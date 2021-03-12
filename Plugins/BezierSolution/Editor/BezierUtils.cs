using System;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

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
		private static readonly Color END_POINT_NORMALS_COLOR = Color.blue;

		private static readonly Color AUTO_CONSTRUCT_SPLINE_BUTTON_COLOR = new Color( 0.65f, 1f, 0.65f );
		private static readonly GUIContent AUTO_CONSTRUCT_ALWAYS_TEXT = new GUIContent( "Always", "In Editor, apply this method automatically as spline's points change" );

		public static readonly GUILayoutOption GL_WIDTH_45 = GUILayout.Width( 45f );
		public static readonly GUILayoutOption GL_WIDTH_60 = GUILayout.Width( 60f );
		public static readonly GUILayoutOption GL_WIDTH_100 = GUILayout.Width( 100f );
		public static readonly GUILayoutOption GL_WIDTH_155 = GUILayout.Width( 155f );

		private const float SPLINE_THICKNESS = 8f;
		private const float END_POINT_SIZE = 0.075f;
		private const float END_POINT_SIZE_SELECTED = 0.075f * 1.5f;
		private const float END_POINT_CONTROL_POINTS_SIZE = 0.05f;
		private const float END_POINT_NORMALS_SIZE = 0.35f;

		private const string PRECEDING_CONTROL_POINT_LABEL = "  <--";
		private const string FOLLOWING_CONTROL_POINT_LABEL = "  -->";

		private const string SHOW_CONTROL_POINTS_PREF = "BezierSolution_ShowControlPoints";
		private const string SHOW_CONTROL_POINT_DIRECTIONS_PREF = "BezierSolution_ShowControlPointDirs";
		private const string SHOW_END_POINTS_LABELS_PREF = "BezierSolution_ShowEndPointLabels";
		private const string SHOW_NORMALS_PREF = "BezierSolution_ShowNormals";

		private static bool? m_showControlPoints = null;
		public static bool ShowControlPoints
		{
			get
			{
				if( m_showControlPoints == null )
					m_showControlPoints = EditorPrefs.GetBool( SHOW_CONTROL_POINTS_PREF, true );

				return m_showControlPoints.Value;
			}
			set
			{
				m_showControlPoints = value;
				EditorPrefs.SetBool( SHOW_CONTROL_POINTS_PREF, value );
			}
		}

		private static bool? m_showControlPointDirections = null;
		public static bool ShowControlPointDirections
		{
			get
			{
				if( m_showControlPointDirections == null )
					m_showControlPointDirections = EditorPrefs.GetBool( SHOW_CONTROL_POINT_DIRECTIONS_PREF, true );

				return m_showControlPointDirections.Value;
			}
			set
			{
				m_showControlPointDirections = value;
				EditorPrefs.SetBool( SHOW_CONTROL_POINT_DIRECTIONS_PREF, value );
			}
		}

		private static bool? m_showEndPointLabels = null;
		public static bool ShowEndPointLabels
		{
			get
			{
				if( m_showEndPointLabels == null )
					m_showEndPointLabels = EditorPrefs.GetBool( SHOW_END_POINTS_LABELS_PREF, true );

				return m_showEndPointLabels.Value;
			}
			set
			{
				m_showEndPointLabels = value;
				EditorPrefs.SetBool( SHOW_END_POINTS_LABELS_PREF, value );
			}
		}

		private static bool? m_showNormals = null;
		public static bool ShowNormals
		{
			get
			{
				if( m_showNormals == null )
					m_showNormals = EditorPrefs.GetBool( SHOW_NORMALS_PREF, true );

				return m_showNormals.Value;
			}
			set
			{
				m_showNormals = value;
				EditorPrefs.SetBool( SHOW_NORMALS_PREF, value );
			}
		}

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
									Array.Resize( ref selection, selection.Length + 1 );
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

			EditorGUI.showMixedValue = HasMultipleDifferentValues( splines, ( s1, s2 ) => s1.loop == s2.loop );
			EditorGUI.BeginChangeCheck();
			bool loop = EditorGUILayout.Toggle( "Loop", splines[0].loop );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierSpline spline = splines[i];
					Undo.RecordObject( spline, "Toggle Loop" );
					spline.loop = loop;
					spline.Internal_SetDirtyImmediatelyWithUndo( "Toggle Loop" );
				}

				SceneView.RepaintAll();
			}

			EditorGUI.showMixedValue = HasMultipleDifferentValues( splines, ( s1, s2 ) => s1.drawGizmos == s2.drawGizmos );
			EditorGUI.BeginChangeCheck();
			bool drawGizmos = EditorGUILayout.Toggle( "Draw Runtime Gizmos", splines[0].drawGizmos );
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
				EditorGUI.indentLevel++;

				EditorGUI.showMixedValue = HasMultipleDifferentValues( splines, ( s1, s2 ) => s1.gizmoColor == s2.gizmoColor );
				EditorGUI.BeginChangeCheck();
				Color gizmoColor = EditorGUILayout.ColorField( "Gizmo Color", splines[0].gizmoColor );
				if( EditorGUI.EndChangeCheck() )
				{
					for( int i = 0; i < splines.Length; i++ )
					{
						Undo.RecordObject( splines[i], "Change Gizmo Color" );
						splines[i].gizmoColor = gizmoColor;
					}

					SceneView.RepaintAll();
				}

				EditorGUI.showMixedValue = HasMultipleDifferentValues( splines, ( s1, s2 ) => s1.gizmoSmoothness == s2.gizmoSmoothness );
				EditorGUI.BeginChangeCheck();
				int gizmoSmoothness = EditorGUILayout.IntSlider( "Gizmo Smoothness", splines[0].gizmoSmoothness, 1, 30 );
				if( EditorGUI.EndChangeCheck() )
				{
					for( int i = 0; i < splines.Length; i++ )
					{
						Undo.RecordObject( splines[i], "Change Gizmo Smoothness" );
						splines[i].gizmoSmoothness = gizmoSmoothness;
					}

					SceneView.RepaintAll();
				}

				EditorGUI.indentLevel--;
			}

			EditorGUI.showMixedValue = false;

			EditorGUI.BeginChangeCheck();
			bool showControlPoints = EditorGUILayout.Toggle( "Show Control Points", ShowControlPoints );
			if( EditorGUI.EndChangeCheck() )
			{
				ShowControlPoints = showControlPoints;
				SceneView.RepaintAll();
			}

			if( showControlPoints )
			{
				EditorGUI.indentLevel++;
				EditorGUI.BeginChangeCheck();
				bool showControlPointDirections = EditorGUILayout.Toggle( "Show Directions", ShowControlPointDirections );
				if( EditorGUI.EndChangeCheck() )
				{
					ShowControlPointDirections = showControlPointDirections;
					SceneView.RepaintAll();
				}
				EditorGUI.indentLevel--;
			}

			EditorGUI.BeginChangeCheck();
			bool showEndPointLabels = EditorGUILayout.Toggle( "Show Point Indices", ShowEndPointLabels );
			if( EditorGUI.EndChangeCheck() )
			{
				ShowEndPointLabels = showEndPointLabels;
				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			bool showNormals = EditorGUILayout.Toggle( "Show Normals", ShowNormals );
			if( EditorGUI.EndChangeCheck() )
			{
				ShowNormals = showNormals;
				SceneView.RepaintAll();
			}

			EditorGUI.showMixedValue = HasMultipleDifferentValues( splines, ( s1, s2 ) => s1.Internal_AutoCalculatedNormalsAngle == s2.Internal_AutoCalculatedNormalsAngle );
			EditorGUI.BeginChangeCheck();
			float autoCalculatedNormalsAngle = EditorGUILayout.FloatField( "Auto Calculated Normals Angle", splines[0].Internal_AutoCalculatedNormalsAngle );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					Undo.RecordObject( splines[i], "Change Normals Angle" );
					splines[i].Internal_AutoCalculatedNormalsAngle = autoCalculatedNormalsAngle;
					splines[i].Internal_SetDirtyImmediatelyWithUndo( "Change Normals Angle" );
				}

				SceneView.RepaintAll();
			}

			EditorGUI.showMixedValue = false;

			EditorGUILayout.Space();

			GUI.color = AUTO_CONSTRUCT_SPLINE_BUTTON_COLOR;
			ShowAutoConstructButton( splines, "Construct Linear Path", SplineAutoConstructMode.Linear );
			ShowAutoConstructButton( splines, "Auto Construct Spline", SplineAutoConstructMode.Smooth1 );
			ShowAutoConstructButton( splines, "Auto Construct Spline 2", SplineAutoConstructMode.Smooth2 );

			GUILayout.BeginHorizontal();
			if( GUILayout.Button( "Auto Calculate Normals" ) )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierSpline spline = splines[i];
					Undo.RecordObject( spline, "Auto Calculate Normals" );

					try
					{
						spline.Internal_AutoCalculateNormals = true;
						spline.Internal_SetDirtyImmediatelyWithUndo( "Auto Calculate Normals" );
					}
					finally
					{
						spline.Internal_AutoCalculateNormals = false;
					}
				}

				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			bool autoCalculateNormalsEnabled = GUILayout.Toggle( Array.Find( splines, ( s ) => s.Internal_AutoCalculateNormals ), AUTO_CONSTRUCT_ALWAYS_TEXT, GUI.skin.button, EditorGUIUtility.wideMode ? GL_WIDTH_100 : GL_WIDTH_60 );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierSpline spline = splines[i];
					Undo.RecordObject( spline, "Change Auto Calculate Normals" );
					spline.Internal_AutoCalculateNormals = autoCalculateNormalsEnabled;
					spline.Internal_SetDirtyImmediatelyWithUndo( "Change Auto Calculate Normals" );
				}

				SceneView.RepaintAll();
			}
			GUILayout.EndHorizontal();

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
							Array.Resize( ref selection, selection.Length + 1 );
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

								Array.Resize( ref selection, selection.Length - 1 );
								break;
							}
						}
					}

					Selection.objects = selection;
				}
			}

			Handles.color = c;

			if( ShowControlPoints )
			{
				Handles.DrawLine( point.position, point.precedingControlPointPosition );
				Handles.DrawLine( point.position, point.followingControlPointPosition );

				if( isSelected )
					Handles.color = SELECTED_END_POINT_CONNECTED_POINTS_COLOR;
				else
					Handles.color = NORMAL_END_POINT_COLOR;

				Handles.RectangleHandleCap( 0, point.precedingControlPointPosition, SceneView.lastActiveSceneView.rotation, HandleUtility.GetHandleSize( point.precedingControlPointPosition ) * END_POINT_CONTROL_POINTS_SIZE, EventType.Repaint );
				Handles.RectangleHandleCap( 0, point.followingControlPointPosition, SceneView.lastActiveSceneView.rotation, HandleUtility.GetHandleSize( point.followingControlPointPosition ) * END_POINT_CONTROL_POINTS_SIZE, EventType.Repaint );

				Handles.color = c;
			}

			if( ShowEndPointLabels )
				Handles.Label( point.position, "Point" + pointIndex );

			if( ShowControlPoints && ShowControlPointDirections )
			{
				Handles.Label( point.precedingControlPointPosition, PRECEDING_CONTROL_POINT_LABEL );
				Handles.Label( point.followingControlPointPosition, FOLLOWING_CONTROL_POINT_LABEL );
			}

			if( ShowNormals )
			{
				Handles.color = END_POINT_NORMALS_COLOR;
				Handles.DrawLine( point.position, point.position + point.normal * END_POINT_NORMALS_SIZE );
				Handles.color = c;
			}
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

					try
					{
						spline.Internal_AutoConstructMode = mode;
						spline.Internal_SetDirtyImmediatelyWithUndo( label );
					}
					finally
					{
						spline.Internal_AutoConstructMode = SplineAutoConstructMode.None;
					}
				}

				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			bool autoConstructEnabled = GUILayout.Toggle( Array.Find( splines, ( s ) => s.Internal_AutoConstructMode == mode ), AUTO_CONSTRUCT_ALWAYS_TEXT, GUI.skin.button, EditorGUIUtility.wideMode ? GL_WIDTH_100 : GL_WIDTH_60 );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierSpline spline = splines[i];
					Undo.RecordObject( spline, "Change Autoconstruct Mode" );

					if( autoConstructEnabled )
					{
						spline.Internal_AutoConstructMode = mode;
						spline.Internal_SetDirtyImmediatelyWithUndo( "Change Autoconstruct Mode" );
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

		private static bool HasMultipleDifferentValues( BezierSpline[] splines, Func<BezierSpline, BezierSpline, bool> comparer )
		{
			if( splines.Length <= 1 )
				return false;

			for( int i = 1; i < splines.Length; i++ )
			{
				if( !comparer( splines[0], splines[i] ) )
					return true;
			}

			return false;
		}
	}
}