using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BezierSolution.Extras
{
	public abstract class BezierWalkerEditor<T> : Editor where T : BezierWalker
	{
		protected T[] walkers;

		private bool simulateInEditor;
		private double lastUpdateTime;

		protected bool hasInitialData;
		protected List<Vector3> initialPositions = new List<Vector3>( 0 );
		protected List<Quaternion> initialRotations = new List<Quaternion>( 0 );
		protected List<float> initialNormalizedTs = new List<float>( 0 );

		private void OnEnable()
		{
			walkers = Array.ConvertAll( targets, ( e ) => (T) e );

			if( simulateInEditor )
				StartSimulateInEditor();
		}

		private void OnDisable()
		{
			StopSimulateInEditor();
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			BezierUtils.DrawSeparator();
			EditorGUI.BeginChangeCheck();
			simulateInEditor = GUILayout.Toggle( simulateInEditor, "Simulate In Editor", GUI.skin.button );
			if( EditorGUI.EndChangeCheck() )
			{
				if( simulateInEditor )
					StartSimulateInEditor();
				else
					StopSimulateInEditor();
			}
		}

		private void StartSimulateInEditor()
		{
			SaveInitialData();

			for( int i = 0; i < walkers.Length; i++ )
				walkers[i].MovingForward = true;

			lastUpdateTime = EditorApplication.timeSinceStartup;
			EditorApplication.update -= SimulateInEditor;
			EditorApplication.update += SimulateInEditor;
		}

		private void StopSimulateInEditor()
		{
			EditorApplication.update -= SimulateInEditor;

			if( hasInitialData )
			{
				hasInitialData = false;
				RestoreInitialData();
			}

			simulateInEditor = false;
		}

		protected virtual void SaveInitialData()
		{
			initialPositions.Clear();
			initialRotations.Clear();
			initialNormalizedTs.Clear();

			for( int i = 0; i < walkers.Length; i++ )
			{
				initialPositions.Add( walkers[i].transform.position );
				initialRotations.Add( walkers[i].transform.rotation );
				initialNormalizedTs.Add( walkers[i].NormalizedT );
			}

			hasInitialData = true;
		}

		protected virtual void RestoreInitialData()
		{
			for( int i = 0; i < walkers.Length; i++ )
			{
				if( walkers[i] )
				{
					walkers[i].transform.position = initialPositions[i];
					walkers[i].transform.rotation = initialRotations[i];
					walkers[i].NormalizedT = initialNormalizedTs[i];
				}
			}
		}

		private void SimulateInEditor()
		{
			if( EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying )
			{
				// Stop the simulation if we are about to enter Play mode
				StopSimulateInEditor();
			}
			else
			{
				double time = EditorApplication.timeSinceStartup;
				Simulate( (float) ( time - lastUpdateTime ) );
				lastUpdateTime = time;
			}
		}

		protected virtual void Simulate( float deltaTime )
		{
			for( int i = 0; i < walkers.Length; i++ )
				walkers[i].Execute( deltaTime );
		}
	}
}