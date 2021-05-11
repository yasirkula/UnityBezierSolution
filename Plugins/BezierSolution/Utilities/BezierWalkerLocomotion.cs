using System.Collections.Generic;
using UnityEngine;

namespace BezierSolution
{
	[AddComponentMenu( "Bezier Solution/Bezier Walker Locomotion" )]
	public class BezierWalkerLocomotion : BezierWalker
	{
		public BezierWalker walker;

#pragma warning disable 0649
		[SerializeField]
		private List<Transform> tailObjects;
		public List<Transform> Tail { get { return tailObjects; } }

		[SerializeField]
		private List<float> tailObjectDistances;
		public List<float> TailDistances { get { return tailObjectDistances; } }
#pragma warning restore 0649

		public bool highQuality = true; // true by default because when it is set to false, tail objects can jitter too much

		public float movementLerpModifier = 10f;
		public float rotationLerpModifier = 10f;

		public LookAtMode lookAt = LookAtMode.Forward;

		public override BezierSpline Spline { get { return walker.Spline; } }
		public override bool MovingForward { get { return walker.MovingForward; } }
		public override float NormalizedT
		{
			get { return walker.NormalizedT; }
			set { walker.NormalizedT = value; }
		}

		private void Start()
		{
			if( !walker )
			{
				Debug.LogError( "Need to attach BezierWalkerLocomotion to a BezierWalker!" );
				Destroy( this );
			}

			if( tailObjects.Count != tailObjectDistances.Count )
			{
				Debug.LogError( "One distance per tail object is needed!" );
				Destroy( this );
			}
		}

		private void LateUpdate()
		{
			Execute( Time.deltaTime );
		}

		public override void Execute( float deltaTime )
		{
			BezierSpline spline = Spline;
			float t = highQuality ? spline.evenlySpacedPoints.GetPercentageAtNormalizedT( NormalizedT ) : NormalizedT;
			bool forward = MovingForward;

			for( int i = 0; i < tailObjects.Count; i++ )
			{
				Transform tailObject = tailObjects[i];
				Vector3 tailPosition;
				float tailNormalizedT;
				if( highQuality )
				{
					if( forward )
						t -= tailObjectDistances[i] / spline.evenlySpacedPoints.splineLength;
					else
						t += tailObjectDistances[i] / spline.evenlySpacedPoints.splineLength;

					tailNormalizedT = spline.evenlySpacedPoints.GetNormalizedTAtPercentage( t );
					tailPosition = spline.GetPoint( tailNormalizedT );
				}
				else
				{
					tailPosition = spline.MoveAlongSpline( ref t, forward ? -tailObjectDistances[i] : tailObjectDistances[i] );
					tailNormalizedT = t;
				}

				tailObject.position = Vector3.Lerp( tailObject.position, tailPosition, movementLerpModifier * deltaTime );

				if( lookAt == LookAtMode.Forward )
				{
					BezierSpline.Segment segment = spline.GetSegmentAt( tailNormalizedT );
					tailObject.rotation = Quaternion.Lerp( tailObject.rotation, Quaternion.LookRotation( forward ? segment.GetTangent() : -segment.GetTangent(), segment.GetNormal() ), rotationLerpModifier * deltaTime );
				}
				else if( lookAt == LookAtMode.SplineExtraData )
					tailObject.rotation = Quaternion.Lerp( tailObject.rotation, spline.GetExtraData( tailNormalizedT, extraDataLerpAsQuaternionFunction ), rotationLerpModifier * deltaTime );
			}
		}

		public void AddToTail( Transform transform, float distanceToPreviousObject )
		{
			if( transform == null )
			{
				Debug.LogError( "Object is null!" );
				return;
			}

			tailObjects.Add( transform );
			tailObjectDistances.Add( distanceToPreviousObject );
		}

		public void InsertIntoTail( int index, Transform transform, float distanceToPreviousObject )
		{
			if( transform == null )
			{
				Debug.LogError( "Object is null!" );
				return;
			}

			tailObjects.Insert( index, transform );
			tailObjectDistances.Insert( index, distanceToPreviousObject );
		}

		public void RemoveFromTail( Transform transform )
		{
			if( transform == null )
			{
				Debug.LogError( "Object is null!" );
				return;
			}

			for( int i = 0; i < tailObjects.Count; i++ )
			{
				if( tailObjects[i] == transform )
				{
					tailObjects.RemoveAt( i );
					tailObjectDistances.RemoveAt( i );

					return;
				}
			}
		}

#if UNITY_EDITOR
		private void Reset()
		{
			BezierWalker[] walkerComponents = GetComponents<BezierWalker>();
			for( int i = 0; i < walkerComponents.Length; i++ )
			{
				if( !( walkerComponents[i] is BezierWalkerLocomotion ) && ( (MonoBehaviour) walkerComponents[i] ).enabled )
				{
					walker = walkerComponents[i];
					break;
				}
			}
		}
#endif
	}
}