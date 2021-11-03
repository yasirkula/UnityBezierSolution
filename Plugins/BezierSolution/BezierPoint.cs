using UnityEngine;

namespace BezierSolution
{
	[AddComponentMenu( "Bezier Solution/Bezier Point" )]
	[HelpURL( "https://github.com/yasirkula/UnityBezierSolution" )]
	public partial class BezierPoint : MonoBehaviour
	{
		public Vector3 localPosition
		{
			get { return transform.localPosition; }
			set
			{
				if( transform.localPosition == value )
					return;

				transform.localPosition = value;
				spline.dirtyFlags |= InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange;
			}
		}

#pragma warning disable 0649
		[SerializeField, HideInInspector]
		private Vector3 m_position;
		public Vector3 position
		{
			get { return m_position; }
			set
			{
				if( transform.position == value )
					return;

				transform.position = value;
				spline.dirtyFlags |= InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		public Quaternion localRotation
		{
			get { return transform.localRotation; }
			set
			{
				if( transform.localRotation == value )
					return;

				transform.localRotation = value;
				spline.dirtyFlags |= InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		public Quaternion rotation
		{
			get { return transform.rotation; }
			set
			{
				if( transform.rotation == value )
					return;

				transform.rotation = value;
				spline.dirtyFlags |= InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		public Vector3 localEulerAngles
		{
			get { return transform.localEulerAngles; }
			set
			{
				if( transform.localEulerAngles == value )
					return;

				transform.localEulerAngles = value;
				spline.dirtyFlags |= InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		public Vector3 eulerAngles
		{
			get { return transform.eulerAngles; }
			set
			{
				if( transform.eulerAngles == value )
					return;

				transform.eulerAngles = value;
				spline.dirtyFlags |= InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		public Vector3 localScale
		{
			get { return transform.localScale; }
			set
			{
				if( transform.localScale == value )
					return;

				transform.localScale = value;
				spline.dirtyFlags |= InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		[SerializeField, HideInInspector]
		private Vector3 m_precedingControlPointLocalPosition = Vector3.left;
		public Vector3 precedingControlPointLocalPosition
		{
			get { return m_precedingControlPointLocalPosition; }
			set
			{
				if( m_precedingControlPointLocalPosition == value )
					return;

				m_precedingControlPointLocalPosition = value;
				m_precedingControlPointPosition = transform.TransformPoint( value );

				if( m_handleMode == HandleMode.Aligned )
				{
					m_followingControlPointLocalPosition = -m_precedingControlPointLocalPosition.normalized * m_followingControlPointLocalPosition.magnitude;
					m_followingControlPointPosition = transform.TransformPoint( m_followingControlPointLocalPosition );
				}
				else if( m_handleMode == HandleMode.Mirrored )
				{
					m_followingControlPointLocalPosition = -m_precedingControlPointLocalPosition;
					m_followingControlPointPosition = transform.TransformPoint( m_followingControlPointLocalPosition );
				}

				spline.dirtyFlags |= InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		[SerializeField, HideInInspector]
		private Vector3 m_precedingControlPointPosition;
		public Vector3 precedingControlPointPosition
		{
			get { return m_precedingControlPointPosition; }
			set
			{
				if( m_precedingControlPointPosition == value )
					return;

				m_precedingControlPointPosition = value;
				m_precedingControlPointLocalPosition = transform.InverseTransformPoint( value );

				if( transform.hasChanged )
				{
					m_position = transform.position;
					m_followingControlPointPosition = transform.TransformPoint( m_followingControlPointLocalPosition );

					spline.dirtyFlags |= InternalDirtyFlags.EndPointTransformChange;
					transform.hasChanged = false;
				}

				if( m_handleMode == HandleMode.Aligned )
				{
					m_followingControlPointPosition = m_position - ( m_precedingControlPointPosition - m_position ).normalized *
																   ( m_followingControlPointPosition - m_position ).magnitude;
					m_followingControlPointLocalPosition = transform.InverseTransformPoint( m_followingControlPointPosition );
				}
				else if( m_handleMode == HandleMode.Mirrored )
				{
					m_followingControlPointPosition = 2f * m_position - m_precedingControlPointPosition;
					m_followingControlPointLocalPosition = transform.InverseTransformPoint( m_followingControlPointPosition );
				}

				spline.dirtyFlags |= InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		[SerializeField, HideInInspector]
		private Vector3 m_followingControlPointLocalPosition = Vector3.right;
		public Vector3 followingControlPointLocalPosition
		{
			get { return m_followingControlPointLocalPosition; }
			set
			{
				if( m_followingControlPointLocalPosition == value )
					return;

				m_followingControlPointLocalPosition = value;
				m_followingControlPointPosition = transform.TransformPoint( value );

				if( m_handleMode == HandleMode.Aligned )
				{
					m_precedingControlPointLocalPosition = -m_followingControlPointLocalPosition.normalized * m_precedingControlPointLocalPosition.magnitude;
					m_precedingControlPointPosition = transform.TransformPoint( m_precedingControlPointLocalPosition );
				}
				else if( m_handleMode == HandleMode.Mirrored )
				{
					m_precedingControlPointLocalPosition = -m_followingControlPointLocalPosition;
					m_precedingControlPointPosition = transform.TransformPoint( m_precedingControlPointLocalPosition );
				}

				spline.dirtyFlags |= InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		[SerializeField, HideInInspector]
		private Vector3 m_followingControlPointPosition;
		public Vector3 followingControlPointPosition
		{
			get { return m_followingControlPointPosition; }
			set
			{
				if( m_followingControlPointPosition == value )
					return;

				m_followingControlPointPosition = value;
				m_followingControlPointLocalPosition = transform.InverseTransformPoint( value );

				if( transform.hasChanged )
				{
					m_position = transform.position;
					m_precedingControlPointPosition = transform.TransformPoint( m_precedingControlPointLocalPosition );

					spline.dirtyFlags |= InternalDirtyFlags.EndPointTransformChange;
					transform.hasChanged = false;
				}

				if( m_handleMode == HandleMode.Aligned )
				{
					m_precedingControlPointPosition = m_position - ( m_followingControlPointPosition - m_position ).normalized *
																	( m_precedingControlPointPosition - m_position ).magnitude;
					m_precedingControlPointLocalPosition = transform.InverseTransformPoint( m_precedingControlPointPosition );
				}
				else if( m_handleMode == HandleMode.Mirrored )
				{
					m_precedingControlPointPosition = 2f * m_position - m_followingControlPointPosition;
					m_precedingControlPointLocalPosition = transform.InverseTransformPoint( m_precedingControlPointPosition );
				}

				spline.dirtyFlags |= InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		[SerializeField, HideInInspector]
		private HandleMode m_handleMode = HandleMode.Mirrored;
		public HandleMode handleMode
		{
			get { return m_handleMode; }
			set
			{
				if( m_handleMode == value )
					return;

				m_handleMode = value;

				if( value == HandleMode.Aligned || value == HandleMode.Mirrored )
				{
					// Temporarily change the value of m_precedingControlPointLocalPosition so that it will be different from precedingControlPointLocalPosition
					// and precedingControlPointLocalPosition's setter will run
					Vector3 _precedingControlPointLocalPosition = m_precedingControlPointLocalPosition;
					m_precedingControlPointLocalPosition -= Vector3.one;

					precedingControlPointLocalPosition = _precedingControlPointLocalPosition;
				}

				spline.dirtyFlags |= InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		[SerializeField, HideInInspector]
		[UnityEngine.Serialization.FormerlySerializedAs( "normal" )]
		private Vector3 m_normal = Vector3.up;
		public Vector3 normal
		{
			get { return m_normal; }
			set
			{
				if( m_normal == value )
					return;

				m_normal = value;
				spline.dirtyFlags |= InternalDirtyFlags.NormalChange;
			}
		}

		[SerializeField, HideInInspector]
		[UnityEngine.Serialization.FormerlySerializedAs( "autoCalculatedNormalAngleOffset" )]
		private float m_autoCalculatedNormalAngleOffset = 0f;
		public float autoCalculatedNormalAngleOffset
		{
			get { return m_autoCalculatedNormalAngleOffset; }
			set
			{
				if( m_autoCalculatedNormalAngleOffset == value )
					return;

				m_autoCalculatedNormalAngleOffset = value;
				spline.dirtyFlags |= InternalDirtyFlags.NormalOffsetChange;
			}
		}

		[SerializeField, HideInInspector]
		private Vector3[] m_intermediateNormals;
		public Vector3[] intermediateNormals
		{
			get { return m_intermediateNormals; }
			set
			{
				// In this special case, don't early exit if the assigned array is the same because one of its elements might have changed.
				// We can safely early exit if the assigned value was null or empty, though
				if( ( m_intermediateNormals == null || m_intermediateNormals.Length == 0 ) && ( value == null || value.Length == 0 ) )
					return;

				m_intermediateNormals = value;
				spline.dirtyFlags |= InternalDirtyFlags.NormalChange;
			}
		}

		[SerializeField, HideInInspector]
		[UnityEngine.Serialization.FormerlySerializedAs( "extraData" )]
		private ExtraData m_extraData;
		public ExtraData extraData
		{
			get { return m_extraData; }
			set
			{
				if( m_extraData == value )
					return;

				m_extraData = value;
				spline.dirtyFlags |= InternalDirtyFlags.ExtraDataChange;
			}
		}
#pragma warning restore 0649

		public BezierSpline spline { get; internal set; }
		public int index { get; internal set; }

		public BezierPoint previousPoint
		{
			get
			{
				if( spline )
				{
					if( index > 0 )
						return spline.endPoints[index - 1];
					else if( spline.loop )
						return spline.endPoints[spline.endPoints.Count - 1];
				}

				return null;
			}
		}

		public BezierPoint nextPoint
		{
			get
			{
				if( spline )
				{
					if( index < spline.endPoints.Count - 1 )
						return spline.endPoints[index + 1];
					else if( spline.loop )
						return spline.endPoints[0];
				}

				return null;
			}
		}

		private void Awake()
		{
			transform.hasChanged = true;
		}

		private void OnDestroy()
		{
			if( spline )
				spline.dirtyFlags |= InternalDirtyFlags.All;
		}

		public void CopyTo( BezierPoint other )
		{
			other.transform.localPosition = transform.localPosition;
			other.transform.localRotation = transform.localRotation;
			other.transform.localScale = transform.localScale;

			other.m_handleMode = m_handleMode;

			other.m_precedingControlPointLocalPosition = m_precedingControlPointLocalPosition;
			other.m_followingControlPointLocalPosition = m_followingControlPointLocalPosition;

			other.m_normal = m_normal;
			other.m_autoCalculatedNormalAngleOffset = m_autoCalculatedNormalAngleOffset;

			other.m_extraData = m_extraData;
		}

		public void Refresh()
		{
			m_position = transform.position;
			m_precedingControlPointPosition = transform.TransformPoint( m_precedingControlPointLocalPosition );
			m_followingControlPointPosition = transform.TransformPoint( m_followingControlPointLocalPosition );

			transform.hasChanged = false;
		}

		internal void RefreshIfChanged()
		{
			if( transform.hasChanged )
			{
				Refresh();
				spline.dirtyFlags |= InternalDirtyFlags.EndPointTransformChange | InternalDirtyFlags.ControlPointPositionChange;
			}
		}

		internal void SetNormalAndResetIntermediateNormals( Vector3 normal, string undo )
		{
			if( spline && spline.autoCalculateNormals )
				return;

#if UNITY_EDITOR
			if( !string.IsNullOrEmpty( undo ) )
				UnityEditor.Undo.RecordObject( this, undo );
#endif

			this.normal = normal;
			intermediateNormals = null;

			BezierPoint previousPoint = this.previousPoint;
			if( previousPoint && previousPoint.m_intermediateNormals != null && previousPoint.m_intermediateNormals.Length > 0 )
			{
#if UNITY_EDITOR
				if( !string.IsNullOrEmpty( undo ) )
					UnityEditor.Undo.RecordObject( previousPoint, undo );
#endif

				previousPoint.intermediateNormals = null;
			}
		}

		public void Reset()
		{
			localPosition = Vector3.zero;
			localRotation = Quaternion.identity;
			localScale = Vector3.one;

			precedingControlPointLocalPosition = Vector3.left;
			followingControlPointLocalPosition = Vector3.right;

			m_normal = Vector3.up;
			m_autoCalculatedNormalAngleOffset = 0f;

			m_extraData = new ExtraData();

			transform.hasChanged = true;
		}

#if UNITY_EDITOR
		[ContextMenu( "Invert Spline" )]
		private void InvertSplineContextMenu()
		{
			if( spline )
				spline.InvertSpline( "Invert spline" );
		}
#endif
	}
}