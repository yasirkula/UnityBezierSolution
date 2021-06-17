using System;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;
using System.Reflection;

namespace BezierSolution.Extras
{
	public static class BezierUtils
	{
		private const string PRECEDING_CONTROL_POINT_LABEL = "  <--";
		private const string FOLLOWING_CONTROL_POINT_LABEL = "  -->";

		private static readonly Color AUTO_CONSTRUCT_SPLINE_BUTTON_COLOR = new Color( 0.65f, 1f, 0.65f );

		private static readonly GUIContent LOOP_TEXT = new GUIContent( "Loop", "Connects the first end point and the last end point of the spline" );
		private static readonly GUIContent DRAW_RUNTIME_GIZMOS_TEXT = new GUIContent( "Draw Runtime Gizmos", "Draws the spline during gameplay" );
		private static readonly GUIContent SHOW_CONTROL_POINTS_TEXT = new GUIContent( "Show Control Points", "Shows control points of the end points in Scene window" );
		private static readonly GUIContent SHOW_DIRECTIONS_TEXT = new GUIContent( "Show Directions", "Shows control points' directions in Scene window" );
		private static readonly GUIContent SHOW_POINT_INDICES_TEXT = new GUIContent( "Show Point Indices", "Shows end points' indices in Scene window" );
		private static readonly GUIContent SHOW_NORMALS_TEXT = new GUIContent( "Show Normals", "Shows end points' normal vectors in Scene window" );
		private static readonly GUIContent AUTO_CALCULATED_NORMALS_ANGLE_TEXT = new GUIContent( "Auto Calculated Normals Angle", "When 'Auto Calculate Normals' button is clicked, all normals will be rotated around their Z axis by the specified amount (each end point's rotation angle can further be customized from the end point's Inspector)" );
		private static readonly GUIContent CONSTRUCT_LINEAR_PATH_TEXT = new GUIContent( "Construct Linear Path", "Constructs a completely linear path (end points' Handle Mode will be set to Free)" );
		private static readonly GUIContent AUTO_CONSTRUCT_SPLINE_TEXT = new GUIContent( "Auto Construct Spline", "Constructs a smooth path" );
		private static readonly GUIContent AUTO_CONSTRUCT_SPLINE_2_TEXT = new GUIContent( "Auto Construct Spline 2", "Constructs a smooth path (another algorithm)" );
		private static readonly GUIContent AUTO_CALCULATE_NORMALS_TEXT = new GUIContent( "Auto Calculate Normals", "Attempts to automatically calculate the end points' normal vectors" );
		private static readonly GUIContent AUTO_CONSTRUCT_ALWAYS_TEXT = new GUIContent( "Always", "Applies this method automatically as spline's points change" );
		private static readonly GUIContent QUICK_EDIT_MODE_TEXT = new GUIContent( "Quick Edit Mode", "Quickly add new points to the spline or snap existing points to the scene geometry" );
		private static readonly GUIContent QUICK_EDIT_MODIFY_NORMALS_TEXT = new GUIContent( "Use Raycast Normals", "While dragging a point or adding a new point, the point's Normal vector will be set to the normal of the scene geometry under the cursor" );
		private static readonly GUIContent QUICK_EDIT_PRESERVE_SPLINE_SHAPE_TEXT = new GUIContent( "Preserve Spline Shape", "While inserting new points along the spline, the spline's shape will be preserved but the neighboring end points' 'Handle Mode' will no longer be 'Mirrored'" );

		public static readonly GUILayoutOption GL_WIDTH_45 = GUILayout.Width( 45f );
		public static readonly GUILayoutOption GL_WIDTH_60 = GUILayout.Width( 60f );
		public static readonly GUILayoutOption GL_WIDTH_100 = GUILayout.Width( 100f );
		public static readonly GUILayoutOption GL_WIDTH_155 = GUILayout.Width( 155f );

		private static readonly MethodInfo intersectRayMeshMethod = typeof( HandleUtility ).GetMethod( "IntersectRayMesh", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );

		public static bool QuickEditSplineMode { get; private set; }

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

			// Make sure that none of the points of the spline are selected
			if( BezierPointEditor.ActiveEditor && Array.IndexOf( BezierPointEditor.ActiveEditor.allSplines, spline ) >= 0 )
				return;

			Gizmos.color = BezierSettings.NormalSplineColor;

			Vector3 lastPos = spline[0].position;
			float increaseAmount = 1f / ( spline.Count * BezierSettings.SplineSmoothness );

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
			if( !BezierSettings.VisualizeExtraDataAsFrustum )
				return;

			// If the either the point or its spline isn't selected, don't show frustum of the point
			if( ( !BezierSplineEditor.ActiveEditor || Array.IndexOf( BezierSplineEditor.ActiveEditor.allSplines, point.spline ) < 0 ) &&
				( !BezierPointEditor.ActiveEditor || Array.IndexOf( BezierPointEditor.ActiveEditor.allSplines, point.spline ) < 0 ) )
				return;

			Quaternion rotation = point.extraData;
			if( Mathf.Approximately( rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w, 1f ) )
			{
				Matrix4x4 temp = Gizmos.matrix;
				Gizmos.matrix = Matrix4x4.TRS( point.position, rotation, Vector3.one * ( BezierSettings.ExtraDataAsFrustumSize * HandleUtility.GetHandleSize( point.position ) ) );
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

			Color c = GUI.backgroundColor;

			EditorGUI.showMixedValue = HasMultipleDifferentValues( splines, ( s1, s2 ) => s1.loop == s2.loop );
			EditorGUI.BeginChangeCheck();
			bool loop = EditorGUILayout.Toggle( LOOP_TEXT, splines[0].loop );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierSpline spline = splines[i];
					Undo.RecordObject( spline, "Toggle Loop" );
					spline.loop = loop;
					SetSplineDirtyWithUndo( spline, "Toggle Loop", InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange );
				}

				SceneView.RepaintAll();
			}

			EditorGUI.showMixedValue = HasMultipleDifferentValues( splines, ( s1, s2 ) => s1.drawGizmos == s2.drawGizmos );
			EditorGUI.BeginChangeCheck();
			bool drawGizmos = EditorGUILayout.Toggle( DRAW_RUNTIME_GIZMOS_TEXT, splines[0].drawGizmos );
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
			bool showControlPoints = EditorGUILayout.Toggle( SHOW_CONTROL_POINTS_TEXT, BezierSettings.ShowControlPoints );
			if( EditorGUI.EndChangeCheck() )
			{
				BezierSettings.ShowControlPoints = showControlPoints;
				SceneView.RepaintAll();
			}

			if( showControlPoints )
			{
				EditorGUI.indentLevel++;
				EditorGUI.BeginChangeCheck();
				bool showControlPointDirections = EditorGUILayout.Toggle( SHOW_DIRECTIONS_TEXT, BezierSettings.ShowControlPointDirections );
				if( EditorGUI.EndChangeCheck() )
				{
					BezierSettings.ShowControlPointDirections = showControlPointDirections;
					SceneView.RepaintAll();
				}
				EditorGUI.indentLevel--;
			}

			EditorGUI.BeginChangeCheck();
			bool showEndPointLabels = EditorGUILayout.Toggle( SHOW_POINT_INDICES_TEXT, BezierSettings.ShowEndPointLabels );
			if( EditorGUI.EndChangeCheck() )
			{
				BezierSettings.ShowEndPointLabels = showEndPointLabels;
				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			bool showNormals = EditorGUILayout.Toggle( SHOW_NORMALS_TEXT, BezierSettings.ShowNormals );
			if( EditorGUI.EndChangeCheck() )
			{
				BezierSettings.ShowNormals = showNormals;
				SceneView.RepaintAll();
			}

			if( showNormals )
			{
				EditorGUI.indentLevel++;
				EditorGUI.BeginChangeCheck();
				float normalsPreviewLength = EditorGUILayout.FloatField( "Preview Length", BezierSettings.NormalsPreviewLength );
				if( EditorGUI.EndChangeCheck() )
				{
					BezierSettings.NormalsPreviewLength = Mathf.Max( 0f, normalsPreviewLength );
					SceneView.RepaintAll();
				}
				EditorGUI.indentLevel--;
			}

			EditorGUI.showMixedValue = HasMultipleDifferentValues( splines, ( s1, s2 ) => s1.autoCalculatedNormalsAngle == s2.autoCalculatedNormalsAngle );
			EditorGUI.BeginChangeCheck();
			float autoCalculatedNormalsAngle = EditorGUILayout.FloatField( AUTO_CALCULATED_NORMALS_ANGLE_TEXT, splines[0].autoCalculatedNormalsAngle );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					Undo.RecordObject( splines[i], "Change Normals Angle" );
					splines[i].autoCalculatedNormalsAngle = autoCalculatedNormalsAngle;
					SetSplineDirtyWithUndo( splines[i], "Change Normals Angle", InternalDirtyFlags.NormalOffsetChange );
				}

				SceneView.RepaintAll();
			}

			EditorGUI.showMixedValue = false;

			EditorGUILayout.Space();

			GUI.backgroundColor = AUTO_CONSTRUCT_SPLINE_BUTTON_COLOR;
			ShowAutoConstructButton( splines, CONSTRUCT_LINEAR_PATH_TEXT, SplineAutoConstructMode.Linear );
			ShowAutoConstructButton( splines, AUTO_CONSTRUCT_SPLINE_TEXT, SplineAutoConstructMode.Smooth1 );
			ShowAutoConstructButton( splines, AUTO_CONSTRUCT_SPLINE_2_TEXT, SplineAutoConstructMode.Smooth2 );

			GUILayout.BeginHorizontal();
			if( GUILayout.Button( AUTO_CALCULATE_NORMALS_TEXT ) )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierSpline spline = splines[i];
					Undo.RecordObject( spline, "Auto Calculate Normals" );

					try
					{
						spline.autoCalculateNormals = true;
						SetSplineDirtyWithUndo( spline, "Auto Calculate Normals", InternalDirtyFlags.NormalOffsetChange );
					}
					finally
					{
						spline.autoCalculateNormals = false;
					}
				}

				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			bool autoCalculateNormalsEnabled = GUILayout.Toggle( Array.Find( splines, ( s ) => s.autoCalculateNormals ), AUTO_CONSTRUCT_ALWAYS_TEXT, GUI.skin.button, EditorGUIUtility.wideMode ? GL_WIDTH_100 : GL_WIDTH_60 );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierSpline spline = splines[i];
					Undo.RecordObject( spline, "Change Auto Calculate Normals" );
					spline.autoCalculateNormals = autoCalculateNormalsEnabled;

					if( autoCalculateNormalsEnabled )
						SetSplineDirtyWithUndo( spline, "Change Auto Calculate Normals", InternalDirtyFlags.NormalOffsetChange );
				}

				SceneView.RepaintAll();
			}
			GUILayout.EndHorizontal();

			GUI.backgroundColor = c;

			EditorGUILayout.Space();

			EditorGUI.BeginChangeCheck();
			QuickEditSplineMode = GUILayout.Toggle( QuickEditSplineMode, QUICK_EDIT_MODE_TEXT, GUI.skin.button );
			if( EditorGUI.EndChangeCheck() )
			{
				EditorApplication.update -= SceneView.RepaintAll;

				if( QuickEditSplineMode )
				{
					Tools.hidden = true;
					EditorApplication.update += SceneView.RepaintAll;
				}
				else if( BezierSplineEditor.ActiveEditor )
					Tools.hidden = false;

				SceneView.RepaintAll();
			}

			if( QuickEditSplineMode )
			{
				EditorGUILayout.HelpBox( "- Dragging a point: snaps the dragged point to the scene geometry under the cursor\n- CTRL+Left Click: adds a new point to the end of the spline\n- CTRL+Shift+Left Click: inserts a new point along the spline\n- Shift+Left Click: deletes clicked point", MessageType.Info );

				if( Array.Find( splines, ( s ) => !s.autoCalculateNormals ) )
				{
					EditorGUI.indentLevel++;
					BezierSettings.QuickEditSplineModifyNormals = EditorGUILayout.Toggle( QUICK_EDIT_MODIFY_NORMALS_TEXT, BezierSettings.QuickEditSplineModifyNormals );
					EditorGUI.indentLevel--;
				}

				if( Array.Find( splines, ( s ) => s.autoConstructMode == SplineAutoConstructMode.None ) )
				{
					EditorGUI.indentLevel++;
					BezierSettings.QuickEditSplinePreserveShape = EditorGUILayout.Toggle( QUICK_EDIT_PRESERVE_SPLINE_SHAPE_TEXT, BezierSettings.QuickEditSplinePreserveShape );
					EditorGUI.indentLevel--;
				}
			}
		}

		public static void DrawBezierPoint( BezierPoint point, int pointIndex, bool isSelected )
		{
			Color c = Handles.color;
			Event e = Event.current;

			if( QuickEditSplineMode )
				isSelected = false;

			Handles.color = isSelected ? BezierSettings.SelectedEndPointColor : BezierSettings.NormalEndPointColor;
			float size = isSelected ? BezierSettings.SelectedEndPointSize : BezierSettings.EndPointSize;

			if( QuickEditSplineMode )
			{
				if( e.alt || e.control )
					Handles.DotHandleCap( 0, point.position, Quaternion.identity, HandleUtility.GetHandleSize( point.position ) * size, EventType.Repaint );
				else if( !e.shift )
				{
					// Shift isn't held: move dragged points

					// Draw a ScaleValueHandle for the sole purpose of detecting drag input
					EditorGUI.BeginChangeCheck();
					Handles.ScaleValueHandle( 1f, point.position, Quaternion.identity, HandleUtility.GetHandleSize( point.position ) * size * 6.5f, Handles.DotHandleCap, 1f );
					if( EditorGUI.EndChangeCheck() )
					{
						// Point is dragged, snap it to the scene geometry
						Vector3 sceneHitPoint, sceneHitNormal;
						RaycastAgainstScene( point.spline[point.spline.Count - 1], out sceneHitPoint, out sceneHitNormal );

						Undo.RecordObject( point.transform, "Move point" );
						point.transform.position = sceneHitPoint;

						if( BezierSettings.QuickEditSplineModifyNormals && !point.spline.autoCalculateNormals )
						{
							Undo.RecordObject( point, "Move point" );
							point.normal = sceneHitNormal;
						}
					}
				}
				else
				{
					// Shift is held: delete clicked points

					// Disallow deleting points from splines with only 2 or less points
					if( point.spline.Count <= 2 )
						Handles.DotHandleCap( 0, point.position, Quaternion.identity, HandleUtility.GetHandleSize( point.position ) * size, EventType.Repaint );
					else
					{
						Handles.color = BezierSettings.QuickEditModeDeleteEndPointColor;

						if( Handles.Button( point.position, Quaternion.identity, HandleUtility.GetHandleSize( point.position ) * size, size, Handles.DotHandleCap ) )
						{
							// When the selected point is deleted, automatically select the next point so that there is still an active BezierPointEditor
							// to continue editing this spline
							Object[] selection = Selection.objects;
							int pointIndexInSelection = Array.IndexOf( selection, point.gameObject );
							if( pointIndexInSelection >= 0 )
								selection[pointIndexInSelection] = point.spline[( point.index + 1 ) % point.spline.Count].gameObject;

							Undo.DestroyObjectImmediate( point.gameObject );

							if( pointIndexInSelection >= 0 )
								Selection.objects = selection;
						}
					}
				}
			}
			else if( e.alt || e.button > 0 || ( isSelected && !e.control ) )
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

			if( BezierSettings.ShowControlPoints )
			{
				Handles.DrawLine( point.position, point.precedingControlPointPosition );
				Handles.DrawLine( point.position, point.followingControlPointPosition );

				Handles.color = isSelected ? BezierSettings.SelectedControlPointColor : BezierSettings.NormalControlPointColor;

				Handles.RectangleHandleCap( 0, point.precedingControlPointPosition, SceneView.lastActiveSceneView.rotation, HandleUtility.GetHandleSize( point.precedingControlPointPosition ) * BezierSettings.ControlPointSize, EventType.Repaint );
				Handles.RectangleHandleCap( 0, point.followingControlPointPosition, SceneView.lastActiveSceneView.rotation, HandleUtility.GetHandleSize( point.followingControlPointPosition ) * BezierSettings.ControlPointSize, EventType.Repaint );

				Handles.color = c;
			}

			if( BezierSettings.ShowEndPointLabels )
				Handles.Label( point.position, "Point" + pointIndex );

			if( BezierSettings.ShowControlPoints && BezierSettings.ShowControlPointDirections )
			{
				Handles.Label( point.precedingControlPointPosition, PRECEDING_CONTROL_POINT_LABEL );
				Handles.Label( point.followingControlPointPosition, FOLLOWING_CONTROL_POINT_LABEL );
			}

			if( BezierSettings.ShowNormals )
			{
				Handles.color = BezierSettings.NormalsPreviewColor;
				Handles.DrawLine( point.position, point.position + point.normal * HandleUtility.GetHandleSize( point.position ) * BezierSettings.NormalsPreviewLength );
				Handles.color = c;
			}
		}

		public static void QuickEditModeSceneGUI( BezierSpline[] splines )
		{
			Event e = Event.current;
			GUIContent QUICK_EDIT_MODE_TEXT = new GUIContent( "QUICK EDIT SPLINE MODE" );

			GUIStyle style = "PreOverlayLabel"; // Taken from: https://github.com/Unity-Technologies/UnityCsReference/blob/f78f4093c8a2b45949a847cdc704cf209dcf2f36/Editor/Mono/EditorGUI.cs#L629
			Rect multiEditTipRect = new Rect( new Vector2( 0f, 5f ), style.CalcSize( QUICK_EDIT_MODE_TEXT ) );
			multiEditTipRect.x = ( EditorGUIUtility.currentViewWidth - multiEditTipRect.width ) * 0.5f; // Center the text

			Handles.BeginGUI();
			EditorGUI.DropShadowLabel( multiEditTipRect, QUICK_EDIT_MODE_TEXT, style );
			Handles.EndGUI();

			if( splines.Length == 0 || e.alt || !e.control || GUIUtility.hotControl != 0 )
				return;

			if( !e.shift )
			{
				// Shift isn't held: add new point to the closest spline when LMB is pressed

				// Get a line that starts from Scene camera's position and goes at cursor's direction
				Ray ray = HandleUtility.GUIPointToWorldRay( e.mousePosition );
				Vector3 lineStart = ray.origin;
				Vector3 lineEnd = lineStart + ray.direction * 2500f;

				// Find the spline end point closest to this line
				BezierPoint closestEndPoint = null;
				float closestEndPointDistance = float.PositiveInfinity;
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierPoint point = splines[i][splines[i].Count - 1];
					float pointDistance = HandleUtility.DistancePointLine( point.position, lineStart, lineEnd );
					if( pointDistance <= closestEndPointDistance )
					{
						closestEndPoint = point;
						closestEndPointDistance = pointDistance;
					}
				}

				if( closestEndPoint )
				{
					Vector3 sceneHitPoint, sceneHitNormal;
					RaycastAgainstScene( closestEndPoint, out sceneHitPoint, out sceneHitNormal );

					// Draw a line from the closest end point to the raycast hit point
					Color c = Handles.color;
					Handles.color = BezierSettings.QuickEditModeNewEndPointColor;
					Handles.DotHandleCap( 0, sceneHitPoint, Quaternion.identity, HandleUtility.GetHandleSize( sceneHitPoint ) * BezierSettings.QuickEditModeNewEndPointSize, EventType.Repaint );
					Handles.DrawLine( sceneHitPoint, closestEndPoint.position );
					Handles.color = c;

					// When left clicked, insert a point at the highlighted position
					if( e.type == EventType.MouseDown && e.button == 0 )
					{
						BezierPoint newPoint = closestEndPoint.spline.InsertNewPointAt( closestEndPoint.spline.Count );
						newPoint.position = sceneHitPoint;
						if( BezierSettings.QuickEditSplineModifyNormals && !closestEndPoint.spline.autoCalculateNormals )
							newPoint.normal = sceneHitNormal;

						// Rotate the previous point's followingControlPointPosition in the direction of the new point and assign the resulting vector
						// to the new point's followingControlPointPosition
						Vector3 directionToNewPoint = sceneHitPoint - closestEndPoint.position;
						Quaternion controlPointDeltaRotation = Quaternion.FromToRotation( closestEndPoint.followingControlPointPosition - closestEndPoint.position, directionToNewPoint );
						newPoint.followingControlPointPosition = sceneHitPoint + controlPointDeltaRotation * ( directionToNewPoint * 0.35f );

						Undo.RegisterCreatedObjectUndo( newPoint.gameObject, "Insert Point" );
						if( newPoint.transform.parent )
							Undo.RegisterCompleteObjectUndo( newPoint.transform.parent, "Insert Point" );

						e.Use();
					}
				}
			}
			else
			{
				// Shift is held: insert point to closest spline when LMB is pressed

				// Get a line that starts from Scene camera's position and goes at cursor's direction
				Ray ray = HandleUtility.GUIPointToWorldRay( e.mousePosition );
				Vector3 lineStart = ray.origin;
				Vector3 lineEnd = lineStart + ray.direction * 2500f;

				// Find the spline point closest to this line
				BezierSpline closestSpline = null;
				Vector3 closestPointOnSpline = Vector3.zero;
				float closestPointDistance = float.PositiveInfinity;
				float closestPointNormalizedT = 0f;
				for( int i = 0; i < splines.Length; i++ )
				{
					Vector3 pointOnLine;
					Vector3 pointOnSpline = splines[i].FindNearestPointToLine( lineStart, lineEnd, out pointOnLine, out closestPointNormalizedT );
					float pointDistance = ( pointOnLine - pointOnSpline ).sqrMagnitude;
					if( pointDistance <= closestPointDistance )
					{
						closestSpline = splines[i];
						closestPointOnSpline = pointOnSpline;
						closestPointDistance = pointDistance;
					}
				}

				if( closestSpline )
				{
					Color c = Handles.color;
					Handles.color = BezierSettings.QuickEditModeNewEndPointColor;
					Handles.DotHandleCap( 0, closestPointOnSpline, Quaternion.identity, HandleUtility.GetHandleSize( closestPointOnSpline ) * BezierSettings.QuickEditModeNewEndPointSize, EventType.Repaint );
					Handles.color = c;

					// When left clicked, insert a point at the highlighted position
					if( e.type == EventType.MouseDown && e.button == 0 )
					{
						BezierSpline.Segment segment = closestSpline.GetSegmentAt( closestPointNormalizedT );
						bool preserveSplineShape = BezierSettings.QuickEditSplinePreserveShape && closestSpline.autoConstructMode == SplineAutoConstructMode.None;

						Vector3 position, precedingControlPointPosition, followingControlPointPosition;
						BezierPointEditor.CalculateInsertedPointPosition( segment.point1, segment.point2, segment.localT, preserveSplineShape, out position, out precedingControlPointPosition, out followingControlPointPosition );

						BezierPoint newPoint = closestSpline.InsertNewPointAt( segment.point2.index );
						newPoint.position = position;
						if( preserveSplineShape )
						{
							newPoint.handleMode = BezierPoint.HandleMode.Aligned;
							newPoint.precedingControlPointPosition = precedingControlPointPosition;
							newPoint.followingControlPointPosition = followingControlPointPosition;
						}
						else
						{
							Vector3 precedingDirection = precedingControlPointPosition - position;
							Vector3 followingDirection = followingControlPointPosition - position;
							newPoint.followingControlPointPosition = position + followingDirection.normalized * Mathf.Min( precedingDirection.magnitude, followingDirection.magnitude );
						}

						Undo.RegisterCreatedObjectUndo( newPoint.gameObject, "Insert Point" );
						if( newPoint.transform.parent )
							Undo.RegisterCompleteObjectUndo( newPoint.transform.parent, "Insert Point" );

						e.Use();
					}
				}
			}
		}

		private static void ShowAutoConstructButton( BezierSpline[] splines, GUIContent label, SplineAutoConstructMode mode )
		{
			GUILayout.BeginHorizontal();
			if( GUILayout.Button( label ) )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierSpline spline = splines[i];
					Undo.RecordObject( spline, label.text );

					try
					{
						spline.autoConstructMode = mode;
						SetSplineDirtyWithUndo( spline, label.text, InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange );
					}
					finally
					{
						spline.autoConstructMode = SplineAutoConstructMode.None;
					}
				}

				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			bool autoConstructEnabled = GUILayout.Toggle( Array.Find( splines, ( s ) => s.autoConstructMode == mode ), AUTO_CONSTRUCT_ALWAYS_TEXT, GUI.skin.button, EditorGUIUtility.wideMode ? GL_WIDTH_100 : GL_WIDTH_60 );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < splines.Length; i++ )
				{
					BezierSpline spline = splines[i];
					Undo.RecordObject( spline, "Change Autoconstruct Mode" );

					if( autoConstructEnabled )
					{
						spline.autoConstructMode = mode;
						SetSplineDirtyWithUndo( spline, "Change Autoconstruct Mode", InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange );
					}
					else
						spline.autoConstructMode = SplineAutoConstructMode.None;
				}

				SceneView.RepaintAll();
			}
			GUILayout.EndHorizontal();
		}

		internal static void SetSplineDirtyWithUndo( BezierSpline spline, string undo, InternalDirtyFlags dirtyFlags )
		{
			if( spline.autoCalculateNormals || spline.autoConstructMode != SplineAutoConstructMode.None )
			{
				for( int i = 0; i < spline.Count; i++ )
				{
					Undo.RecordObject( spline[i], undo );
					Undo.RecordObject( spline[i].transform, undo );
				}
			}

			spline.dirtyFlags |= dirtyFlags;
			spline.CheckDirty();
		}

		private static void RaycastAgainstScene( BezierPoint referencePoint, out Vector3 position, out Vector3 normal )
		{
			EventType eventType = Event.current.type;
			Ray ray = HandleUtility.GUIPointToWorldRay( Event.current.mousePosition );

			// First, try raycasting against scene geometry with or without colliders (it doesn't matter)
			// Credit: https://forum.unity.com/threads/editor-raycast-against-scene-meshes-without-collider-editor-select-object-using-gui-coordinate.485502
			if( intersectRayMeshMethod != null && eventType != EventType.Layout && eventType != EventType.Repaint ) // HandleUtility.PickGameObject doesn't work with Layout and Repaint events in OnSceneGUI
			{
				GameObject gameObjectUnderCursor = HandleUtility.PickGameObject( Event.current.mousePosition, false );
				if( gameObjectUnderCursor )
				{
					Mesh meshUnderCursor = null;
					MeshFilter meshFilter = gameObjectUnderCursor.GetComponent<MeshFilter>();
					if( meshFilter )
						meshUnderCursor = meshFilter.sharedMesh;

					if( !meshUnderCursor )
					{
						SkinnedMeshRenderer skinnedMeshRenderer = gameObjectUnderCursor.GetComponent<SkinnedMeshRenderer>();
						if( skinnedMeshRenderer )
							meshUnderCursor = skinnedMeshRenderer.sharedMesh;
					}

					if( meshUnderCursor )
					{
						object[] rayMeshParameters = new object[] { ray, meshUnderCursor, gameObjectUnderCursor.transform.localToWorldMatrix, null };
						if( (bool) intersectRayMeshMethod.Invoke( null, rayMeshParameters ) )
						{
							RaycastHit hit = (RaycastHit) rayMeshParameters[3];
							position = hit.point;
							normal = hit.normal.normalized;

							return;
						}
					}
				}
			}

			// Raycast against scene geometry with colliders
			object raycastResult = HandleUtility.RaySnap( ray );
			if( raycastResult != null && raycastResult is RaycastHit )
			{
				position = ( (RaycastHit) raycastResult ).point;
				normal = ( (RaycastHit) raycastResult ).normal.normalized;

				return;
			}

			// Raycast against a plane that goes through referencePoint
			if( referencePoint )
			{
				Plane plane = new Plane( referencePoint.normal, referencePoint.position );
				float enter;
				if( plane.Raycast( ray, out enter ) )
				{
					position = ray.GetPoint( enter );
					normal = referencePoint.normal;

					return;
				}
			}

			position = ray.GetPoint( 5f );
			normal = Vector3.up;
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
								BezierSettings.SelectedSplineColor, null, BezierSettings.SplineThickness );
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