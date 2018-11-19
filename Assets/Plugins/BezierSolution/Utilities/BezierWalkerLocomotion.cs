using System.Collections.Generic;
using UnityEngine;

namespace BezierSolution
{
	public class BezierWalkerLocomotion : MonoBehaviour, IBezierWalker
	{
		private IBezierWalker walker;

		[SerializeField]
		private List<Transform> tailObjects;

		[SerializeField]
		private List<float> tailObjectDistances;

		public int TailLength { get { return tailObjects.Count; } }

		public Transform this[int index] { get { return tailObjects[index]; } }

		public float rotationLerpModifier = 10f;
		public bool lookForward = true;

		public BezierSpline Spline { get { return walker.Spline; } }
		public float NormalizedT { get { return walker.NormalizedT; } }
		public bool MovingForward { get { return walker.MovingForward; } }

		private void Awake()
		{
			IBezierWalker[] walkerComponents = GetComponents<IBezierWalker>();
			for( int i = 0; i < walkerComponents.Length; i++ )
			{
				if( !( walkerComponents[i] is BezierWalkerLocomotion ) && ( (MonoBehaviour) walkerComponents[i] ).enabled )
				{
					walker = walkerComponents[i];
					break;
				}
			}

			if( walker == null )
			{
				Debug.LogError( "Need to attach BezierWalkerLocomotion to an IBezierWalker!" );
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
			float t = NormalizedT;
			BezierSpline spline = Spline;
			bool forward = MovingForward;

			for( int i = 0; i < tailObjects.Count; i++ )
			{
				Transform tailObject = tailObjects[i];
				float dt = Time.deltaTime;

				if( forward )
				{
					tailObject.position = spline.MoveAlongSpline( ref t, -tailObjectDistances[i] );

					if( lookForward )
						tailObject.rotation = Quaternion.Lerp( tailObject.rotation, Quaternion.LookRotation( spline.GetTangent( t ) ), rotationLerpModifier * dt );
				}
				else
				{
					tailObject.position = spline.MoveAlongSpline( ref t, tailObjectDistances[i] );

					if( lookForward )
						tailObject.rotation = Quaternion.Lerp( tailObject.rotation, Quaternion.LookRotation( -spline.GetTangent( t ) ), rotationLerpModifier * dt );
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
	}
}