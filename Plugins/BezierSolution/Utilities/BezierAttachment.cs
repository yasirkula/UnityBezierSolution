using UnityEngine;

namespace BezierSolution
{
	[AddComponentMenu( "Bezier Solution/Bezier Attachment" )]
	[HelpURL( "https://github.com/yasirkula/UnityBezierSolution" )]
	[ExecuteInEditMode]
	public class BezierAttachment : MonoBehaviour
	{
		public enum RotationMode { No = 0, UseSplineNormals = 1, UseEndPointRotations = 2 };

#pragma warning disable 0649
		[SerializeField]
		private BezierSpline m_spline;
		public BezierSpline spline
		{
			get { return m_spline; }
			set
			{
				if( m_spline != value )
				{
					if( m_spline )
						m_spline.onSplineChanged -= OnSplineChanged;

					m_spline = value;

					if( m_spline && isActiveAndEnabled )
					{
						m_spline.onSplineChanged -= OnSplineChanged;
						m_spline.onSplineChanged += OnSplineChanged;

						OnSplineChanged( m_spline, DirtyFlags.All );
					}
				}
			}
		}

		[SerializeField]
		[Range( 0f, 1f )]
		private float m_normalizedT = 0f;
		public float normalizedT
		{
			get { return m_normalizedT; }
			set
			{
				value = Mathf.Clamp01( value );

				if( m_normalizedT != value )
				{
					m_normalizedT = value;

					if( isActiveAndEnabled )
						OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

		[Header( "Position" )]
		[SerializeField]
		private bool m_updatePosition = true;
		public bool updatePosition
		{
			get { return m_updatePosition; }
			set
			{
				if( m_updatePosition != value )
				{
					m_updatePosition = value;

					if( m_updatePosition && isActiveAndEnabled )
						OnSplineChanged( m_spline, DirtyFlags.SplineShapeChanged );
				}
			}
		}

		[SerializeField]
		private Vector3 m_positionOffset;
		public Vector3 positionOffset
		{
			get { return m_positionOffset; }
			set
			{
				if( m_positionOffset != value )
				{
					m_positionOffset = value;

					if( m_updatePosition && isActiveAndEnabled )
						OnSplineChanged( m_spline, DirtyFlags.SplineShapeChanged );
				}
			}
		}

		[Header( "Rotation" )]
		[SerializeField]
		private RotationMode m_updateRotation = RotationMode.UseSplineNormals;
		public RotationMode updateRotation
		{
			get { return m_updateRotation; }
			set
			{
				if( m_updateRotation != value )
				{
					m_updateRotation = value;

					if( m_updateRotation != RotationMode.No && isActiveAndEnabled )
						OnSplineChanged( m_spline, DirtyFlags.SplineShapeChanged );
				}
			}
		}

		[SerializeField]
		private Vector3 m_rotationOffset;
		public Vector3 rotationOffset
		{
			get { return m_rotationOffset; }
			set
			{
				if( m_rotationOffset != value )
				{
					m_rotationOffset = value;

					if( m_updateRotation != RotationMode.No && isActiveAndEnabled )
						OnSplineChanged( m_spline, DirtyFlags.SplineShapeChanged );
				}
			}
		}

#if UNITY_EDITOR
		[Header( "Other Settings" )]
		[SerializeField]
		private bool executeInEditMode = false;

		[SerializeField, HideInInspector]
		private BezierSpline prevSpline;
#endif
#pragma warning restore 0649

		private void OnEnable()
		{
			if( m_spline )
			{
				m_spline.onSplineChanged -= OnSplineChanged;
				m_spline.onSplineChanged += OnSplineChanged;

				OnSplineChanged( m_spline, DirtyFlags.All );
			}
		}

		private void OnDisable()
		{
			if( m_spline )
				m_spline.onSplineChanged -= OnSplineChanged;
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			UnityEditor.Undo.RecordObject( transform, "Modify BezierAttachment" );

			BezierSpline _spline = m_spline;
			m_spline = prevSpline;
			spline = prevSpline = _spline;

			if( isActiveAndEnabled )
				OnSplineChanged( m_spline, DirtyFlags.All );
		}

		private void LateUpdate()
		{
			if( transform.hasChanged )
				OnSplineChanged( m_spline, DirtyFlags.All );
		}
#endif

		private void OnSplineChanged( BezierSpline spline, DirtyFlags dirtyFlags )
		{
#if UNITY_EDITOR
			if( !executeInEditMode && !UnityEditor.EditorApplication.isPlaying )
				return;
#endif

			RefreshInternal( dirtyFlags );
		}

		public void Refresh()
		{
			RefreshInternal( DirtyFlags.All );
		}

		private void RefreshInternal( DirtyFlags dirtyFlags )
		{
			if( !m_spline || m_spline.Count < 2 )
				return;

			if( !m_updatePosition && m_updateRotation == RotationMode.No )
				return;

			BezierSpline.Segment segment = m_spline.GetSegmentAt( m_normalizedT );

			switch( m_updateRotation )
			{
				case RotationMode.UseSplineNormals:
					if( m_rotationOffset == Vector3.zero )
						transform.rotation = Quaternion.LookRotation( segment.GetTangent(), segment.GetNormal() );
					else
						transform.rotation = Quaternion.LookRotation( segment.GetTangent(), segment.GetNormal() ) * Quaternion.Euler( m_rotationOffset );

					break;
				case RotationMode.UseEndPointRotations:
					if( m_rotationOffset == Vector3.zero )
						transform.rotation = Quaternion.LerpUnclamped( segment.point1.rotation, segment.point2.rotation, segment.localT );
					else
						transform.rotation = Quaternion.LerpUnclamped( segment.point1.rotation, segment.point2.rotation, segment.localT ) * Quaternion.Euler( m_rotationOffset );

					break;
			}

			if( m_updatePosition && ( dirtyFlags & DirtyFlags.SplineShapeChanged ) == DirtyFlags.SplineShapeChanged )
			{
				if( m_positionOffset == Vector3.zero )
					transform.position = segment.GetPoint();
				else
					transform.position = segment.GetPoint() + transform.rotation * m_positionOffset;
			}

#if UNITY_EDITOR
			transform.hasChanged = false;
#endif
		}
	}
}