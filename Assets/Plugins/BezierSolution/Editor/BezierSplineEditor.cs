using UnityEngine;
using UnityEditor;

[CustomEditor( typeof( BezierSpline ) )]
public class BezierSplineEditor : Editor
{
	private BezierSpline spline;
	
	private EndPointIndex selectedEndPointIndex
	{
		get
		{
			return spline.selectedEndPointIndex;
		}
		set
		{
			spline.selectedEndPointIndex = value;
		}
	}
	
	private Color SPLINE_COLOR = new Color( 0.8f, 0.6f, 0.8f );

	private Color SELECTED_END_POINT_COLOR = Color.yellow;
	private Color SELECTED_END_POINT_CONNECTED_POINTS_COLOR = Color.green;

	private Color AUTO_CONSTRUCT_SPLINE_BUTTON_COLOR = new Color( 0.65f, 1f, 0.65f );
	private Color RESET_POINT_BUTTON_COLOR = new Color( 1f, 1f, 0.65f );
	private Color REMOVE_POINT_BUTTON_COLOR = new Color( 1f, 0.65f, 0.65f );

	private const float SPLINE_THICKNESS = 8f;
	private const float END_POINT_SIZE = 0.075f;
	private const float END_POINT_CONTROL_POINTS_SIZE = 0.1f;
	private const float CONTROL_POINT_SIZE = 0.04f;

	private const string PRECEDING_CONTROL_POINT_LABEL = "<-";
	private const string FOLLOWING_CONTROL_POINT_LABEL = "->";
	
	private Quaternion worldSpaceRotation = Quaternion.identity;
	private bool worldSpaceRotationInitialized = false;

	private Quaternion endPointControlPointRotation = Quaternion.identity;
	private bool endPointControlPointRotationInitialized = false;
	
	void OnEnable()
	{
		spline = target as BezierSpline;

		/*if( selectedEndPointIndex >= spline.Count )
			selectedEndPointIndex = spline.Count - 1;*/

		Undo.undoRedoPerformed -= OnUndo;
		Undo.undoRedoPerformed += OnUndo;
	}

	void OnDisable()
	{
		Tools.hidden = false;
		
		Undo.undoRedoPerformed -= OnUndo;
	}

	void OnSceneGUI()
	{
		BezierPoint endPoint0 = null, endPoint1 = null;
		for( int i = 0; i < spline.Count - 1; i++ )
		{
			endPoint0 = spline[i];
			endPoint1 = spline[i + 1];

			DrawBezier( endPoint0, endPoint1 );
		}

		/*
		// Draw tangent lines on scene view
		Color _tmp = Handles.color;
		Handles.color = Color.cyan;
		for( float i = 0f; i < 1f; i += 0.05f )
		{
			Handles.DrawLine( spline.GetPoint( i ), spline.GetPoint( i ) + spline.GetTangent( i ) );
		}
		Handles.color = _tmp;
		*/

		if( endPoint1 != null )
		{
			if( spline.loop )
				DrawBezier( endPoint1, spline[0] );
		}

		for( int i = 0; i < spline.Count; i++ )
		{
			DrawBezierPointAt( i );
		}

		if( selectedEndPointIndex >= spline.Count )
			selectedEndPointIndex = spline.Count - 1;

		if( selectedEndPointIndex == -1 )
		{
			Tools.hidden = false;
		}
		else
		{
			Tools.hidden = true;
			DrawHandlesForSelectedEndPoint();

			Event e = Event.current;
			if( e.type == EventType.ValidateCommand )
			{
				if( e.commandName == "Copy" || e.commandName == "Cut" || e.commandName == "Paste" )
				{
					e.type = EventType.Ignore;
				}
				else if( e.commandName == "Delete" )
				{
					RemovePointAt( selectedEndPointIndex );
					e.type = EventType.Ignore;
				}
				else if( e.commandName == "Duplicate" )
				{
					DuplicatePointAt( selectedEndPointIndex );
					e.type = EventType.Ignore;
				}
			}

			if( e.isKey && e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete )
			{
				RemovePointAt( selectedEndPointIndex );
				e.Use();
			}
		}
	}

	public override void OnInspectorGUI()
	{
		Color c = GUI.color;

		EditorGUI.BeginChangeCheck();
		bool loop = EditorGUILayout.Toggle( "Loop", spline.loop );
		if( EditorGUI.EndChangeCheck() )
		{
			Undo.RecordObject( spline, "Toggle Loop" );
			spline.loop = loop;

			SceneView.RepaintAll();
		}

		EditorGUILayout.Space();

		GUI.color = AUTO_CONSTRUCT_SPLINE_BUTTON_COLOR;

		if( GUILayout.Button( "Auto Construct Spline" ) )
		{
			Undo.RecordObject( spline, "Auto Construct Spline" );
			spline.AutoConstructSpline();

			SceneView.RepaintAll();
		}

		if( GUILayout.Button( "Auto Construct Spline (method #2)" ) )
		{
			Undo.RecordObject( spline, "Auto Construct Spline" );
			spline.AutoConstructSpline2();

			SceneView.RepaintAll();
		}

		GUI.color = c;

		if( selectedEndPointIndex >= spline.Count )
			selectedEndPointIndex = spline.Count - 1;

		if( selectedEndPointIndex != -1 )
		{
			EditorGUILayout.Space();

			DrawHorizontalSeparator();

			GUILayout.BeginHorizontal();
			if( GUILayout.Button( "<-", GUILayout.Width( 45 ) ) )
			{
				int newIndex = selectedEndPointIndex - 1;
				if( newIndex < 0 )
				{
					if( loop )
						newIndex = spline.Count - 1;
					else
						newIndex = 0;
				}

				if( newIndex != selectedEndPointIndex )
				{
					Undo.IncrementCurrentGroup();
					Undo.RecordObject( spline, "Change Selected Point" );
					selectedEndPointIndex = newIndex;

					SceneView.RepaintAll();
				}
			}
			GUILayout.Box( "Selected Point: " + selectedEndPointIndex.index + " / " + ( spline.Count - 1 ), GUILayout.ExpandWidth( true ) );
			if( GUILayout.Button( "->", GUILayout.Width( 45 ) ) )
			{
				int newIndex = selectedEndPointIndex + 1;
				if( newIndex >= spline.Count )
				{
					if( loop )
						newIndex = 0;
					else
						newIndex = spline.Count - 1;
				}

				if( newIndex != selectedEndPointIndex )
				{
					Undo.IncrementCurrentGroup();
					Undo.RecordObject( spline, "Change Selected Point" );
					selectedEndPointIndex = newIndex;

					SceneView.RepaintAll();
				}
			}
			GUILayout.EndHorizontal();

			if( GUILayout.Button( "Deselect" ) )
			{
				Undo.RecordObject( spline, "Deselect Selected Point" );
				selectedEndPointIndex = -1;

				SceneView.RepaintAll();

				return;
			}

			EditorGUILayout.Space();

			if( GUILayout.Button( "Decrement Point's Index" ) )
			{
				if( selectedEndPointIndex > 0 )
				{
					Undo.IncrementCurrentGroup();
					Undo.RecordObject( spline, "Change point index" );
					spline.SwapPointsAt( selectedEndPointIndex, selectedEndPointIndex - 1 );
					selectedEndPointIndex = selectedEndPointIndex - 1;

					SceneView.RepaintAll();
				}
			}

			if( GUILayout.Button( "Increment Point's Index" ) )
			{
				if( selectedEndPointIndex < spline.Count - 1 )
				{
					Undo.IncrementCurrentGroup();
					Undo.RecordObject( spline, "Change point index" );
					spline.SwapPointsAt( selectedEndPointIndex, selectedEndPointIndex + 1 );
					selectedEndPointIndex = selectedEndPointIndex + 1;

					SceneView.RepaintAll();
				}
			}

			EditorGUILayout.Space();

			if( GUILayout.Button( "Insert Point Before" ) )
			{
				InsertNewPointAt( selectedEndPointIndex );
			}

			if( GUILayout.Button( "Insert Point After" ) )
			{
				InsertNewPointAt( selectedEndPointIndex + 1 );
			}

			EditorGUILayout.Space();

			if( GUILayout.Button( "Duplicate Point" ) )
			{
				DuplicatePointAt( selectedEndPointIndex );
			}

			EditorGUILayout.Space();

			BezierPoint selectedEndPoint = spline[selectedEndPointIndex];

			EditorGUI.BeginChangeCheck();
			BezierPoint.HandleMode handleMode = (BezierPoint.HandleMode) EditorGUILayout.EnumPopup( "Handle Mode", selectedEndPoint.handleMode );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( spline, "Change Point Handle Mode" );
				selectedEndPoint.handleMode = handleMode;

				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			Vector3 position = EditorGUILayout.Vector3Field( "Local Position", selectedEndPoint.localPosition );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( spline, "Change Point Position" );
				selectedEndPoint.localPosition = position;

				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			Vector3 eulerAngles = EditorGUILayout.Vector3Field( "Local Euler Angles", selectedEndPoint.localEulerAngles );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( spline, "Change Point Rotation" );
				selectedEndPoint.localEulerAngles = eulerAngles;

				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			Vector3 scale = EditorGUILayout.Vector3Field( "Local Scale", selectedEndPoint.localScale );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( spline, "Change Point Scale" );
				selectedEndPoint.localScale = scale;

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();

			EditorGUI.BeginChangeCheck();
			position = EditorGUILayout.Vector3Field( "Preceding Control Point Local Position", selectedEndPoint.precedingControlPointLocalPosition );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( spline, "Change Point Position" );
				selectedEndPoint.precedingControlPointLocalPosition = position;

				SceneView.RepaintAll();
			}

			EditorGUI.BeginChangeCheck();
			position = EditorGUILayout.Vector3Field( "Following Control Point Local Position", selectedEndPoint.followingControlPointLocalPosition );
			if( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( spline, "Change Point Position" );
				selectedEndPoint.followingControlPointLocalPosition = position;

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();

			if( GUILayout.Button( "Swap Control Points" ) )
			{
				Undo.RecordObject( spline, "Swap Control Points" );
				Vector3 precedingControlPointLocalPos = selectedEndPoint.precedingControlPointLocalPosition;
				selectedEndPoint.precedingControlPointLocalPosition = selectedEndPoint.followingControlPointLocalPosition;
				selectedEndPoint.followingControlPointLocalPosition = precedingControlPointLocalPos;

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();

			DrawHorizontalSeparator();
			
			GUI.color = RESET_POINT_BUTTON_COLOR;

			if( GUILayout.Button( "Reset Point" ) )
			{
				ResetEndPointAt( selectedEndPointIndex );

				SceneView.RepaintAll();
			}

			EditorGUILayout.Space();

			GUI.color = REMOVE_POINT_BUTTON_COLOR;

			if( spline.Count <= 2 )
				GUI.enabled = false;

			if( GUILayout.Button( "Remove Point" ) )
			{
				RemovePointAt( selectedEndPointIndex );
			}

			GUI.enabled = true;
			GUI.color = c;
		}
	}

	private void DrawHorizontalSeparator()
	{
		GUILayout.Box( string.Empty, GUILayout.Height( 2f ), GUILayout.ExpandWidth( true ) );
	}

	private void DrawBezier( BezierPoint endPoint0, BezierPoint endPoint1 )
	{
		Handles.DrawBezier( endPoint0.position, endPoint1.position,
							endPoint0.followingControlPointPosition,
							endPoint1.precedingControlPointPosition,
							SPLINE_COLOR, null, SPLINE_THICKNESS );
	}

	private void DrawBezierPointAt( int index )
	{
		BezierPoint endPoint = spline[index];

		Color c = Handles.color;

		DrawBezierPointAt( index, EndPointIndex.CONTROL_POINT_NONE, Handles.DotCap );

		Handles.color = c;

		Handles.DrawLine( endPoint.position, endPoint.precedingControlPointPosition );
		Handles.DrawLine( endPoint.position, endPoint.followingControlPointPosition );
		
		DrawBezierPointAt( index, EndPointIndex.CONTROL_POINT_PRECEDING, Handles.RectangleCap );
		DrawBezierPointAt( index, EndPointIndex.CONTROL_POINT_FOLLOWING, Handles.RectangleCap );

		Handles.color = c;

		Handles.Label( endPoint.position, "Point" + index );
		Handles.Label( endPoint.precedingControlPointPosition, PRECEDING_CONTROL_POINT_LABEL );
		Handles.Label( endPoint.followingControlPointPosition, FOLLOWING_CONTROL_POINT_LABEL );
	}

	private void DrawBezierPointAt( int index, int controlPointIndex, Handles.DrawCapFunction capFunc )
	{
		Vector3 worldPos;
		Quaternion rotation;
		float sizeModifier;
		if( controlPointIndex == EndPointIndex.CONTROL_POINT_NONE )
		{
			worldPos = spline[index].position;
			rotation = Quaternion.identity;
			sizeModifier = END_POINT_SIZE;
		}
		else if( controlPointIndex == EndPointIndex.CONTROL_POINT_PRECEDING )
		{
			worldPos = spline[index].precedingControlPointPosition;
			rotation = SceneView.lastActiveSceneView.rotation;
			sizeModifier = END_POINT_CONTROL_POINTS_SIZE;
		}
		else
		{
			worldPos = spline[index].followingControlPointPosition;
			rotation = SceneView.lastActiveSceneView.rotation;
			sizeModifier = END_POINT_CONTROL_POINTS_SIZE;
		}

		if( index != selectedEndPointIndex || selectedEndPointIndex.controlPointIndex != controlPointIndex )
		{
			if( index != selectedEndPointIndex )
				Handles.color = Color.white;
			else
				Handles.color = SELECTED_END_POINT_CONNECTED_POINTS_COLOR;

			if( Event.current.alt || Event.current.button > 0 )
			{
				capFunc( 0, worldPos, rotation, HandleUtility.GetHandleSize( worldPos ) * sizeModifier );
			}
			else
			{
				if( Handles.Button( worldPos, rotation, HandleUtility.GetHandleSize( worldPos ) * sizeModifier, sizeModifier, capFunc ) )
				{
					Undo.RecordObject( spline, "Change Selected Point" );
					selectedEndPointIndex = new EndPointIndex( index, controlPointIndex );

					worldSpaceRotationInitialized = false;
					endPointControlPointRotationInitialized = false;

					Repaint();
				}
			}
		}
		else
		{
			Handles.color = SELECTED_END_POINT_COLOR;
			capFunc( 0, worldPos, rotation, HandleUtility.GetHandleSize( worldPos ) * sizeModifier * 1.5f );
		}
	}

	private void DrawHandlesForSelectedEndPoint()
	{
		if( Event.current.alt )
			return;

		if( Tools.current == Tool.Move )
		{
			worldSpaceRotationInitialized = false;

			if( Tools.pivotRotation == PivotRotation.Global )
				endPointControlPointRotationInitialized = false;
		}
		else if( Tools.current == Tool.Rotate )
		{
			endPointControlPointRotationInitialized = false;

			if( Tools.pivotRotation == PivotRotation.Local )
				worldSpaceRotationInitialized = false;
		}
		else
		{
			worldSpaceRotationInitialized = false;
			endPointControlPointRotationInitialized = false;
		}

		BezierPoint selectedEndPoint = spline[selectedEndPointIndex];

		Vector3 handlePosition = FindSelectedPointPosition();
		Quaternion handleRotation;

		if( Tools.current == Tool.Rotate )
		{
			if( selectedEndPointIndex.controlPointIndex != EndPointIndex.CONTROL_POINT_NONE )
				return;

			if( Tools.pivotRotation == PivotRotation.Local )
			{
				handleRotation = selectedEndPoint.rotation;
			}
			else
			{
				if( !worldSpaceRotationInitialized )
				{
					worldSpaceRotation = Quaternion.identity;
					worldSpaceRotationInitialized = true;
				}

				handleRotation = worldSpaceRotation;
			}

			EditorGUI.BeginChangeCheck();
			Quaternion rotation = Handles.RotationHandle( handleRotation, handlePosition );
			if( EditorGUI.EndChangeCheck() )
			{
				// Delta rotation code fetched from Unity Decompiled: 
				// https://github.com/MattRix/UnityDecompiled/blob/master/UnityEditor/UnityEditor/RotateTool.cs
				float angle;
				Vector3 axis;
				( Quaternion.Inverse( handleRotation ) * rotation ).ToAngleAxis( out angle, out axis );

				if( Tools.pivotRotation == PivotRotation.Global )
				{
					axis = selectedEndPoint.worldToLocalMatrix.MultiplyVector( handleRotation * axis );
				}
				
				Undo.RecordObject( spline, "Rotate Point" );
				selectedEndPoint.localRotation *= Quaternion.AngleAxis( angle, axis );

				if( Tools.pivotRotation == PivotRotation.Global )
					worldSpaceRotation = rotation;
			}
		}
		else
		{
			if( Tools.current == Tool.Move )
			{
				if( Tools.pivotRotation == PivotRotation.Local )
				{
					if( selectedEndPointIndex.controlPointIndex == EndPointIndex.CONTROL_POINT_NONE )
					{
						handleRotation = selectedEndPoint.rotation;
					}
					else
					{
						if( !endPointControlPointRotationInitialized )
						{
							endPointControlPointRotation = Quaternion.LookRotation( handlePosition - spline[selectedEndPointIndex].position );
							endPointControlPointRotationInitialized = true;
						}

						handleRotation = endPointControlPointRotation;
					}
				}
				else
				{
					handleRotation = Quaternion.identity;
				}

				EditorGUI.BeginChangeCheck();
				Vector3 position = Handles.PositionHandle( handlePosition, handleRotation );
				if( EditorGUI.EndChangeCheck() )
				{
					Undo.RecordObject( spline, "Move Point" );

					if( selectedEndPointIndex.controlPointIndex == EndPointIndex.CONTROL_POINT_NONE )
						selectedEndPoint.position = position;
					else if( selectedEndPointIndex.controlPointIndex == EndPointIndex.CONTROL_POINT_PRECEDING )
						selectedEndPoint.precedingControlPointPosition = position;
					else
						selectedEndPoint.followingControlPointPosition = position;
				}
			}
			else if( Tools.current == Tool.Scale )
			{
				if( selectedEndPointIndex.controlPointIndex != EndPointIndex.CONTROL_POINT_NONE )
					return;

				handleRotation = selectedEndPoint.rotation;
				float sizeModifier = HandleUtility.GetHandleSize( handlePosition );

				EditorGUI.BeginChangeCheck();
				Vector3 scale = Handles.ScaleHandle( selectedEndPoint.localScale, handlePosition, handleRotation, sizeModifier );
				if( EditorGUI.EndChangeCheck() )
				{
					Undo.RecordObject( spline, "Scale Point" );
					selectedEndPoint.localScale = scale;
				}
			}
		}
	}

	private void InsertNewPointAt( int index )
	{
		Undo.IncrementCurrentGroup();
		Undo.RecordObject( spline, "Insert Point" );
		spline.InsertNewPointAt( index );

		selectedEndPointIndex = index;

		SceneView.RepaintAll();
	}

	private void DuplicatePointAt( int index )
	{
		BezierPoint duplicatePoint = new BezierPoint( spline, spline[selectedEndPointIndex] );

		Undo.IncrementCurrentGroup();
		Undo.RecordObject( spline, "Duplicate Point" );
		spline.InsertNewPointAt( index + 1, duplicatePoint );

		selectedEndPointIndex = index + 1;

		SceneView.RepaintAll();
	}

	private void RemovePointAt( int index )
	{
		Undo.IncrementCurrentGroup();
		Undo.RecordObject( spline, "Remove Point" );
		spline.RemovePointAt( index );

		if( selectedEndPointIndex >= spline.Count )
			selectedEndPointIndex = spline.Count - 1;

		SceneView.RepaintAll();
	}

	private Vector3 FindSelectedPointPosition()
	{
		if( selectedEndPointIndex.controlPointIndex == EndPointIndex.CONTROL_POINT_NONE )
			return spline[selectedEndPointIndex].position;

		if( selectedEndPointIndex.controlPointIndex == EndPointIndex.CONTROL_POINT_PRECEDING )
			return spline[selectedEndPointIndex].precedingControlPointPosition;

		return spline[selectedEndPointIndex].followingControlPointPosition;
	}

	private void ResetEndPointAt( int index )
	{
		BezierPoint selectedEndPoint = spline[index];

		Undo.RecordObject( spline, "Reset Selected Point" );

		selectedEndPoint.SetPositionRotationScale( Vector3.zero, Quaternion.identity, Vector3.one );

		selectedEndPoint.precedingControlPointLocalPosition = Vector3.left;
		selectedEndPoint.followingControlPointLocalPosition = Vector3.right;
	}

	public void OnUndo()
	{
		worldSpaceRotationInitialized = false;
		Repaint();
	}

	private bool HasFrameBounds()
	{
		return true;
	}

	private Bounds OnGetFrameBounds()
	{
		if( selectedEndPointIndex == -1 )
		{
			return new Bounds( spline.transform.position, Vector3.one );
		}
		else
		{
			BezierPoint selectedEndPoint = spline[selectedEndPointIndex];

			if( selectedEndPointIndex.controlPointIndex == EndPointIndex.CONTROL_POINT_NONE )
				return new Bounds( selectedEndPoint.position, Vector3.one );
			
			if( selectedEndPointIndex.controlPointIndex == EndPointIndex.CONTROL_POINT_PRECEDING )
				return new Bounds( selectedEndPoint.precedingControlPointPosition, Vector3.one );

			return new Bounds( selectedEndPoint.followingControlPointPosition, Vector3.one );
		}
	}
}