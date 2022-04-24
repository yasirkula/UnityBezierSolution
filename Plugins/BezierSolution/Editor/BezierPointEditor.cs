using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BezierSolution.Extras
{
	[CustomEditor( typeof( BezierPoint ) )]
	[CanEditMultipleObjects]
	public class BezierPointEditor : Editor
	{
		private enum PointInsertionMode { None = 0, Simple = 1, PreserveShape = 2 };

		private class SplineHolder
		{
			public BezierSpline spline;
			public BezierPoint[] points;

			public SplineHolder( BezierSpline spline, BezierPoint[] points )
			{
				this.spline = spline;
				this.points = points;
			}

			public void SortPoints( bool forwards )
			{
				if( forwards )
					System.Array.Sort( points, CompareForwards );
				else
					System.Array.Sort( points, CompareBackwards );
			}

			private int CompareForwards( BezierPoint x, BezierPoint y )
			{
				return x.index.CompareTo( y.index );
			}

			private int CompareBackwards( BezierPoint x, BezierPoint y )
			{
				return y.index.CompareTo( x.index );
			}
		}

		private const float CONTROL_POINTS_MINIMUM_SAFE_DISTANCE_SQR = 0.05f * 0.05f;

		private static readonly Color RESET_POINT_BUTTON_COLOR = new Color( 1f, 1f, 0.65f, 1f );
		private static readonly Color REMOVE_POINT_BUTTON_COLOR = new Color( 1f, 0.65f, 0.65f, 1f );
		private static readonly GUIContent MULTI_EDIT_TIP = new GUIContent( "Tip: Hold Shift to affect all points' Transforms" );
		private static readonly GUIContent OPPOSITE_TRANSFORMATION_OFF_TIP = new GUIContent( "(in THE SAME direction - hit C to toggle)" );
		private static readonly GUIContent OPPOSITE_TRANSFORMATION_ON_TIP = new GUIContent( "(in OPPOSITE directions - hit C to toggle)" );
		private static readonly GUIContent INSERT_POINT_PRESERVE_SHAPE = new GUIContent( "Preserve Shape", "Spline's shape will be preserved but the neighboring end points' 'Handle Mode' will no longer be 'Mirrored'" );
		private static readonly GUIContent APPLY_TO_ALL_POINTS = new GUIContent( "All", "Apply to all points in the selected spline(s)" );
		private static readonly GUIContent HANDLE_MODE_TIP = new GUIContent( "Handle Mode", "Control points are handled in one of 3 ways:\n-Free: allows moving control points independently\n-Mirrored: places the control points opposite to each other\n-Aligned: ensures that both control points are aligned on a line that passes through the end point (unlike Mirrored mode, their distance to end point may differ)" );
		private static readonly GUIContent EXTRA_DATA_TIP = new GUIContent( "Extra Data", "Each end point can store additional data that holds 4 floats. You can interpolate between points' extra data via spline's GetExtraData function. For example, extra data can be used to assign a custom rotation value to each end point that can be read by BezierWalker components" );
		private static readonly GUIContent NORMALS_SET_TO_CAMERA_FORWARD = new GUIContent( "C", "Set to Scene camera's forward direction" );
		private static readonly GUIContent NORMALS_LOOK_AT_CAMERA = new GUIContent( "L", "Look towards Scene camera's current position" );
		private static readonly GUIContent NORMALIZE_NORMALS_TIP = new GUIContent( "Normalize Normals", "Sets normal vectors' length to 1" );
		private static readonly GUIContent EXTRA_DATA_SET_AS_CAMERA_FORWARD = new GUIContent( "C", "Set as Scene camera's current rotation" );
		private static readonly GUIContent EXTRA_DATA_VIEW_AS_FRUSTUM = new GUIContent( "V", "Visualize data as camera frustum in Scene" );

		private SplineHolder[] selection;
		internal BezierSpline[] allSplines;
		private BezierPoint[] allPoints;
		private int pointCount;

		private Vector3[] pointInitialPositions;
		private Quaternion[] pointInitialRotations;
		private Vector3[] pointInitialScales;
		private Vector3[] precedingPointInitialPositions;
		private Vector3[] followingPointInitialPositions;
		private bool allPointsModified;

		private Quaternion[] precedingPointRotations;
		private Quaternion[] followingPointRotations;
		private bool controlPointRotationsInitialized;

		private Tool previousTool = Tool.None;

		public static BezierPointEditor ActiveEditor { get; private set; }

		private void OnEnable()
		{
			Object[] points = targets;
			pointCount = points.Length;
			allPoints = new BezierPoint[pointCount];

			pointInitialPositions = new Vector3[pointCount];
			pointInitialRotations = new Quaternion[pointCount];
			pointInitialScales = new Vector3[pointCount];
			precedingPointInitialPositions = new Vector3[pointCount];
			followingPointInitialPositions = new Vector3[pointCount];

			precedingPointRotations = new Quaternion[pointCount];
			followingPointRotations = new Quaternion[pointCount];
			controlPointRotationsInitialized = false;

			if( pointCount == 1 )
			{
				BezierPoint point = (BezierPoint) points[0];

				selection = new SplineHolder[1] { new SplineHolder( point.GetComponentInParent<BezierSpline>(), new BezierPoint[1] { point } ) };
				allSplines = selection[0].spline ? new BezierSpline[1] { selection[0].spline } : new BezierSpline[0];
				allPoints[0] = point;
			}
			else
			{
				Dictionary<BezierSpline, List<BezierPoint>> lookupTable = new Dictionary<BezierSpline, List<BezierPoint>>( pointCount );
				List<BezierPoint> nullSplinePoints = null;

				for( int i = 0; i < pointCount; i++ )
				{
					BezierPoint point = (BezierPoint) points[i];
					BezierSpline spline = point.GetComponentInParent<BezierSpline>();
					if( !spline )
					{
						spline = null;

						if( nullSplinePoints == null )
							nullSplinePoints = new List<BezierPoint>( pointCount );

						nullSplinePoints.Add( point );
					}
					else
					{
						List<BezierPoint> _points;
						if( !lookupTable.TryGetValue( spline, out _points ) )
						{
							_points = new List<BezierPoint>( pointCount );
							lookupTable[spline] = _points;
						}

						_points.Add( point );
					}

					allPoints[i] = point;
				}

				int index;
				if( nullSplinePoints != null )
				{
					index = 1;
					selection = new SplineHolder[lookupTable.Count + 1];
					selection[0] = new SplineHolder( null, nullSplinePoints.ToArray() );
				}
				else
				{
					index = 0;
					selection = new SplineHolder[lookupTable.Count];
				}

				int index2 = 0;
				allSplines = new BezierSpline[lookupTable.Count];

				foreach( var element in lookupTable )
				{
					selection[index++] = new SplineHolder( element.Key, element.Value.ToArray() );
					allSplines[index2++] = element.Key;
				}
			}

			for( int i = 0; i < selection.Length; i++ )
			{
				selection[i].SortPoints( true );

				if( selection[i].spline )
					selection[i].spline.Refresh();
			}

			ActiveEditor = this;

			Tools.hidden = true;

			if( BezierUtils.QuickEditSplineMode )
			{
				EditorApplication.update -= SceneView.RepaintAll;
				EditorApplication.update += SceneView.RepaintAll;
			}

			Undo.undoRedoPerformed -= OnUndoRedo;
			Undo.undoRedoPerformed += OnUndoRedo;
		}

		private void OnDisable()
		{
			ActiveEditor = null;
			Tools.hidden = false;

			Undo.undoRedoPerformed -= OnUndoRedo;
			EditorApplication.update -= SceneView.RepaintAll;
		}

		private void OnSceneGUI()
		{
			BezierPoint point = (BezierPoint) target;
			if( !point )
				return;

			if( CheckCommands() )
				return;

			Event e = Event.current;
			Tool tool = Tools.current;

			// OnSceneGUI is called separately for each selected point, make sure that the spline is drawn only once, not multiple times
			if( point == allPoints[0] )
			{
				for( int i = 0; i < selection.Length; i++ )
				{
					BezierSpline spline = selection[i].spline;
					if( spline )
					{
						BezierPoint[] points = selection[i].points;
						BezierUtils.DrawSplineDetailed( spline );
						for( int j = 0, k = 0; j < spline.Count; j++ )
						{
							bool isSelected = spline[j] == points[k];
							if( isSelected && k < points.Length - 1 )
								k++;

							if( !isSelected )
								BezierUtils.DrawBezierPoint( spline[j], j + 1, false );
						}
					}
				}

				if( !BezierUtils.QuickEditSplineMode && allPoints.Length > 1 && ( tool == Tool.Move || tool == Tool.Rotate || tool == Tool.Scale ) )
				{
					GUIStyle style = "PreOverlayLabel"; // Taken from: https://github.com/Unity-Technologies/UnityCsReference/blob/f78f4093c8a2b45949a847cdc704cf209dcf2f36/Editor/Mono/EditorGUI.cs#L629
					Rect multiEditTipRect = new Rect( Vector2.zero, style.CalcSize( MULTI_EDIT_TIP ) );

					Handles.BeginGUI();

					EditorGUI.DropShadowLabel( multiEditTipRect, MULTI_EDIT_TIP, style );
					if( tool == Tool.Move || tool == Tool.Rotate )
					{
						Rect multiEditOppositeTransformationTipRect = new Rect( new Vector2( multiEditTipRect.width + 4f, 0f ), style.CalcSize( BezierSettings.MoveMultiplePointsInOppositeDirections ? OPPOSITE_TRANSFORMATION_ON_TIP : OPPOSITE_TRANSFORMATION_OFF_TIP ) );
						EditorGUI.DropShadowLabel( multiEditOppositeTransformationTipRect, BezierSettings.MoveMultiplePointsInOppositeDirections ? OPPOSITE_TRANSFORMATION_ON_TIP : OPPOSITE_TRANSFORMATION_OFF_TIP, style );
					}

					Handles.EndGUI();

					if( e.type == EventType.KeyUp && e.keyCode == KeyCode.C && ( tool == Tool.Move || tool == Tool.Rotate ) )
						BezierSettings.MoveMultiplePointsInOppositeDirections = !BezierSettings.MoveMultiplePointsInOppositeDirections;
				}

				if( e.type == EventType.MouseDown && e.button == 0 )
				{
					// Cache initial Transform values of the points
					for( int i = 0; i < allPoints.Length; i++ )
					{
						BezierPoint p = allPoints[i];

						pointInitialPositions[i] = p.position;
						pointInitialRotations[i] = p.rotation;
						pointInitialScales[i] = p.localScale;
						precedingPointInitialPositions[i] = p.precedingControlPointPosition;
						followingPointInitialPositions[i] = p.followingControlPointPosition;
					}

					allPointsModified = false;
				}
			}

			// When Control key is pressed, BezierPoint gizmos should be drawn on top of Transform handles in order to allow selecting/deselecting points
			// If Alt key is pressed, Transform handles aren't drawn at all, so BezierPoint gizmos can be drawn immediately
			// When in point insertion mode, handles aren't drawn and BezierPoint gizmos must be drawn immediately
			if( e.alt || ( !e.control && !e.command ) || BezierUtils.QuickEditSplineMode )
				BezierUtils.DrawBezierPoint( point, point.index + 1, true );

			if( BezierSettings.ShowEvenlySpacedPoints )
			{
				if( point == allPoints[0] )
				{
					for( int i = 0; i < allSplines.Length; i++ )
						BezierUtils.DrawSplineEvenlySpacedPoints( allSplines[i] );
				}
			}

			if( BezierUtils.QuickEditSplineMode )
			{
				// Execute quick edit mode's scene GUI only once (otherwise things can get ugly when multiple points are selected)
				if( point == allPoints[0] )
				{
					BezierUtils.QuickEditModeSceneGUI( allSplines );
					HandleUtility.AddDefaultControl( 0 );
				}

				return;
			}

			// Camera rotates with Alt key, don't interfere
			if( e.alt )
				return;

			int pointIndex = -1;
			for( int i = 0; i < allPoints.Length; i++ )
			{
				if( allPoints[i] == point )
				{
					pointIndex = i;
					break;
				}
			}

			if( previousTool != tool )
			{
				controlPointRotationsInitialized = false;
				previousTool = tool;
			}

			// Draw Transform handles for control points
			switch( Tools.current )
			{
				case Tool.Move:
					if( !controlPointRotationsInitialized )
					{
						for( int i = 0; i < allPoints.Length; i++ )
						{
							BezierPoint p = allPoints[i];

							precedingPointRotations[i] = Quaternion.LookRotation( p.precedingControlPointPosition - p.position );
							followingPointRotations[i] = Quaternion.LookRotation( p.followingControlPointPosition - p.position );
						}

						controlPointRotationsInitialized = true;
					}

					// No need to show gizmos for control points in Autoconstruct mode
					Vector3 position;
					if( BezierSettings.ShowControlPoints && ( !point.spline || point.spline.autoConstructMode == SplineAutoConstructMode.None ) )
					{
						EditorGUI.BeginChangeCheck();
						position = Handles.PositionHandle( point.precedingControlPointPosition, Tools.pivotRotation == PivotRotation.Local ? precedingPointRotations[pointIndex] : Quaternion.identity );
						if( EditorGUI.EndChangeCheck() )
						{
							Undo.RecordObject( point, "Move Control Point" );
							point.precedingControlPointPosition = position;

							if( e.shift && allPoints.Length > 1 )
							{
								Vector3 delta = Matrix4x4.TRS( precedingPointInitialPositions[pointIndex], Tools.pivotRotation == PivotRotation.Local ? precedingPointRotations[pointIndex] : Quaternion.identity, Vector3.Distance( precedingPointInitialPositions[pointIndex], point.position ) * Vector3.one ).inverse.MultiplyPoint3x4( position );
								if( BezierSettings.MoveMultiplePointsInOppositeDirections )
									delta = -delta;

								for( int i = 0; i < allPoints.Length; i++ )
								{
									if( i != pointIndex )
									{
										Undo.RecordObject( allPoints[i], "Move Control Point" );
										allPoints[i].precedingControlPointPosition = Matrix4x4.TRS( precedingPointInitialPositions[i], Tools.pivotRotation == PivotRotation.Local ? precedingPointRotations[i] : Quaternion.identity, Vector3.Distance( precedingPointInitialPositions[i], allPoints[i].position ) * Vector3.one ).MultiplyPoint3x4( delta );
									}
								}

								allPointsModified = true;
							}
							else if( !e.shift && allPointsModified ) // If shift is released before the left mouse button, reset other points' positions
							{
								for( int i = 0; i < allPoints.Length; i++ )
								{
									if( i != pointIndex )
									{
										Undo.RecordObject( allPoints[i], "Move Control Point" );
										allPoints[i].precedingControlPointPosition = precedingPointInitialPositions[i];
									}
								}

								allPointsModified = false;
							}
						}

						EditorGUI.BeginChangeCheck();
						position = Handles.PositionHandle( point.followingControlPointPosition, Tools.pivotRotation == PivotRotation.Local ? followingPointRotations[pointIndex] : Quaternion.identity );
						if( EditorGUI.EndChangeCheck() )
						{
							Undo.RecordObject( point, "Move Control Point" );
							point.followingControlPointPosition = position;

							if( e.shift && allPoints.Length > 1 )
							{
								Vector3 delta = Matrix4x4.TRS( followingPointInitialPositions[pointIndex], Tools.pivotRotation == PivotRotation.Local ? followingPointRotations[pointIndex] : Quaternion.identity, Vector3.Distance( followingPointInitialPositions[pointIndex], point.position ) * Vector3.one ).inverse.MultiplyPoint3x4( position );
								if( BezierSettings.MoveMultiplePointsInOppositeDirections )
									delta = -delta;

								for( int i = 0; i < allPoints.Length; i++ )
								{
									if( i != pointIndex )
									{
										Undo.RecordObject( allPoints[i], "Move Control Point" );
										allPoints[i].followingControlPointPosition = Matrix4x4.TRS( followingPointInitialPositions[i], Tools.pivotRotation == PivotRotation.Local ? followingPointRotations[i] : Quaternion.identity, Vector3.Distance( followingPointInitialPositions[i], allPoints[i].position ) * Vector3.one ).MultiplyPoint3x4( delta );
									}
								}

								allPointsModified = true;
							}
							else if( !e.shift && allPointsModified ) // If shift is released before the left mouse button, reset other points' positions
							{
								for( int i = 0; i < allPoints.Length; i++ )
								{
									if( i != pointIndex )
									{
										Undo.RecordObject( allPoints[i], "Move Control Point" );
										allPoints[i].followingControlPointPosition = followingPointInitialPositions[i];
									}
								}

								allPointsModified = false;
							}
						}
					}

					EditorGUI.BeginChangeCheck();
					position = Handles.PositionHandle( point.position, Tools.pivotRotation == PivotRotation.Local ? point.rotation : Quaternion.identity );
					if( EditorGUI.EndChangeCheck() )
					{
						Undo.RecordObject( point, "Move Point" );
						Undo.RecordObject( point.transform, "Move Point" );
						point.position = position;

						if( e.shift && allPoints.Length > 1 )
						{
							Vector3 delta = position - pointInitialPositions[pointIndex];
							if( BezierSettings.MoveMultiplePointsInOppositeDirections )
								delta = -delta;

							for( int i = 0; i < allPoints.Length; i++ )
							{
								if( i != pointIndex )
								{
									Undo.RecordObject( allPoints[i], "Move Point" );
									Undo.RecordObject( allPoints[i].transform, "Move Point" );
									allPoints[i].position = pointInitialPositions[i] + delta;
								}
							}

							allPointsModified = true;
						}
						else if( !e.shift && allPointsModified ) // If shift is released before the left mouse button, reset other points' positions
						{
							for( int i = 0; i < allPoints.Length; i++ )
							{
								if( i != pointIndex )
								{
									Undo.RecordObject( allPoints[i], "Move Point" );
									Undo.RecordObject( allPoints[i].transform, "Move Point" );
									allPoints[i].position = pointInitialPositions[i];
								}
							}

							allPointsModified = false;
						}
					}

					break;
				case Tool.Rotate:
					Quaternion handleRotation;
					if( Tools.pivotRotation == PivotRotation.Local )
					{
						handleRotation = point.rotation;
						controlPointRotationsInitialized = false;
					}
					else
					{
						if( !controlPointRotationsInitialized )
						{
							for( int i = 0; i < allPoints.Length; i++ )
								precedingPointRotations[i] = Quaternion.identity;

							controlPointRotationsInitialized = true;
						}

						handleRotation = precedingPointRotations[pointIndex];
					}

					EditorGUI.BeginChangeCheck();
					Quaternion rotation = Handles.RotationHandle( handleRotation, point.position );
					if( EditorGUI.EndChangeCheck() )
					{
						// "rotation * Quaternion.Inverse( handleRotation )": World-space delta rotation
						// "delta rotation * point.rotation": Applying world-space delta rotation to current rotation
						Quaternion pointFinalRotation = rotation * Quaternion.Inverse( handleRotation ) * point.rotation;

						Undo.RecordObject( point.transform, "Rotate Point" );
						point.rotation = pointFinalRotation;

						if( e.shift && allPoints.Length > 1 )
						{
							Quaternion delta = pointFinalRotation * Quaternion.Inverse( pointInitialRotations[pointIndex] );
							if( BezierSettings.MoveMultiplePointsInOppositeDirections )
								delta = Quaternion.Inverse( delta );

							for( int i = 0; i < allPoints.Length; i++ )
							{
								if( i != pointIndex )
								{
									Undo.RecordObject( allPoints[i].transform, "Rotate Point" );
									allPoints[i].rotation = delta * pointInitialRotations[i];
								}
							}

							allPointsModified = true;
						}
						else if( !e.shift && allPointsModified ) // If shift is released before the left mouse button, reset other points' rotations
						{
							for( int i = 0; i < allPoints.Length; i++ )
							{
								if( i != pointIndex )
								{
									Undo.RecordObject( allPoints[i].transform, "Rotate Point" );
									allPoints[i].rotation = pointInitialRotations[i];
								}
							}

							allPointsModified = false;
						}

						if( Tools.pivotRotation == PivotRotation.Global )
							precedingPointRotations[pointIndex] = rotation;
					}

					break;
				case Tool.Scale:
					EditorGUI.BeginChangeCheck();
					Vector3 scale = Handles.ScaleHandle( point.localScale, point.position, point.rotation, HandleUtility.GetHandleSize( point.position ) );
					if( EditorGUI.EndChangeCheck() )
					{
						Undo.RecordObject( point.transform, "Scale Point" );
						point.localScale = scale;

						if( e.shift && allPoints.Length > 1 )
						{
							Vector3 delta = new Vector3( 1f, 1f, 1f );
							Vector3 prevScale = pointInitialScales[pointIndex];
							if( prevScale.x != 0f )
								delta.x = scale.x / prevScale.x;
							if( prevScale.y != 0f )
								delta.y = scale.y / prevScale.y;
							if( prevScale.z != 0f )
								delta.z = scale.z / prevScale.z;

							for( int i = 0; i < allPoints.Length; i++ )
							{
								if( i != pointIndex )
								{
									prevScale = pointInitialScales[i];
									prevScale.Scale( delta );

									Undo.RecordObject( allPoints[i].transform, "Scale Point" );
									allPoints[i].localScale = prevScale;
								}
							}

							allPointsModified = true;
						}
						else if( !e.shift && allPointsModified ) // If shift is released before the left mouse button, reset other points' scales
						{
							for( int i = 0; i < allPoints.Length; i++ )
							{
								if( i != pointIndex )
								{
									Undo.RecordObject( allPoints[i].transform, "Scale Point" );
									allPoints[i].localScale = pointInitialScales[i];
								}
							}

							allPointsModified = false;
						}
					}

					break;
			}

			if( e.control || e.command )
				BezierUtils.DrawBezierPoint( point, point.index + 1, true );
		}

		public override void OnInspectorGUI()
		{
			if( CheckCommands() )
				GUIUtility.ExitGUI();

			if( allSplines.Length == 0 )
			{
				EditorGUILayout.HelpBox( "Selected point(s) aren't children of a BezierSpline!", MessageType.Error );

				if( GUILayout.Button( "Refresh" ) )
					OnEnable();

				return;
			}

			BezierUtils.DrawSplineInspectorGUI( allSplines );

			if( BezierUtils.QuickEditSplineMode )
				return;

			EditorGUILayout.Space();
			BezierUtils.DrawSeparator();

			GUILayout.BeginHorizontal();

			if( GUILayout.Button( "<-", BezierUtils.GL_WIDTH_45 ) )
			{
				Object[] newSelection = new Object[pointCount];
				for( int i = 0, index = 0; i < selection.Length; i++ )
				{
					BezierSpline spline = selection[i].spline;
					BezierPoint[] points = selection[i].points;

					if( spline )
					{
						for( int j = 0; j < points.Length; j++ )
						{
							int prevIndex = points[j].index - 1;
							if( prevIndex < 0 )
								prevIndex = spline.Count - 1;

							newSelection[index++] = spline[prevIndex].gameObject;
						}
					}
					else
					{
						for( int j = 0; j < points.Length; j++ )
							newSelection[index++] = points[j].gameObject;
					}
				}

				Selection.objects = newSelection;
				GUIUtility.ExitGUI();
			}

			string pointIndex = ( pointCount == 1 && selection[0].spline ) ? ( allPoints[0].index + 1 ).ToString() : "-";
			string splineLength = ( selection.Length == 1 && selection[0].spline ) ? selection[0].spline.Count.ToString() : "-";
			GUILayout.Box( "Selected Point: " + pointIndex + " / " + splineLength, GUILayout.ExpandWidth( true ) );

			if( GUILayout.Button( "->", BezierUtils.GL_WIDTH_45 ) )
			{
				Object[] newSelection = new Object[pointCount];
				for( int i = 0, index = 0; i < selection.Length; i++ )
				{
					BezierSpline spline = selection[i].spline;
					BezierPoint[] points = selection[i].points;

					if( spline )
					{
						for( int j = 0; j < points.Length; j++ )
						{
							int nextIndex = points[j].index + 1;
							if( nextIndex >= spline.Count )
								nextIndex = 0;

							newSelection[index++] = spline[nextIndex].gameObject;
						}
					}
					else
					{
						for( int j = 0; j < points.Length; j++ )
							newSelection[index++] = points[j].gameObject;
					}
				}

				Selection.objects = newSelection;
				GUIUtility.ExitGUI();
			}

			GUILayout.EndHorizontal();

			EditorGUILayout.Space();

			if( GUILayout.Button( "Decrement Point's Index" ) )
			{
				Undo.IncrementCurrentGroup();

				for( int i = 0; i < selection.Length; i++ )
				{
					BezierSpline spline = selection[i].spline;
					if( spline )
					{
						selection[i].SortPoints( true );

						BezierPoint[] points = selection[i].points;
						int[] newIndices = new int[points.Length];
						for( int j = 0; j < points.Length; j++ )
						{
							int index = points[j].index;
							int newIndex = index - 1;
							if( newIndex < 0 )
								newIndex = spline.Count - 1;

							newIndices[j] = newIndex;
						}

						for( int j = 0; j < points.Length; j++ )
							spline.ChangePointIndex( points[j].index, newIndices[j], "Change point index" );

						selection[i].SortPoints( true );
					}
				}

				SceneView.RepaintAll();
			}

			if( GUILayout.Button( "Increment Point's Index" ) )
			{
				Undo.IncrementCurrentGroup();

				for( int i = 0; i < selection.Length; i++ )
				{
					BezierSpline spline = selection[i].spline;
					if( spline )
					{
						selection[i].SortPoints( false );

						BezierPoint[] points = selection[i].points;
						int[] newIndices = new int[points.Length];
						for( int j = 0; j < points.Length; j++ )
						{
							int index = points[j].index;
							int newIndex = index + 1;
							if( newIndex >= spline.Count )
								newIndex = 0;

							newIndices[j] = newIndex;
						}

						for( int j = 0; j < points.Length; j++ )
							spline.ChangePointIndex( points[j].index, newIndices[j], "Change point index" );

						selection[i].SortPoints( true );
					}
				}

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();

			bool allSplinesUsingAutoConstructMode = !System.Array.Find( allSplines, ( s ) => s.autoConstructMode == SplineAutoConstructMode.None );
			bool anySplineUsingAutoCalculateNormals = System.Array.Find( allSplines, ( s ) => s.autoCalculateNormals );

			GUILayout.BeginHorizontal();
			if( GUILayout.Button( "Insert Point Before" ) )
				InsertNewPoints( false, false );
			if( !allSplinesUsingAutoConstructMode && GUILayout.Button( INSERT_POINT_PRESERVE_SHAPE, EditorGUIUtility.wideMode ? BezierUtils.GL_WIDTH_155 : BezierUtils.GL_WIDTH_100 ) )
				InsertNewPoints( false, true );
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if( GUILayout.Button( "Insert Point After" ) )
				InsertNewPoints( true, false );
			if( !allSplinesUsingAutoConstructMode && GUILayout.Button( INSERT_POINT_PRESERVE_SHAPE, EditorGUIUtility.wideMode ? BezierUtils.GL_WIDTH_155 : BezierUtils.GL_WIDTH_100 ) )
				InsertNewPoints( true, true );
			GUILayout.EndHorizontal();

			EditorGUILayout.Space();

			if( GUILayout.Button( "Duplicate Point" ) )
				DuplicateSelectedPoints();

			EditorGUILayout.Space();

			GUI.enabled = !allSplinesUsingAutoConstructMode;

			EditorGUI.showMixedValue = HasMultipleDifferentValues( ( p1, p2 ) => p1.handleMode == p2.handleMode );
			EditorGUI.BeginChangeCheck();
			BezierPoint.HandleMode handleMode = (BezierPoint.HandleMode) EditorGUILayout.EnumPopup( HANDLE_MODE_TIP, allPoints[0].handleMode );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < allPoints.Length; i++ )
				{
					Undo.RecordObject( allPoints[i], "Change Point Handle Mode" );
					allPoints[i].handleMode = handleMode;
				}

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();

			EditorGUI.showMixedValue = HasMultipleDifferentValues( ( p1, p2 ) => p1.precedingControlPointLocalPosition == p2.precedingControlPointLocalPosition );
			EditorGUI.BeginChangeCheck();
			Vector3 precedingControlPointLocalPosition = EditorGUILayout.Vector3Field( "Preceding Control Point Local Position", allPoints[0].precedingControlPointLocalPosition );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < allPoints.Length; i++ )
				{
					Undo.RecordObject( allPoints[i], "Change Point Position" );
					allPoints[i].precedingControlPointLocalPosition = precedingControlPointLocalPosition;
				}

				SceneView.RepaintAll();
			}

			EditorGUI.showMixedValue = HasMultipleDifferentValues( ( p1, p2 ) => p1.followingControlPointLocalPosition == p2.followingControlPointLocalPosition );
			EditorGUI.BeginChangeCheck();
			Vector3 followingControlPointLocalPosition = EditorGUILayout.Vector3Field( "Following Control Point Local Position", allPoints[0].followingControlPointLocalPosition );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < allPoints.Length; i++ )
				{
					Undo.RecordObject( allPoints[i], "Change Point Position" );
					allPoints[i].followingControlPointLocalPosition = followingControlPointLocalPosition;
				}

				SceneView.RepaintAll();
			}

			bool showControlPointDistanceWarning = false;
			for( int i = 0; i < allPoints.Length; i++ )
			{
				BezierPoint point = allPoints[i];
				if( ( point.position - point.precedingControlPointPosition ).sqrMagnitude < CONTROL_POINTS_MINIMUM_SAFE_DISTANCE_SQR ||
					( point.position - point.followingControlPointPosition ).sqrMagnitude < CONTROL_POINTS_MINIMUM_SAFE_DISTANCE_SQR )
				{
					showControlPointDistanceWarning = true;
					break;
				}
			}

			if( showControlPointDistanceWarning )
				EditorGUILayout.HelpBox( "Positions of control point(s) shouldn't be very close to (0,0,0), this might result in unpredictable behaviour while moving along the spline with constant speed.", MessageType.Warning );

			EditorGUILayout.Space();

			GUILayout.BeginHorizontal();
			if( GUILayout.Button( "Swap Control Points" ) )
			{
				SwapControlPoints( allPoints );
				SceneView.RepaintAll();
			}
			if( GUILayout.Button( APPLY_TO_ALL_POINTS, BezierUtils.GL_WIDTH_60 ) )
			{
				for( int i = 0; i < allSplines.Length; i++ )
					SwapControlPoints( allSplines[i].endPoints );

				SceneView.RepaintAll();
			}
			GUILayout.EndHorizontal();

			GUI.enabled = true;

			EditorGUILayout.Space();
			BezierUtils.DrawSeparator();

			GUI.enabled = !anySplineUsingAutoCalculateNormals;

			GUILayout.BeginHorizontal();
			EditorGUI.showMixedValue = HasMultipleDifferentValues( ( p1, p2 ) => p1.normal == p2.normal );
			EditorGUI.BeginChangeCheck();
			Rect normalRect = EditorGUILayout.GetControlRect( false, EditorGUIUtility.singleLineHeight ); // When using GUILayout, button isn't vertically centered
			normalRect.width -= 65f;
			Vector3 normal = EditorGUI.Vector3Field( normalRect, "Normal", allPoints[0].normal );
			normalRect.x += normalRect.width + 5f;
			normalRect.width = 30f;
			if( GUI.Button( normalRect, NORMALS_SET_TO_CAMERA_FORWARD ) )
				normal = SceneView.lastActiveSceneView.camera.transform.forward;
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < allPoints.Length; i++ )
					allPoints[i].SetNormalAndResetIntermediateNormals( normal, "Change Normal" );

				SceneView.RepaintAll();
			}

			normalRect.x += 30f;
			if( GUI.Button( normalRect, NORMALS_LOOK_AT_CAMERA ) )
			{
				Vector3 cameraPos = SceneView.lastActiveSceneView.camera.transform.position;
				for( int i = 0; i < allPoints.Length; i++ )
					allPoints[i].SetNormalAndResetIntermediateNormals( ( cameraPos - allPoints[i].position ).normalized, "Change Normal" );

				SceneView.RepaintAll();
			}
			GUILayout.EndHorizontal();

			if( !EditorGUIUtility.wideMode )
				GUILayout.Space( EditorGUIUtility.singleLineHeight );

			if( anySplineUsingAutoCalculateNormals )
			{
				GUI.enabled = true;

				EditorGUI.showMixedValue = HasMultipleDifferentValues( ( p1, p2 ) => p1.autoCalculatedNormalAngleOffset == p2.autoCalculatedNormalAngleOffset );
				EditorGUI.BeginChangeCheck();
				float autoCalculatedNormalAngleOffset = EditorGUILayout.FloatField( "Normal Angle", allPoints[0].autoCalculatedNormalAngleOffset );
				if( EditorGUI.EndChangeCheck() )
				{
					for( int i = 0; i < allPoints.Length; i++ )
					{
						Undo.RecordObject( allPoints[i], "Change Normal Angle" );
						allPoints[i].autoCalculatedNormalAngleOffset = autoCalculatedNormalAngleOffset;
					}

					for( int i = 0; i < allSplines.Length; i++ )
						BezierUtils.SetSplineDirtyWithUndo( allSplines[i], "Change Normal Angle", InternalDirtyFlags.NormalOffsetChange );

					SceneView.RepaintAll();
				}

				GUI.enabled = false;
			}
			else
			{
				EditorGUI.BeginChangeCheck();
				float normalRotationAngle = EditorGUILayout.FloatField( "Rotate Normal (Drag Here)", 0f );
				if( EditorGUI.EndChangeCheck() && !Mathf.Approximately( normalRotationAngle, 0f ) )
				{
					for( int i = 0; i < allPoints.Length; i++ )
					{
						BezierSpline spline = allPoints[i].spline;
						int index = allPoints[i].index;

						if( spline )
						{
							Vector3 tangent;
							if( index < spline.Count - 1 )
								tangent = new BezierSpline.Segment( spline[index], spline[index + 1], 0f ).GetTangent();
							else if( spline.loop )
								tangent = new BezierSpline.Segment( spline[index], spline[0], 0f ).GetTangent();
							else
								tangent = new BezierSpline.Segment( spline[index - 1], spline[index], 1f ).GetTangent();

							allPoints[i].SetNormalAndResetIntermediateNormals( Quaternion.AngleAxis( normalRotationAngle, tangent ) * allPoints[i].normal, "Change Normal Rotate Angle" );
						}
					}

					SceneView.RepaintAll();
				}
			}

			EditorGUILayout.Space();

			GUILayout.BeginHorizontal();
			if( GUILayout.Button( "Flip Normals" ) )
			{
				FlipNormals( allPoints );
				SceneView.RepaintAll();
			}
			if( GUILayout.Button( APPLY_TO_ALL_POINTS, BezierUtils.GL_WIDTH_60 ) )
			{
				for( int i = 0; i < allSplines.Length; i++ )
					FlipNormals( allSplines[i].endPoints );

				SceneView.RepaintAll();
			}
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if( GUILayout.Button( NORMALIZE_NORMALS_TIP ) )
			{
				NormalizeNormals( allPoints );
				SceneView.RepaintAll();
			}
			if( GUILayout.Button( APPLY_TO_ALL_POINTS, BezierUtils.GL_WIDTH_60 ) )
			{
				for( int i = 0; i < allSplines.Length; i++ )
					NormalizeNormals( allSplines[i].endPoints );

				SceneView.RepaintAll();
			}
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if( GUILayout.Button( "Reset Normals" ) )
			{
				ResetNormals( allPoints );
				SceneView.RepaintAll();
			}
			if( GUILayout.Button( APPLY_TO_ALL_POINTS, BezierUtils.GL_WIDTH_60 ) )
			{
				for( int i = 0; i < allSplines.Length; i++ )
					ResetNormals( allSplines[i].endPoints );

				SceneView.RepaintAll();
			}
			GUILayout.EndHorizontal();

			GUI.enabled = true;

			EditorGUILayout.Space();
			BezierUtils.DrawSeparator();

			GUILayout.BeginHorizontal();
			EditorGUI.showMixedValue = HasMultipleDifferentValues( ( p1, p2 ) => p1.extraData == p2.extraData );
			EditorGUI.BeginChangeCheck();
			Rect extraDataRect = EditorGUILayout.GetControlRect( false, EditorGUIUtility.singleLineHeight ); // When using GUILayout, button isn't vertically centered
			extraDataRect.width -= 65f;
			BezierPoint.ExtraData extraData = EditorGUI.Vector4Field( extraDataRect, EXTRA_DATA_TIP, allPoints[0].extraData );
			extraDataRect.x += extraDataRect.width + 5f;
			extraDataRect.width = 30f;
			if( GUI.Button( extraDataRect, EXTRA_DATA_SET_AS_CAMERA_FORWARD ) )
				extraData = SceneView.lastActiveSceneView.camera.transform.rotation;
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < allPoints.Length; i++ )
				{
					Undo.RecordObject( allPoints[i], "Change Extra Data" );
					allPoints[i].extraData = extraData;
				}

				SceneView.RepaintAll();
			}

			EditorGUI.showMixedValue = false;

			extraDataRect.x += 30f;
			EditorGUI.BeginChangeCheck();
			bool visualizeExtraDataAsFrustum = GUI.Toggle( extraDataRect, BezierSettings.VisualizeExtraDataAsFrustum, EXTRA_DATA_VIEW_AS_FRUSTUM, GUI.skin.button );
			if( EditorGUI.EndChangeCheck() )
			{
				BezierSettings.VisualizeExtraDataAsFrustum = visualizeExtraDataAsFrustum;
				SceneView.RepaintAll();
			}
			GUILayout.EndHorizontal();

			if( !EditorGUIUtility.wideMode )
				GUILayout.Space( EditorGUIUtility.singleLineHeight );

			BezierUtils.DrawSeparator();
			EditorGUILayout.Space();

			Color c = GUI.backgroundColor;
			GUI.backgroundColor = RESET_POINT_BUTTON_COLOR;

			if( GUILayout.Button( "Reset Point" ) )
			{
				for( int i = 0; i < allPoints.Length; i++ )
				{
					Undo.RecordObject( allPoints[i].transform, "Reset Point" );
					Undo.RecordObject( allPoints[i], "Reset Point" );

					allPoints[i].Reset();
				}

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();

			GUI.backgroundColor = REMOVE_POINT_BUTTON_COLOR;

			if( GUILayout.Button( "Remove Point" ) )
			{
				RemoveSelectedPoints();
				GUIUtility.ExitGUI();
			}

			GUI.backgroundColor = c;

			for( int i = 0; i < allSplines.Length; i++ )
				allSplines[i].CheckDirty();
		}

		private bool CheckCommands()
		{
			Event e = Event.current;
			if( e.type == EventType.ValidateCommand )
			{
				if( e.commandName == "Delete" )
				{
					RemoveSelectedPoints();
					e.type = EventType.Ignore;

					return true;
				}
				else if( e.commandName == "Duplicate" )
				{
					DuplicateSelectedPoints();
					e.type = EventType.Ignore;

					return true;
				}
			}

			if( e.isKey && e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete )
			{
				RemoveSelectedPoints();
				e.Use();

				return true;
			}

			return false;
		}

		private bool HasMultipleDifferentValues( System.Func<BezierPoint, BezierPoint, bool> comparer )
		{
			if( allPoints.Length <= 1 )
				return false;

			for( int i = 1; i < allPoints.Length; i++ )
			{
				if( !comparer( allPoints[0], allPoints[i] ) )
					return true;
			}

			return false;
		}

		private void InsertNewPoints( bool insertAfter, bool preserveShape )
		{
			Undo.IncrementCurrentGroup();

			Object[] newSelection = new Object[pointCount];
			for( int i = 0, index = 0; i < selection.Length; i++ )
			{
				BezierSpline spline = selection[i].spline;
				BezierPoint[] points = selection[i].points;

				for( int j = 0; j < points.Length; j++ )
				{
					BezierPoint newPoint;
					if( spline )
					{
						int pointIndex = points[j].index;
						if( insertAfter )
							pointIndex++;

						Vector3 position, followingControlPointPosition;
						if( spline.Count >= 2 )
						{
							if( !spline.loop && pointIndex == 0 )
							{
								position = spline[0].position - Vector3.Distance( spline[1].position, spline[0].position ) * spline.GetTangent( 0f ).normalized;
								followingControlPointPosition = position - ( position - spline[0].position ) * 0.5f;
							}
							else if( !spline.loop && pointIndex == spline.Count )
							{
								position = spline[pointIndex - 1].position + Vector3.Distance( spline[pointIndex - 1].position, spline[pointIndex - 2].position ) * spline.GetTangent( 1f ).normalized;
								followingControlPointPosition = position + ( position - spline[pointIndex - 1].position ) * 0.5f;
							}
							else
							{
								// Insert point in the middle without affecting the spline's shape
								BezierPoint point1 = ( pointIndex == 0 || pointIndex == spline.Count ) ? spline[spline.Count - 1] : spline[pointIndex - 1];
								BezierPoint point2 = ( pointIndex == 0 || pointIndex == spline.Count ) ? spline[0] : spline[pointIndex];

								Vector3 precedingControlPointPosition;
								CalculateInsertedPointPosition( point1, point2, 0.5f, preserveShape, out position, out precedingControlPointPosition, out followingControlPointPosition );
							}
						}
						else if( spline.Count == 1 )
						{
							position = pointIndex == 0 ? spline[0].position - Vector3.forward : spline[0].position + Vector3.forward;
							followingControlPointPosition = position + ( spline[0].followingControlPointPosition - spline[0].position );
						}
						else
						{
							position = spline.transform.position;
							followingControlPointPosition = Vector3.right;
						}

						newPoint = spline.InsertNewPointAt( pointIndex );
						newPoint.position = position;
						newPoint.followingControlPointPosition = followingControlPointPosition;
					}
					else
						newPoint = Instantiate( points[j], points[j].transform.parent );

					Undo.RegisterCreatedObjectUndo( newPoint.gameObject, "Insert Point" );
					if( newPoint.transform.parent )
						Undo.RegisterCompleteObjectUndo( newPoint.transform.parent, "Insert Point" );

					newSelection[index++] = newPoint.gameObject;
				}
			}

			Selection.objects = newSelection;
			SceneView.RepaintAll();
		}

		private void DuplicateSelectedPoints()
		{
			Undo.IncrementCurrentGroup();

			Object[] newSelection = new Object[pointCount];
			for( int i = 0, index = 0; i < selection.Length; i++ )
			{
				BezierSpline spline = selection[i].spline;
				BezierPoint[] points = selection[i].points;

				for( int j = 0; j < points.Length; j++ )
				{
					BezierPoint newPoint;
					if( spline )
						newPoint = spline.DuplicatePointAt( points[j].index );
					else
						newPoint = Instantiate( points[j], points[j].transform.parent );

					Undo.RegisterCreatedObjectUndo( newPoint.gameObject, "Duplicate Point" );
					if( newPoint.transform.parent )
						Undo.RegisterCompleteObjectUndo( newPoint.transform.parent, "Duplicate Point" );

					newSelection[index++] = newPoint.gameObject;
				}
			}

			Selection.objects = newSelection;
			SceneView.RepaintAll();
		}

		private void RemoveSelectedPoints()
		{
			Undo.IncrementCurrentGroup();

			Object[] newSelection = new Object[selection.Length];
			for( int i = 0; i < selection.Length; i++ )
			{
				BezierSpline spline = selection[i].spline;
				BezierPoint[] points = selection[i].points;

				for( int j = 0; j < points.Length; j++ )
					Undo.DestroyObjectImmediate( points[j].gameObject );

				if( spline )
					newSelection[i] = spline.gameObject;
			}

			Selection.objects = newSelection;
			SceneView.RepaintAll();
		}

		private void SwapControlPoints( IList<BezierPoint> points )
		{
			for( int i = 0; i < points.Count; i++ )
			{
				Undo.RecordObject( points[i], "Swap Control Points" );
				Vector3 temp = points[i].precedingControlPointLocalPosition;
				points[i].precedingControlPointLocalPosition = points[i].followingControlPointLocalPosition;
				points[i].followingControlPointLocalPosition = temp;
			}
		}

		// Credit: https://stackoverflow.com/a/2614028/2373034
		internal static void CalculateInsertedPointPosition( BezierPoint neighbor1, BezierPoint neighbor2, float localT, bool preserveShape, out Vector3 position, out Vector3 precedingControlPointPosition, out Vector3 followingControlPointPosition )
		{
			float oneMinusLocalT = 1f - localT;
			Vector3 P0_1 = oneMinusLocalT * neighbor1.position + localT * neighbor1.followingControlPointPosition;
			Vector3 P1_2 = oneMinusLocalT * neighbor1.followingControlPointPosition + localT * neighbor2.precedingControlPointPosition;
			Vector3 P2_3 = oneMinusLocalT * neighbor2.precedingControlPointPosition + localT * neighbor2.position;

			precedingControlPointPosition = oneMinusLocalT * P0_1 + localT * P1_2;
			followingControlPointPosition = oneMinusLocalT * P1_2 + localT * P2_3;

			position = oneMinusLocalT * precedingControlPointPosition + localT * followingControlPointPosition;

			// We need to change neighboring end points' handleModes if we want to truly preserve the spline's shape
			if( preserveShape )
			{
				Undo.RecordObject( neighbor1, "Insert Point" );
				Undo.RecordObject( neighbor2, "Insert Point" );

				if( neighbor1.handleMode == BezierPoint.HandleMode.Mirrored )
					neighbor1.handleMode = BezierPoint.HandleMode.Aligned;
				if( neighbor2.handleMode == BezierPoint.HandleMode.Mirrored )
					neighbor2.handleMode = BezierPoint.HandleMode.Aligned;

				neighbor1.followingControlPointPosition = P0_1;
				neighbor2.precedingControlPointPosition = P2_3;
			}
		}

		private void FlipNormals( IList<BezierPoint> points )
		{
			for( int i = 0; i < points.Count; i++ )
				points[i].SetNormalAndResetIntermediateNormals( -points[i].normal, "Flip Normals" );
		}

		private void NormalizeNormals( IList<BezierPoint> points )
		{
			for( int i = 0; i < points.Count; i++ )
			{
				if( points[i].normal != Vector3.zero )
					points[i].SetNormalAndResetIntermediateNormals( points[i].normal.normalized, "Normalize Normals" );
			}
		}

		private void ResetNormals( IList<BezierPoint> points )
		{
			for( int i = 0; i < points.Count; i++ )
				points[i].SetNormalAndResetIntermediateNormals( Vector3.up, "Reset Normals" );
		}

		private void OnUndoRedo()
		{
			controlPointRotationsInitialized = false;

			for( int i = 0; i < selection.Length; i++ )
			{
				if( selection[i].spline )
				{
					selection[i].spline.dirtyFlags |= InternalDirtyFlags.All;
					selection[i].spline.Refresh();
				}
			}

			Repaint();
		}

		private bool HasFrameBounds()
		{
			return !serializedObject.isEditingMultipleObjects;
		}

		private Bounds OnGetFrameBounds()
		{
			return new Bounds( ( (BezierPoint) target ).position, new Vector3( 1f, 1f, 1f ) );
		}
	}
}