using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BezierSolution.Extras
{
	[CustomEditor( typeof( BezierWalkerLocomotion ) )]
	[CanEditMultipleObjects]
	public class BezierWalkerLocomotionEditor : BezierWalkerEditor
	{
		private int tailSaveDataStartIndex;

		protected override void SaveInitialData()
		{
			base.SaveInitialData();
			tailSaveDataStartIndex = initialPositions.Count;

			for( int i = 0; i < walkers.Length; i++ )
			{
				List<Transform> tail = ( (BezierWalkerLocomotion) walkers[i] ).Tail;
				for( int j = 0; j < tail.Count; j++ )
				{
					initialPositions.Add( tail[j].position );
					initialRotations.Add( tail[j].rotation );
				}
			}
		}

		protected override void RestoreInitialData()
		{
			base.RestoreInitialData();

			int index = tailSaveDataStartIndex;
			for( int i = 0; i < walkers.Length; i++ )
			{
				List<Transform> tail = ( (BezierWalkerLocomotion) walkers[i] ).Tail;
				for( int j = 0; j < tail.Count; j++, index++ )
				{
					tail[j].position = initialPositions[index];
					tail[j].rotation = initialRotations[index];
				}
			}
		}

		protected override void Simulate( float deltaTime )
		{
			for( int i = 0; i < walkers.Length; i++ )
				( (BezierWalkerLocomotion) walkers[i] ).walker.Execute( deltaTime );

			base.Simulate( deltaTime );
		}
	}
}