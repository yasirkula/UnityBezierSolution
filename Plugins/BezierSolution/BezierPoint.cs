using UnityEngine;

namespace BezierSolution
{
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
				transform.localPosition = value;
				SetSplineDirty();
			}
		}

#pragma warning disable 0649
		[SerializeField]
		[HideInInspector]
		private Vector3 m_position;
		public Vector3 position
		{
			get
			{
				if( transform.hasChanged )
					Revalidate();

				return m_position;
			}
			set
			{
				transform.position = value;
				SetSplineDirty();
			}
		}
#pragma warning restore 0649

		public Quaternion localRotation
		{
			get { return transform.localRotation; }
			set
			{
				transform.localRotation = value;
				SetSplineDirty();
			}
		}

		public Quaternion rotation
		{
			get { return transform.rotation; }
			set
			{
				transform.rotation = value;
				SetSplineDirty();
			}
		}

		public Vector3 localEulerAngles
		{
			get { return transform.localEulerAngles; }
			set
			{
				transform.localEulerAngles = value;
				SetSplineDirty();
			}
		}

		public Vector3 eulerAngles
		{
			get { return transform.eulerAngles; }
			set
			{
				transform.eulerAngles = value;
				SetSplineDirty();
			}
		}

		public Vector3 localScale
		{
			get { return transform.localScale; }
			set
			{
				transform.localScale = value;
				SetSplineDirty();
			}
		}

#pragma warning disable 0649
		[SerializeField]
		[HideInInspector]
		private Vector3 m_precedingControlPointLocalPosition = Vector3.left;
		public Vector3 precedingControlPointLocalPosition
		{
			get
			{
				return m_precedingControlPointLocalPosition;
			}
			set
			{
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

				SetSplineDirty();
			}
		}

		[SerializeField]
		[HideInInspector]
		private Vector3 m_precedingControlPointPosition;
		public Vector3 precedingControlPointPosition
		{
			get
			{
				if( transform.hasChanged )
					Revalidate();

				return m_precedingControlPointPosition;
			}
			set
			{
				m_precedingControlPointPosition = value;
				m_precedingControlPointLocalPosition = transform.InverseTransformPoint( value );

				if( transform.hasChanged )
				{
					m_position = transform.position;
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

				SetSplineDirty();
			}
		}

		[SerializeField]
		[HideInInspector]
		private Vector3 m_followingControlPointLocalPosition = Vector3.right;
		public Vector3 followingControlPointLocalPosition
		{
			get
			{
				return m_followingControlPointLocalPosition;
			}
			set
			{
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

				SetSplineDirty();
			}
		}

		[SerializeField]
		[HideInInspector]
		private Vector3 m_followingControlPointPosition;
		public Vector3 followingControlPointPosition
		{
			get
			{
				if( transform.hasChanged )
					Revalidate();

				return m_followingControlPointPosition;
			}
			set
			{
				m_followingControlPointPosition = value;
				m_followingControlPointLocalPosition = transform.InverseTransformPoint( value );

				if( transform.hasChanged )
				{
					m_position = transform.position;
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

				SetSplineDirty();
			}
		}

		[SerializeField]
		[HideInInspector]
		private HandleMode m_handleMode = HandleMode.Mirrored;
		public HandleMode handleMode
		{
			get
			{
				return m_handleMode;
			}
			set
			{
				m_handleMode = value;

				if( value == HandleMode.Aligned || value == HandleMode.Mirrored )
					precedingControlPointLocalPosition = m_precedingControlPointLocalPosition;

				SetSplineDirty();
			}
		}

		[HideInInspector]
		public ExtraData extraData;
#pragma warning restore 0649

#if UNITY_EDITOR
		[System.NonSerialized]
		public BezierSpline Internal_Spline;
		[System.NonSerialized]
		public int Internal_Index;
#endif

		private void Awake()
		{
			transform.hasChanged = true;
		}

		public void CopyTo( BezierPoint other )
		{
			other.transform.localPosition = transform.localPosition;
			other.transform.localRotation = transform.localRotation;
			other.transform.localScale = transform.localScale;

			other.m_handleMode = m_handleMode;

			other.m_precedingControlPointLocalPosition = m_precedingControlPointLocalPosition;
			other.m_followingControlPointLocalPosition = m_followingControlPointLocalPosition;
		}

		private void Revalidate()
		{
			m_position = transform.position;
			m_precedingControlPointPosition = transform.TransformPoint( m_precedingControlPointLocalPosition );
			m_followingControlPointPosition = transform.TransformPoint( m_followingControlPointLocalPosition );

			transform.hasChanged = false;
		}

		[System.Diagnostics.Conditional( "UNITY_EDITOR" )]
		private void SetSplineDirty()
		{
#if UNITY_EDITOR
			if( Internal_Spline )
				Internal_Spline.Internal_IsDirty = true;
#endif
		}

		public void Reset()
		{
			localPosition = Vector3.zero;
			localRotation = Quaternion.identity;
			localScale = Vector3.one;

			precedingControlPointLocalPosition = Vector3.left;
			followingControlPointLocalPosition = Vector3.right;

			transform.hasChanged = true;
		}
	}
}