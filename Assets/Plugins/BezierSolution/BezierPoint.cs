using UnityEngine;

[System.Serializable]
public class BezierPoint
{
	public enum HandleMode { Free, Aligned, Mirrored };

	[SerializeField]
	[HideInInspector]
	private BezierSpline spline;

	[SerializeField]
	[HideInInspector]
	private Vector3 m_localPosition;
	public Vector3 localPosition
	{
		get
		{
			return m_localPosition;
		}
		set
		{
			m_localPosition = value;

			Revalidate();
		}
	}

	[SerializeField]
	[HideInInspector]
	private Vector3 m_position;
	public Vector3 position
	{
		get
		{
			return m_position;
		}
		set
		{
			m_position = value;
			m_localPosition = spline.transform.InverseTransformPoint( m_position );

			Revalidate( false );
		}
	}

	[SerializeField]
	[HideInInspector]
	private Quaternion m_localRotation;
	public Quaternion localRotation
	{
		get
		{
			return m_localRotation;
		}
		set
		{
			m_localRotation = value;
			m_localEulerAngles = m_localRotation.eulerAngles;

			Revalidate();
		}
	}

	public Quaternion rotation
	{
		get
		{
			return spline.transform.rotation * localRotation;
		}
		set
		{
			localRotation = Quaternion.Inverse( spline.transform.rotation ) * value;
		}
	}

	[SerializeField]
	[HideInInspector]
	private Vector3 m_localEulerAngles;
	public Vector3 localEulerAngles
	{
		get
		{
			return m_localEulerAngles;
		}
		set
		{
			m_localEulerAngles = value;
			m_localRotation = Quaternion.Euler( m_localEulerAngles );

			Revalidate();
		}
	}

	public Vector3 eulerAngles
	{
		get
		{
			return rotation.eulerAngles;
		}
		set
		{
			rotation = Quaternion.Euler( value );
		}
	}

	[SerializeField]
	[HideInInspector]
	private Vector3 m_localScale;
	public Vector3 localScale
	{
		get
		{
			return m_localScale;
		}
		set
		{
			m_localScale = value;

			Revalidate();
		}
	}

	[SerializeField]
	[HideInInspector]
	private Vector3 m_precedingControlPointLocalPosition;
	public Vector3 precedingControlPointLocalPosition
	{
		get
		{
			return m_precedingControlPointLocalPosition;
		}
		set
		{
			Matrix4x4 localToWorldMatrix = this.localToWorldMatrix;

			m_precedingControlPointLocalPosition = value;
			m_precedingControlPointPosition = localToWorldMatrix.MultiplyPoint3x4( value );

			if( m_handleMode == HandleMode.Aligned )
			{
				m_followingControlPointLocalPosition = -m_precedingControlPointLocalPosition.normalized * m_followingControlPointLocalPosition.magnitude;
				m_followingControlPointPosition = localToWorldMatrix.MultiplyPoint3x4( m_followingControlPointLocalPosition );
			}
			else if( m_handleMode == HandleMode.Mirrored )
			{
				m_followingControlPointLocalPosition = -m_precedingControlPointLocalPosition;
				m_followingControlPointPosition = localToWorldMatrix.MultiplyPoint3x4( m_followingControlPointLocalPosition );
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
			return m_precedingControlPointPosition;
		}
		set
		{
			Matrix4x4 worldToLocalMatrix = this.worldToLocalMatrix;

			m_precedingControlPointPosition = value;
			m_precedingControlPointLocalPosition = worldToLocalMatrix.MultiplyPoint3x4( value );

			if( m_handleMode == HandleMode.Aligned )
			{
				m_followingControlPointPosition = m_position - ( m_precedingControlPointPosition - m_position ).normalized *
															   ( m_followingControlPointPosition - m_position ).magnitude;
				m_followingControlPointLocalPosition = worldToLocalMatrix.MultiplyPoint3x4( m_followingControlPointPosition );
			}
			else if( m_handleMode == HandleMode.Mirrored )
			{
				m_followingControlPointPosition = 2f * m_position - m_precedingControlPointPosition;
				m_followingControlPointLocalPosition = worldToLocalMatrix.MultiplyPoint3x4( m_followingControlPointPosition );
			}
		}
	}

	[SerializeField]
	[HideInInspector]
	private Vector3 m_followingControlPointLocalPosition;
	public Vector3 followingControlPointLocalPosition
	{
		get
		{
			return m_followingControlPointLocalPosition;
		}
		set
		{
			Matrix4x4 localToWorldMatrix = this.localToWorldMatrix;

			m_followingControlPointLocalPosition = value;
			m_followingControlPointPosition = localToWorldMatrix.MultiplyPoint3x4( value );

			if( m_handleMode == HandleMode.Aligned )
			{
				m_precedingControlPointLocalPosition = -m_followingControlPointLocalPosition.normalized * m_precedingControlPointLocalPosition.magnitude;
				m_precedingControlPointPosition = localToWorldMatrix.MultiplyPoint3x4( m_precedingControlPointLocalPosition );
			}
			else if( m_handleMode == HandleMode.Mirrored )
			{
				m_precedingControlPointLocalPosition = -m_followingControlPointLocalPosition;
				m_precedingControlPointPosition = localToWorldMatrix.MultiplyPoint3x4( m_precedingControlPointLocalPosition );
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
			return m_followingControlPointPosition;
		}
		set
		{
			Matrix4x4 worldToLocalMatrix = this.worldToLocalMatrix;

			m_followingControlPointPosition = value;
			m_followingControlPointLocalPosition = worldToLocalMatrix.MultiplyPoint3x4( value );

			if( m_handleMode == HandleMode.Aligned )
			{
				m_precedingControlPointPosition = m_position - ( m_followingControlPointPosition - m_position ).normalized * 
																( m_precedingControlPointPosition - m_position ).magnitude;
				m_precedingControlPointLocalPosition = worldToLocalMatrix.MultiplyPoint3x4( m_precedingControlPointPosition );
			}
			else if( m_handleMode == HandleMode.Mirrored )
			{
				m_precedingControlPointPosition = 2f * m_position - m_followingControlPointPosition;
				m_precedingControlPointLocalPosition = worldToLocalMatrix.MultiplyPoint3x4( m_precedingControlPointPosition );
			}
		}
	}

	[SerializeField]
	[HideInInspector]
	private HandleMode m_handleMode;
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

	public Matrix4x4 localToWorldMatrix
	{
		get
		{
			Matrix4x4 selfTransformationMatrix = Matrix4x4.identity;
			selfTransformationMatrix.SetTRS( m_localPosition, m_localRotation, m_localScale );

			return spline.localToWorldMatrix * selfTransformationMatrix;
		}
	}

	public Matrix4x4 worldToLocalMatrix
	{
		get
		{
			return localToWorldMatrix.inverse;
		}
	}

	public BezierPoint( BezierSpline spline ) : this( spline, Vector3.zero )
	{
	}

	public BezierPoint( BezierSpline spline, Vector3 localPosition )
	{
		this.spline = spline;

		m_localPosition = localPosition;
		m_localRotation = Quaternion.identity;
		m_localEulerAngles = Vector3.zero;
		m_localScale = Vector3.one;

		m_precedingControlPointLocalPosition = Vector3.left;
		m_followingControlPointLocalPosition = Vector3.right;

		m_handleMode = HandleMode.Mirrored;

		Revalidate();
	}

	public BezierPoint( BezierSpline spline, BezierPoint pointToCopy )
	{
		this.spline = spline;

		this.m_localPosition = pointToCopy.m_localPosition;
		this.m_localRotation = pointToCopy.m_localRotation;
		this.m_localEulerAngles = pointToCopy.m_localEulerAngles;
		this.m_localScale = pointToCopy.m_localScale;

		this.m_precedingControlPointLocalPosition = pointToCopy.m_precedingControlPointLocalPosition;
		this.m_followingControlPointLocalPosition = pointToCopy.m_followingControlPointLocalPosition;

		this.m_handleMode = pointToCopy.m_handleMode;

		Revalidate();
	}

	public void SetPositionRotationScale( Vector3 localPosition, Quaternion localRotation, Vector3 localScale )
	{
		m_localPosition = localPosition;
		m_localRotation = localRotation;
		m_localEulerAngles = localRotation.eulerAngles;
		m_localScale = localScale;

		Revalidate();
	}

	public void SetPositionRotationScale( Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale )
	{
		m_localPosition = localPosition;
		m_localEulerAngles = localEulerAngles;
		m_localRotation = Quaternion.Euler( localEulerAngles );
		m_localScale = localScale;

		Revalidate();
	}

	public void Revalidate( bool recalculatePosition = true )
	{
		Matrix4x4 splineTransformationMatrix = spline.localToWorldMatrix;

		if( recalculatePosition )
			m_position = splineTransformationMatrix.MultiplyPoint3x4( m_localPosition );

		Matrix4x4 selfTransformationMatrix = Matrix4x4.identity;
		selfTransformationMatrix.SetTRS( m_localPosition, m_localRotation, m_localScale );

		selfTransformationMatrix = splineTransformationMatrix * selfTransformationMatrix;

		m_precedingControlPointPosition = selfTransformationMatrix.MultiplyPoint3x4( m_precedingControlPointLocalPosition );
		m_followingControlPointPosition = selfTransformationMatrix.MultiplyPoint3x4( m_followingControlPointLocalPosition );
	}
}