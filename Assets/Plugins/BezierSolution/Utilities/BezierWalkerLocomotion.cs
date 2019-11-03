using System.Collections.Generic;
using UnityEngine;

namespace BezierSolution
{
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

		public float movementLerpModifier = 10f;
		public float rotationLerpModifier = 10f;

		[System.Obsolete( "Use lookAt instead", true )]
		[System.NonSerialized]
		public bool lookForward = true;
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
			float t = NormalizedT;
			BezierSpline spline = Spline;
			bool forward = MovingForward;

			for( int i = 0; i < tailObjects.Count; i++ )
			{
				Transform tailObject = tailObjects[i];

				if( forward )
				{
					tailObject.position = Vector3.Lerp( tailObject.position, spline.MoveAlongSpline( ref t, -tailObjectDistances[i] ), movementLerpModifier * deltaTime );

					if( lookAt == LookAtMode.Forward )
						tailObject.rotation = Quaternion.Lerp( tailObject.rotation, Quaternion.LookRotation( spline.GetTangent( t ) ), rotationLerpModifier * deltaTime );
					else if( lookAt == LookAtMode.SplineExtraData )
						tailObject.rotation = Quaternion.Lerp( tailObject.rotation, spline.GetExtraData( t, InterpolateExtraDataAsQuaternion ), rotationLerpModifier * deltaTime );
				}
				else
				{
					tailObject.position = Vector3.Lerp( tailObject.position, spline.MoveAlongSpline( ref t, tailObjectDistances[i] ), movementLerpModifier * deltaTime );

					if( lookAt == LookAtMode.Forward )
						tailObject.rotation = Quaternion.Lerp( tailObject.rotation, Quaternion.LookRotation( -spline.GetTangent( t ) ), rotationLerpModifier * deltaTime );
					else if( lookAt == LookAtMode.SplineExtraData )
						tailObject.rotation = Quaternion.Lerp( tailObject.rotation, spline.GetExtraData( t, InterpolateExtraDataAsQuaternion ), rotationLerpModifier * deltaTime );
				}
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