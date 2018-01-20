using UnityEngine;
using UnityEditor;

namespace BezierSolution
{
	[CustomEditor( typeof( BezierPoint ) )]
	public class BezierPointEditor : Editor
	{
		private BezierSpline spline;
		private BezierPoint point;

		private Quaternion precedingPointRotation = Quaternion.identity;
		private Quaternion followingPointRotation = Quaternion.identity;
		private bool controlPointRotationsInitialized = false;

		private Color RESET_POINT_BUTTON_COLOR = new Color( 1f, 1f, 0.65f );
		private Color REMOVE_POINT_BUTTON_COLOR = new Color( 1f, 0.65f, 0.65f );

		void OnEnable()
		{
			point = target as BezierPoint;
			spline = point.GetComponentInParent<BezierSpline>();

			if( spline != null && !spline.Equals( null ) )
				spline.Refresh();

			Undo.undoRedoPerformed -= OnUndoRedo;
			Undo.undoRedoPerformed += OnUndoRedo;
		}

		void OnDisable()
		{
			Undo.undoRedoPerformed -= OnUndoRedo;
		}

		void OnSceneGUI()
		{
			if( spline != null && !spline.Equals( null ) )
			{
				Event e = Event.current;
				if( e.type == EventType.ValidateCommand )
				{
					if( e.commandName == "Delete" )
					{
						RemovePointAt( spline.IndexOf( point ) );
						e.type = EventType.Ignore;

						return;
					}
					else if( e.commandName == "Duplicate" )
					{
						DuplicatePointAt( spline.IndexOf( point ) );
						e.type = EventType.Ignore;
					}
				}

				if( e.isKey && e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete )
				{
					RemovePointAt( spline.IndexOf( point ) );
					e.Use();

					return;
				}

				BezierUtils.DrawSplineDetailed( spline );
				for( int i = 0; i < spline.Count; i++ )
				{
					BezierUtils.DrawBezierPoint( spline[i], i + 1, spline[i] == point );
				}
			}
			else
				BezierUtils.DrawBezierPoint( point, 0, true );

			// Draw translate handles for control points
			if( Event.current.alt )
				return;

			if( Tools.current != Tool.Move )
			{
				controlPointRotationsInitialized = false;
				return;
			}

			if( !controlPointRotationsInitialized )
			{
				precedingPointRotation = Quaternion.LookRotation( point.precedingControlPointPosition - point.position );
				followingPointRotation = Quaternion.LookRotation( point.followingControlPointPosition - point.position );

				controlPointRotationsInitialized = true;
			}

			EditorGUI.BeginChangeCheck();
			Vector3 position = Handles.PositionHandle( point.precedingControlPointPosition, Tools.pivotRotation == PivotRotation.Local ? precedingPointRotation : Quaternion.identity );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( point, "Move Control Point" );
				point.precedingControlPointPosition = position;
			}
			
			EditorGUI.BeginChangeCheck();
			position = Handles.PositionHandle( point.followingControlPointPosition, Tools.pivotRotation == PivotRotation.Local ? followingPointRotation : Quaternion.identity );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( point, "Move Control Point" );
				point.followingControlPointPosition = position;
			}
		}

		public override void OnInspectorGUI()
		{
			Color c = GUI.color;

			if( spline != null && !spline.Equals( null ) )
			{
				BezierUtils.DrawSplineInspectorGUI( spline );

				if( point == null || point.Equals( null ) )
					return;

				EditorGUILayout.Space();
				DrawSeparator();
				
				GUILayout.BeginHorizontal();

				if( GUILayout.Button( "<-", GUILayout.Width( 45 ) ) )
				{
					int prevIndex = spline.IndexOf( point ) - 1;
					if( prevIndex < 0 )
						prevIndex = spline.Count - 1;

					Selection.activeTransform = spline[prevIndex].transform;
					return;
				}

				GUILayout.Box( "Selected Point: " + ( spline.IndexOf( point ) + 1 ) + " / " + spline.Count, GUILayout.ExpandWidth( true ) );

				if( GUILayout.Button( "->", GUILayout.Width( 45 ) ) )
				{
					int nextIndex = spline.IndexOf( point ) + 1;
					if( nextIndex >= spline.Count )
						nextIndex = 0;

					Selection.activeTransform = spline[nextIndex].transform;
					return;
				}

				GUILayout.EndHorizontal();

				EditorGUILayout.Space();

				if( GUILayout.Button( "Decrement Point's Index" ) )
				{
					int index = spline.IndexOf( point );
					int newIndex = index - 1;
					if( newIndex < 0 )
						newIndex = spline.Count - 1;

					if( index != newIndex )
					{
						Undo.IncrementCurrentGroup();
						Undo.RegisterCompleteObjectUndo( point.transform.parent, "Change point index" );

						spline.SwapPointsAt( index, newIndex );
						SceneView.RepaintAll();
					}
				}

				if( GUILayout.Button( "Increment Point's Index" ) )
				{
					int index = spline.IndexOf( point );
					int newIndex = index + 1;
					if( newIndex >= spline.Count )
						newIndex = 0;

					if( index != newIndex )
					{
						Undo.IncrementCurrentGroup();
						Undo.RegisterCompleteObjectUndo( point.transform.parent, "Change point index" );

						spline.SwapPointsAt( index, newIndex );
						SceneView.RepaintAll();
					}
				}

				EditorGUILayout.Space();

				if( GUILayout.Button( "Insert Point Before" ) )
					InsertNewPointAt( spline.IndexOf( point ) );

				if( GUILayout.Button( "Insert Point After" ) )
					InsertNewPointAt( spline.IndexOf( point ) + 1 );

				EditorGUILayout.Space();

				if( GUILayout.Button( "Duplicate Point" ) )
					DuplicatePointAt( spline.IndexOf( point ) );

				EditorGUILayout.Space();
			}
			
			EditorGUI.BeginChangeCheck();
			BezierPoint.HandleMode handleMode = (BezierPoint.HandleMode) EditorGUILayout.EnumPopup( "Handle Mode", point.handleMode );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( point, "Change Point Handle Mode" );
				point.handleMode = handleMode;

				SceneView.RepaintAll();
			}
			
			EditorGUILayout.Space();

			EditorGUI.BeginChangeCheck();
			Vector3 position = EditorGUILayout.Vector3Field( "Preceding Control Point Local Position", point.precedingControlPointLocalPosition );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( point, "Change Point Position" );
				point.precedingControlPointLocalPosition = position;

				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			position = EditorGUILayout.Vector3Field( "Following Control Point Local Position", point.followingControlPointLocalPosition );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( point, "Change Point Position" );
				point.followingControlPointLocalPosition = position;

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();

			if( GUILayout.Button( "Swap Control Points" ) )
			{
				Undo.RecordObject( point, "Swap Control Points" );
				Vector3 precedingControlPointLocalPos = point.precedingControlPointLocalPosition;
				point.precedingControlPointLocalPosition = point.followingControlPointLocalPosition;
				point.followingControlPointLocalPosition = precedingControlPointLocalPos;

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();

			DrawSeparator();

			if( spline != null && !spline.Equals( null ) )
			{
				GUI.color = RESET_POINT_BUTTON_COLOR;

				if( GUILayout.Button( "Reset Point" ) )
				{
					ResetEndPointAt( spline.IndexOf( point ) );
					SceneView.RepaintAll();
				}

				EditorGUILayout.Space();

				GUI.color = REMOVE_POINT_BUTTON_COLOR;

				if( spline.Count <= 2 )
					GUI.enabled = false;

				if( GUILayout.Button( "Remove Point" ) )
					RemovePointAt( spline.IndexOf( point ) );

				GUI.enabled = true;
			}

			GUI.color = c;
		}

		private void DrawSeparator()
		{
			GUILayout.Box( string.Empty, GUILayout.Height( 2f ), GUILayout.ExpandWidth( true ) );
		}

		private void InsertNewPointAt( int index )
		{
			Vector3 position;

			if( spline.Count >= 2 )
			{
				if( index > 0 && index < spline.Count )
				{
					position = ( spline[index - 1].localPosition + spline[index].localPosition ) * 0.5f;
				}
				else if( index == 0 )
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
						position = spline[index - 1].localPosition + ( spline[index - 1].localPosition - spline[index - 2].localPosition ) * 0.5f;
				}
			}
			else if( spline.Count == 1 )
				position = index == 0 ? spline[0].localPosition - Vector3.forward : spline[0].localPosition + Vector3.forward;
			else
				position = Vector3.zero;
			
			BezierPoint point = spline.InsertNewPointAt( index );
			point.localPosition = position;

			Undo.IncrementCurrentGroup();
			Undo.RegisterCreatedObjectUndo( point.gameObject, "Insert Point" );
			Undo.RegisterCompleteObjectUndo( point.transform.parent, "Insert Point" );
			
			Selection.activeTransform = point.transform;
			SceneView.RepaintAll();
		}

		private void DuplicatePointAt( int index )
		{
			BezierPoint point = spline.DuplicatePointAt( index );

			Undo.IncrementCurrentGroup();
			Undo.RegisterCreatedObjectUndo( point.gameObject, "Duplicate Point" );
			Undo.RegisterCompleteObjectUndo( point.transform.parent, "Duplicate Point" );
			
			Selection.activeTransform = point.transform;
			SceneView.RepaintAll();
		}

		private void RemovePointAt( int index )
		{
			if( spline.Count <= 2 )
				return;

			Undo.IncrementCurrentGroup();
			Undo.DestroyObjectImmediate( spline[index].gameObject );
			
			if( index >= spline.Count )
				index--;

			Selection.activeTransform = spline[index].transform;
			
			SceneView.RepaintAll();
		}

		private void ResetEndPointAt( int index )
		{
			Undo.RecordObject( spline[index].transform, "Reset Point" );
			Undo.RecordObject( spline[index], "Reset Point" );

			spline[index].Reset();
		}

		private void OnUndoRedo()
		{
			controlPointRotationsInitialized = false;

			if( spline != null && !spline.Equals( null ) )
				spline.Refresh();

			Repaint();
		}

		private bool HasFrameBounds()
		{
			return true;
		}

		private Bounds OnGetFrameBounds()
		{
			return new Bounds( point.position, Vector3.one );
		}
	}
}