using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BezierSolution.Extras
{
	[CustomEditor( typeof( BezierPoint ) )]
	[CanEditMultipleObjects]
	public class BezierPointEditor : Editor
	{
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
				return x.Internal_Index.CompareTo( y.Internal_Index );
			}

			private int CompareBackwards( BezierPoint x, BezierPoint y )
			{
				return y.Internal_Index.CompareTo( x.Internal_Index );
			}
		}

		private const float CONTROL_POINTS_MINIMUM_SAFE_DISTANCE_SQR = 0.05f * 0.05f;

		private static readonly Color RESET_POINT_BUTTON_COLOR = new Color( 1f, 1f, 0.65f );
		private static readonly Color REMOVE_POINT_BUTTON_COLOR = new Color( 1f, 0.65f, 0.65f );
		private static readonly GUIContent MULTI_EDIT_TIP = new GUIContent( "Tip: Hold Shift to affect all points' Transforms" );
		private static readonly GUIContent EXTRA_DATA_SET_AS_CAMERA = new GUIContent( "C", "Set as Scene camera's current rotation" );
		private static readonly GUIContent EXTRA_DATA_VIEW_AS_FRUSTUM = new GUIContent( "V", "Visualize data as camera frustum in Scene" );

		private SplineHolder[] selection;
		private BezierSpline[] allSplines;
		private BezierPoint[] allPoints;
		private int pointCount;

		private Quaternion[] precedingPointRotations;
		private Quaternion[] followingPointRotations;
		private bool controlPointRotationsInitialized;

		// Having two variables allow us to show frustum gizmos only when a point is selected
		// and not lose the original value when OnDisable is called
		private static bool m_visualizeExtraDataAsFrustum;
		public static bool VisualizeExtraDataAsFrustum { get; private set; }

		private Tool previousTool = Tool.None;

		private void OnEnable()
		{
			Object[] points = targets;
			pointCount = points.Length;
			allPoints = new BezierPoint[pointCount];

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

			VisualizeExtraDataAsFrustum = m_visualizeExtraDataAsFrustum;
			Tools.hidden = true;

			Undo.undoRedoPerformed -= OnUndoRedo;
			Undo.undoRedoPerformed += OnUndoRedo;
		}

		private void OnDisable()
		{
			VisualizeExtraDataAsFrustum = false;

			Tools.hidden = false;
			Undo.undoRedoPerformed -= OnUndoRedo;
		}

		private void OnSceneGUI()
		{
			BezierPoint point = (BezierPoint) target;
			if( !point )
				return;

			if( CheckCommands() )
				return;

			// OnSceneGUI is called separately for each selected point,
			// make sure that the spline is drawn only once, not multiple times
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

				if( allPoints.Length > 1 )
				{
					Handles.BeginGUI();
					GUIStyle style = "PreOverlayLabel"; // Taken from: https://github.com/Unity-Technologies/UnityCsReference/blob/f78f4093c8a2b45949a847cdc704cf209dcf2f36/Editor/Mono/EditorGUI.cs#L629
					EditorGUI.DropShadowLabel( new Rect( new Vector2( 0f, 0f ), style.CalcSize( MULTI_EDIT_TIP ) ), MULTI_EDIT_TIP, style );
					Handles.EndGUI();
				}
			}

			// When Control key is pressed, BezierPoint gizmos should be drawn on top of Transform handles in order to allow selecting/deselecting points
			// If Alt key is pressed, Transform handles aren't drawn at all, so BezierPoint gizmos can be drawn immediately
			Event e = Event.current;
			if( e.alt || !e.control )
				BezierUtils.DrawBezierPoint( point, point.Internal_Index + 1, true );

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

			Tool tool = Tools.current;
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
					if( !point.Internal_Spline || point.Internal_Spline.Internal_AutoConstructMode == SplineAutoConstructMode.None )
					{
						EditorGUI.BeginChangeCheck();
						position = Handles.PositionHandle( point.precedingControlPointPosition, Tools.pivotRotation == PivotRotation.Local ? precedingPointRotations[pointIndex] : Quaternion.identity );
						if( EditorGUI.EndChangeCheck() )
						{
							Undo.RecordObject( point, "Move Control Point" );
							point.precedingControlPointPosition = position;
						}

						EditorGUI.BeginChangeCheck();
						position = Handles.PositionHandle( point.followingControlPointPosition, Tools.pivotRotation == PivotRotation.Local ? followingPointRotations[pointIndex] : Quaternion.identity );
						if( EditorGUI.EndChangeCheck() )
						{
							Undo.RecordObject( point, "Move Control Point" );
							point.followingControlPointPosition = position;
						}
					}

					EditorGUI.BeginChangeCheck();
					position = Handles.PositionHandle( point.position, Tools.pivotRotation == PivotRotation.Local ? point.rotation : Quaternion.identity );
					if( EditorGUI.EndChangeCheck() )
					{
						if( !e.shift )
						{
							Undo.RecordObject( point, "Move Point" );
							Undo.RecordObject( point.transform, "Move Point" );

							point.position = position;
						}
						else
						{
							Vector3 delta = position - point.position;

							for( int i = 0; i < allPoints.Length; i++ )
							{
								Undo.RecordObject( allPoints[i], "Move Point" );
								Undo.RecordObject( allPoints[i].transform, "Move Point" );

								allPoints[i].position += delta;
							}
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
						float angle;
						Vector3 axis;
						( Quaternion.Inverse( handleRotation ) * rotation ).ToAngleAxis( out angle, out axis );
						axis = handleRotation * axis;

						if( !e.shift )
						{
							Undo.RecordObject( point.transform, "Rotate Point" );

							Vector3 localAxis = point.transform.InverseTransformDirection( axis );
							point.localRotation *= Quaternion.AngleAxis( angle, localAxis );
						}
						else
						{
							for( int i = 0; i < allPoints.Length; i++ )
							{
								Undo.RecordObject( allPoints[i].transform, "Rotate Point" );

								Vector3 localAxis = allPoints[i].transform.InverseTransformDirection( axis );
								allPoints[i].localRotation *= Quaternion.AngleAxis( angle, localAxis );
							}
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
						if( !e.shift )
						{
							Undo.RecordObject( point.transform, "Scale Point" );
							point.localScale = scale;
						}
						else
						{
							Vector3 delta = new Vector3( 1f, 1f, 1f );
							Vector3 prevScale = point.localScale;
							if( prevScale.x != 0f )
								delta.x = scale.x / prevScale.x;
							if( prevScale.y != 0f )
								delta.y = scale.y / prevScale.y;
							if( prevScale.z != 0f )
								delta.z = scale.z / prevScale.z;

							for( int i = 0; i < allPoints.Length; i++ )
							{
								Undo.RecordObject( allPoints[i].transform, "Scale Point" );

								prevScale = allPoints[i].localScale;
								prevScale.Scale( delta );
								allPoints[i].localScale = prevScale;
							}
						}
					}

					break;
			}

			if( e.control )
				BezierUtils.DrawBezierPoint( point, point.Internal_Index + 1, true );
		}

		public override void OnInspectorGUI()
		{
			if( CheckCommands() )
				GUIUtility.ExitGUI();

			BezierUtils.DrawSplineInspectorGUI( allSplines );

			EditorGUILayout.Space();
			BezierUtils.DrawSeparator();

			GUILayout.BeginHorizontal();

			if( GUILayout.Button( "<-", GUILayout.Width( 45 ) ) )
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
							int prevIndex = points[j].Internal_Index - 1;
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

			string pointIndex = ( pointCount == 1 && selection[0].spline ) ? ( allPoints[0].Internal_Index + 1 ).ToString() : "-";
			string splineLength = ( selection.Length == 1 && selection[0].spline ) ? selection[0].spline.Count.ToString() : "-";
			GUILayout.Box( "Selected Point: " + pointIndex + " / " + splineLength, GUILayout.ExpandWidth( true ) );

			if( GUILayout.Button( "->", GUILayout.Width( 45 ) ) )
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
							int nextIndex = points[j].Internal_Index + 1;
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
							int index = points[j].Internal_Index;
							int newIndex = index - 1;
							if( newIndex < 0 )
								newIndex = spline.Count - 1;

							newIndices[j] = newIndex;
						}

						for( int j = 0; j < points.Length; j++ )
						{
							Undo.RegisterCompleteObjectUndo( points[j].transform.parent, "Change point index" );
							spline.Internal_MovePoint( points[j].Internal_Index, newIndices[j], "Change point index" );
						}

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
							int index = points[j].Internal_Index;
							int newIndex = index + 1;
							if( newIndex >= spline.Count )
								newIndex = 0;

							newIndices[j] = newIndex;
						}

						for( int j = 0; j < points.Length; j++ )
						{
							Undo.RegisterCompleteObjectUndo( points[j].transform.parent, "Change point index" );
							spline.Internal_MovePoint( points[j].Internal_Index, newIndices[j], "Change point index" );
						}

						selection[i].SortPoints( true );
					}
				}

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();

			if( GUILayout.Button( "Insert Point Before" ) )
				InsertNewPoints( false );

			if( GUILayout.Button( "Insert Point After" ) )
				InsertNewPoints( true );

			EditorGUILayout.Space();

			if( GUILayout.Button( "Duplicate Point" ) )
				DuplicateSelectedPoints();

			EditorGUILayout.Space();

			bool hasMultipleDifferentValues = false;
			BezierPoint.HandleMode handleMode = allPoints[0].handleMode;
			for( int i = 1; i < allPoints.Length; i++ )
			{
				if( allPoints[i].handleMode != handleMode )
				{
					hasMultipleDifferentValues = true;
					break;
				}
			}

			EditorGUI.showMixedValue = hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			handleMode = (BezierPoint.HandleMode) EditorGUILayout.EnumPopup( "Handle Mode", handleMode );
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

			hasMultipleDifferentValues = false;
			Vector3 position = allPoints[0].precedingControlPointLocalPosition;
			for( int i = 1; i < allPoints.Length; i++ )
			{
				if( allPoints[i].precedingControlPointLocalPosition != position )
				{
					hasMultipleDifferentValues = true;
					break;
				}
			}

			EditorGUI.showMixedValue = hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			position = EditorGUILayout.Vector3Field( "Preceding Control Point Local Position", position );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < allPoints.Length; i++ )
				{
					Undo.RecordObject( allPoints[i], "Change Point Position" );
					allPoints[i].precedingControlPointLocalPosition = position;
				}

				SceneView.RepaintAll();
			}

			hasMultipleDifferentValues = false;
			position = allPoints[0].followingControlPointLocalPosition;
			for( int i = 1; i < allPoints.Length; i++ )
			{
				if( allPoints[i].followingControlPointLocalPosition != position )
				{
					hasMultipleDifferentValues = true;
					break;
				}
			}

			EditorGUI.showMixedValue = hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			position = EditorGUILayout.Vector3Field( "Following Control Point Local Position", position );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < allPoints.Length; i++ )
				{
					Undo.RecordObject( allPoints[i], "Change Point Position" );
					allPoints[i].followingControlPointLocalPosition = position;
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

			if( GUILayout.Button( "Swap Control Points" ) )
			{
				for( int i = 0; i < allPoints.Length; i++ )
				{
					Undo.RecordObject( allPoints[i], "Swap Control Points" );
					Vector3 temp = allPoints[i].precedingControlPointLocalPosition;
					allPoints[i].precedingControlPointLocalPosition = allPoints[i].followingControlPointLocalPosition;
					allPoints[i].followingControlPointLocalPosition = temp;
				}

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();
			BezierUtils.DrawSeparator();

			hasMultipleDifferentValues = false;
			BezierPoint.ExtraData extraData = allPoints[0].extraData;
			for( int i = 1; i < allPoints.Length; i++ )
			{
				if( allPoints[i].extraData != extraData )
				{
					hasMultipleDifferentValues = true;
					break;
				}
			}

			GUILayout.BeginHorizontal();
			EditorGUI.showMixedValue = hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			Rect extraDataRect = EditorGUILayout.GetControlRect( false, EditorGUIUtility.singleLineHeight ); // When using GUILayout, button isn't vertically centered
			extraDataRect.width -= 65f;
			extraData = EditorGUI.Vector4Field( extraDataRect, "Extra Data", extraData );
			extraDataRect.x += extraDataRect.width + 5f;
			extraDataRect.width = 30f;
			if( GUI.Button( extraDataRect, EXTRA_DATA_SET_AS_CAMERA ) )
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
			m_visualizeExtraDataAsFrustum = GUI.Toggle( extraDataRect, m_visualizeExtraDataAsFrustum, EXTRA_DATA_VIEW_AS_FRUSTUM, GUI.skin.button );
			if( EditorGUI.EndChangeCheck() )
			{
				VisualizeExtraDataAsFrustum = m_visualizeExtraDataAsFrustum;
				SceneView.RepaintAll();
			}

			GUILayout.EndHorizontal();

			BezierUtils.DrawSeparator();
			EditorGUILayout.Space();

			Color c = GUI.color;
			GUI.color = RESET_POINT_BUTTON_COLOR;

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

			GUI.color = REMOVE_POINT_BUTTON_COLOR;

			if( GUILayout.Button( "Remove Point" ) )
			{
				RemoveSelectedPoints();
				GUIUtility.ExitGUI();
			}

			GUI.color = c;

			for( int i = 0; i < allSplines.Length; i++ )
				allSplines[i].Internal_CheckDirty();
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

		private void InsertNewPoints( bool insertAfter )
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
						int pointIndex = points[j].Internal_Index;
						if( insertAfter )
							pointIndex++;

						Vector3 position;
						if( spline.Count >= 2 )
						{
							if( pointIndex > 0 && pointIndex < spline.Count )
								position = ( spline[pointIndex - 1].localPosition + spline[pointIndex].localPosition ) * 0.5f;
							else if( pointIndex == 0 )
							{
								if( spline.loop )
									position = ( spline[0].localPosition + spline[spline.Count - 1].localPosition ) * 0.5f;
								else
									position = spline[0].localPosition - ( spline[1].localPosition - spline[0].localPosition ) * 0.5f;
							}
							else
							{
								if( spline.loop )
									position = ( spline[0].localPosition + spline[spline.Count - 1].localPosition ) * 0.5f;
								else
									position = spline[pointIndex - 1].localPosition + ( spline[pointIndex - 1].localPosition - spline[pointIndex - 2].localPosition ) * 0.5f;
							}
						}
						else if( spline.Count == 1 )
							position = pointIndex == 0 ? spline[0].localPosition - Vector3.forward : spline[0].localPosition + Vector3.forward;
						else
							position = Vector3.zero;

						newPoint = spline.InsertNewPointAt( pointIndex );
						newPoint.localPosition = position;
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
						newPoint = spline.DuplicatePointAt( points[j].Internal_Index );
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

		private void OnUndoRedo()
		{
			controlPointRotationsInitialized = false;

			for( int i = 0; i < selection.Length; i++ )
			{
				if( selection[i].spline )
					selection[i].spline.Refresh();
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