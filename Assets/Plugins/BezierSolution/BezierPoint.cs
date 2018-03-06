using UnityEngine;

namespace BezierSolution
{
	public class BezierPoint : MonoBehaviour
	{
		public enum HandleMode { Free, Aligned, Mirrored };

		public Vector3 localPosition
		{
			get { return transform.localPosition; }
			set { transform.localPosition = value; }
		}

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
			set { transform.position = value; }
		}
		
		public Quaternion localRotation
		{
			get { return transform.localRotation; }
			set { transform.localRotation = value; }
		}

		public Quaternion rotation
		{
			get { return transform.rotation; }
			set { transform.rotation = value; }
		}

		public Vector3 localEulerAngles
		{
			get { return transform.localEulerAngles; }
			set { transform.localEulerAngles = value; }
		}

		public Vector3 eulerAngles
		{
			get { return transform.eulerAngles; }
			set { transform.eulerAngles = value; }
		}
		
		public Vector3 localScale
		{
			get { return transform.localScale; }
			set { transform.localScale = value; }
		}

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
			}
		}

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