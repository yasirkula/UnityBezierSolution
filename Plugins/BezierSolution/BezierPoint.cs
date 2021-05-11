using UnityEngine;

namespace BezierSolution
{
	[AddComponentMenu( "Bezier Solution/Bezier Point" )]
	public class BezierPoint : MonoBehaviour
	{
		[System.Serializable]
		public struct ExtraData
		{
			public float c1, c2, c3, c4;

			public ExtraData( float c1 = 0f, float c2 = 0f, float c3 = 0f, float c4 = 0f )
			{
				this.c1 = c1;
				this.c2 = c2;
				this.c3 = c3;
				this.c4 = c4;
			}

			public static ExtraData Lerp( ExtraData a, ExtraData b, float t )
			{
				t = Mathf.Clamp01( t );
				return new ExtraData(
					a.c1 + ( b.c1 - a.c1 ) * t,
					a.c2 + ( b.c2 - a.c2 ) * t,
					a.c3 + ( b.c3 - a.c3 ) * t,
					a.c4 + ( b.c4 - a.c4 ) * t );
			}

			public static ExtraData LerpUnclamped( ExtraData a, ExtraData b, float t )
			{
				return new ExtraData(
					a.c1 + ( b.c1 - a.c1 ) * t,
					a.c2 + ( b.c2 - a.c2 ) * t,
					a.c3 + ( b.c3 - a.c3 ) * t,
					a.c4 + ( b.c4 - a.c4 ) * t );
			}

			public static implicit operator ExtraData( Vector2 v ) { return new ExtraData( v.x, v.y ); }
			public static implicit operator ExtraData( Vector3 v ) { return new ExtraData( v.x, v.y, v.z ); }
			public static implicit operator ExtraData( Vector4 v ) { return new ExtraData( v.x, v.y, v.z, v.w ); }
			public static implicit operator ExtraData( Quaternion q ) { return new ExtraData( q.x, q.y, q.z, q.w ); }
			public static implicit operator ExtraData( Rect r ) { return new ExtraData( r.xMin, r.yMin, r.width, r.height ); }
#if UNITY_2017_2_OR_NEWER
			public static implicit operator ExtraData( Vector2Int v ) { return new ExtraData( v.x, v.y ); }
			public static implicit operator ExtraData( Vector3Int v ) { return new ExtraData( v.x, v.y, v.z ); }
			public static implicit operator ExtraData( RectInt r ) { return new ExtraData( r.xMin, r.yMin, r.width, r.height ); }
#endif

			public static implicit operator Vector2( ExtraData v ) { return new Vector2( v.c1, v.c2 ); }
			public static implicit operator Vector3( ExtraData v ) { return new Vector3( v.c1, v.c2, v.c3 ); }
			public static implicit operator Vector4( ExtraData v ) { return new Vector4( v.c1, v.c2, v.c3, v.c4 ); }
			public static implicit operator Quaternion( ExtraData v ) { return new Quaternion( v.c1, v.c2, v.c3, v.c4 ); }
			public static implicit operator Rect( ExtraData v ) { return new Rect( v.c1, v.c2, v.c3, v.c4 ); }
#if UNITY_2017_2_OR_NEWER
			public static implicit operator Vector2Int( ExtraData v ) { return new Vector2Int( Mathf.RoundToInt( v.c1 ), Mathf.RoundToInt( v.c2 ) ); }
			public static implicit operator Vector3Int( ExtraData v ) { return new Vector3Int( Mathf.RoundToInt( v.c1 ), Mathf.RoundToInt( v.c2 ), Mathf.RoundToInt( v.c3 ) ); }
			public static implicit operator RectInt( ExtraData v ) { return new RectInt( Mathf.RoundToInt( v.c1 ), Mathf.RoundToInt( v.c2 ), Mathf.RoundToInt( v.c3 ), Mathf.RoundToInt( v.c4 ) ); }
#endif

			public static bool operator ==( ExtraData d1, ExtraData d2 ) { return d1.c1 == d2.c1 && d1.c2 == d2.c2 && d1.c3 == d2.c3 && d1.c4 == d2.c4; }
			public static bool operator !=( ExtraData d1, ExtraData d2 ) { return d1.c1 != d2.c1 || d1.c2 != d2.c2 || d1.c3 != d2.c3 || d1.c4 != d2.c4; }

			public override bool Equals( object obj ) { return obj is ExtraData && this == (ExtraData) obj; }
			public override int GetHashCode() { return unchecked((int) ( ( ( ( 17 * 23 + c1 ) * 23 + c2 ) * 23 + c3 ) * 23 + c4 )); }
			public override string ToString() { return ( (Vector4) this ).ToString(); }
		}

		public enum HandleMode { Free, Aligned, Mirrored };

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
	}
}