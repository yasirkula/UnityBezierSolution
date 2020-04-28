using UnityEngine;
using UnityEngine.Events;

namespace BezierSolution
{
	public class BezierWalkerWithTime : BezierWalker
	{
		public BezierSpline spline;
		public TravelMode travelMode;

		public float travelTime = 5f;
		[SerializeField]
		[Range( 0f, 1f )]
		private float m_normalizedT = 0f;

		public override BezierSpline Spline { get { return spline; } }

		public override float NormalizedT
		{
			get { return m_normalizedT; }
			set { m_normalizedT = value; }
		}

		public float movementLerpModifier = 10f;
		public float rotationLerpModifier = 10f;

		[System.Obsolete( "Use lookAt instead", true )]
		[System.NonSerialized]
		public bool lookForward = true;
		public LookAtMode lookAt = LookAtMode.Forward;

		private bool isGoingForward = true;
		public override bool MovingForward { get { return isGoingForward; } }

		public UnityEvent onPathCompleted = new UnityEvent();
		private bool onPathCompletedCalledAt1 = false;
		private bool onPathCompletedCalledAt0 = false;

		private void Update()
		{
			Execute( Time.deltaTime );
		}

		public override void Execute( float deltaTime )
		{
			transform.position = Vector3.Lerp( transform.position, spline.GetPoint( m_normalizedT ), movementLerpModifier * deltaTime );

			if( lookAt == LookAtMode.Forward )
			{
				Quaternion targetRotation;
				if( isGoingForward )
					targetRotation = Quaternion.LookRotation( spline.GetTangent( m_normalizedT ) );
				else
					targetRotation = Quaternion.LookRotation( -spline.GetTangent( m_normalizedT ) );

				transform.rotation = Quaternion.Lerp( transform.rotation, targetRotation, rotationLerpModifier * deltaTime );
			}
			else if( lookAt == LookAtMode.SplineExtraData )
				transform.rotation = Quaternion.Lerp( transform.rotation, spline.GetExtraData( m_normalizedT, extraDataLerpAsQuaternionFunction ), rotationLerpModifier * deltaTime );

			if( isGoingForward )
			{
				m_normalizedT += deltaTime / travelTime;

				if( m_normalizedT > 1f )
				{
					if( travelMode == TravelMode.Once )
						m_normalizedT = 1f;
					else if( travelMode == TravelMode.Loop )
						m_normalizedT -= 1f;
					else
					{
						m_normalizedT = 2f - m_normalizedT;
						isGoingForward = false;
					}

					if( !onPathCompletedCalledAt1 )
					{
						onPathCompletedCalledAt1 = true;
#if UNITY_EDITOR
						if( UnityEditor.EditorApplication.isPlaying )
#endif
							onPathCompleted.Invoke();
					}
				}
				else
				{
					onPathCompletedCalledAt1 = false;
				}
			}
			else
			{
				m_normalizedT -= deltaTime / travelTime;

				if( m_normalizedT < 0f )
				{
					if( travelMode == TravelMode.Once )
						m_normalizedT = 0f;
					else if( travelMode == TravelMode.Loop )
						m_normalizedT += 1f;
					else
					{
						m_normalizedT = -m_normalizedT;
						isGoingForward = true;
					}

					if( !onPathCompletedCalledAt0 )
					{
						onPathCompletedCalledAt0 = true;
#if UNITY_EDITOR
						if( UnityEditor.EditorApplication.isPlaying )
#endif
							onPathCompleted.Invoke();
					}
				}
				else
				{
					onPathCompletedCalledAt0 = false;
				}
			}
		}
	}
}