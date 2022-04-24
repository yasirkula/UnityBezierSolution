using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#if UNITY_2018_3_OR_NEWER && !UNITY_2021_2_OR_NEWER
using PrefabStage = UnityEditor.Experimental.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.Experimental.SceneManagement.PrefabStageUtility;
#endif
#endif

namespace BezierSolution
{
	[AddComponentMenu( "Bezier Solution/Bend Mesh Along Bezier" )]
	[HelpURL( "https://github.com/yasirkula/UnityBezierSolution" )]
	[RequireComponent( typeof( MeshFilter ) )]
	[ExecuteInEditMode]
	public class BendMeshAlongBezier : MonoBehaviour
	{
		public enum VectorMode { DontModify = 0, ModifyOriginals = 1, RecalculateFromScratch = 2 };
		public enum Axis { X = 0, Y = 1, Z = 2 };

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
		[MinMaxRange( 0f, 1f )]
		private Vector2 m_splineSampleRange = new Vector2( 0f, 1f );
		public Vector2 SplineSampleRange
		{
			get { return m_splineSampleRange; }
			set
			{
				value.x = Mathf.Clamp01( value.x );
				value.y = Mathf.Clamp01( value.y );

				if( m_splineSampleRange != value )
				{
					m_splineSampleRange = value;
					OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

		[Header( "Bend Options" )]
		[SerializeField]
		private bool m_highQuality = false;
		public bool highQuality
		{
			get { return m_highQuality; }
			set
			{
				if( m_highQuality != value )
				{
					m_highQuality = value;
					OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

		[SerializeField]
		private Axis m_bendAxis = Axis.Y;
		public Axis bendAxis
		{

			get { return m_bendAxis; }
			set
			{
				if( m_bendAxis != value )
				{
					m_bendAxis = value;

					RecalculateVertexRange();
					OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

		[SerializeField]
		[Range( 0f, 360f )]
		private float m_extraRotation = 0f;
		public float extraRotation
		{
			get { return m_extraRotation; }
			set
			{
				value = Mathf.Clamp( value, 0f, 360f );

				if( m_extraRotation != value )
				{
					m_extraRotation = value;
					OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

		[SerializeField]
		private bool m_invertDirection = false;
		public bool invertDirection
		{
			get { return m_invertDirection; }
			set
			{
				if( m_invertDirection != value )
				{
					m_invertDirection = value;
					OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

		[SerializeField]
		private Vector2 m_thicknessMultiplier = Vector2.one;
		public Vector2 thicknessMultiplier
		{
			get { return m_thicknessMultiplier; }
			set
			{
				if( m_thicknessMultiplier != value )
				{
					m_thicknessMultiplier = value;
					OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

		[Header( "Vertex Attributes" )]
		[SerializeField]
		private VectorMode m_normalsMode = VectorMode.ModifyOriginals;
		public VectorMode normalsMode
		{
			get { return m_normalsMode; }
			set
			{
				if( m_normalsMode != value )
				{
					m_normalsMode = value;

					if( mesh )
					{
						if( m_normalsMode == VectorMode.DontModify && originalNormals != null )
						{
							mesh.normals = originalNormals;
							originalNormals = null;
						}

						if( m_normalsMode != VectorMode.ModifyOriginals )
							normals = null;
					}

					OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

		[SerializeField]
		private VectorMode m_tangentsMode = VectorMode.ModifyOriginals;
		public VectorMode tangentsMode
		{
			get { return m_tangentsMode; }
			set
			{
				if( m_tangentsMode != value )
				{
					m_tangentsMode = value;

					if( mesh )
					{
						if( m_tangentsMode == VectorMode.DontModify && originalTangents != null )
						{
							mesh.tangents = originalTangents;
							originalTangents = null;
						}

						if( m_tangentsMode != VectorMode.ModifyOriginals )
							tangents = null;
					}

					OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

		[Header( "Other Settings" )]
		[SerializeField]
		private bool m_autoRefresh = true;
		public bool autoRefresh
		{
			get { return m_autoRefresh; }
			set
			{
				if( m_autoRefresh != value )
				{
					m_autoRefresh = value;
					OnSplineChanged( m_spline, DirtyFlags.All );
				}
			}
		}

#if UNITY_EDITOR
		[SerializeField]
		private bool executeInEditMode = false;

		[SerializeField, HideInInspector]
		private BezierSpline prevSpline;
		[SerializeField, HideInInspector]
		private VectorMode prevNormalsMode, prevTangentsMode;
		[SerializeField, HideInInspector]
		private bool prevHighQuality;
#endif

		[SerializeField, HideInInspector]
		private Mesh originalMesh; // If this isn't serialized, then sometimes exceptions occur on undo/redo
#pragma warning restore 0649

		private MeshFilter meshFilter;

		private Mesh mesh;
		private Vector3[] vertices, originalVertices;
		private Vector3[] normals, originalNormals;
		private Vector4[] tangents, originalTangents;

		private float minVertex, _1OverVertexRange;

		private void OnEnable()
		{
#if UNITY_EDITOR
			// Restore normals and tangents after assembly reload if they are set to DontModify because otherwise they become null automatically (i.e. information gets lost)
			if( mesh && originalMesh )
			{
				if( m_normalsMode == VectorMode.DontModify )
					mesh.normals = originalMesh.normals;
				if( m_tangentsMode == VectorMode.DontModify )
					mesh.tangents = originalMesh.tangents;
			}

			EditorSceneManager.sceneSaving -= OnSceneSaving;
			EditorSceneManager.sceneSaving += OnSceneSaving;
			EditorSceneManager.sceneSaved -= OnSceneSaved;
			EditorSceneManager.sceneSaved += OnSceneSaved;
#endif

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

#if UNITY_EDITOR
			EditorSceneManager.sceneSaving -= OnSceneSaving;
			EditorSceneManager.sceneSaved -= OnSceneSaved;

			if( !EditorApplication.isPlaying )
				OnDestroy();
#endif
		}

		private void OnDestroy()
		{
			MeshFilter _meshFilter = meshFilter;
			meshFilter = null;

			if( _meshFilter && originalMesh )
				_meshFilter.sharedMesh = originalMesh;

			if( mesh )
				DestroyImmediate( mesh );

#if UNITY_EDITOR && UNITY_2018_3_OR_NEWER
			// This allows removing the 'modified' flag of Mesh Filter's Mesh property but these sorts of things
			// may cause new problems in edge cases so it is commented out
			//if( !EditorApplication.isPlaying && _meshFilter && originalMesh )
			//{
			//	// Revert modified status of the prefab instance's MeshFilter Mesh if possible
			//	MeshFilter prefabMeshFilter = null;
			//	if( PrefabUtility.GetPrefabInstanceStatus( _meshFilter ) == PrefabInstanceStatus.Connected )
			//		prefabMeshFilter = PrefabUtility.GetCorrespondingObjectFromSource( _meshFilter ) as MeshFilter;

			//	if( prefabMeshFilter && prefabMeshFilter.sharedMesh == originalMesh )
			//		PrefabUtility.RevertPropertyOverride( new SerializedObject( _meshFilter ).FindProperty( "m_Mesh" ), InteractionMode.AutomatedAction );
			//}
#endif
		}

		public void Activate()
		{
			enabled = true;
		}

		public void Deactivate()
		{
			OnDestroy();
			enabled = false;
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			EditorApplication.update -= OnValidateImplementation;
			EditorApplication.update += OnValidateImplementation;
		}

		// Calling this code inside OnValidate throws "SendMessage cannot be called during Awake, CheckConsistency, or OnValidate" warnings
		private void OnValidateImplementation()
		{
			EditorApplication.update -= OnValidateImplementation;

			if( !this )
				return;

			BezierSpline _spline = m_spline;
			m_spline = prevSpline;
			spline = prevSpline = _spline;

			bool _highQuality = m_highQuality;
			m_highQuality = prevHighQuality;
			highQuality = prevHighQuality = _highQuality;

			VectorMode _normalsMode = m_normalsMode;
			m_normalsMode = prevNormalsMode;
			normalsMode = prevNormalsMode = _normalsMode;

			VectorMode _tangentsMode = m_tangentsMode;
			m_tangentsMode = prevTangentsMode;
			tangentsMode = prevTangentsMode = _tangentsMode;

			RecalculateVertexRange();

			if( !executeInEditMode && !EditorApplication.isPlaying )
				OnDestroy();
			else if( isActiveAndEnabled )
				OnSplineChanged( m_spline, DirtyFlags.All );

			SceneView.RepaintAll();
		}

		private void OnSceneSaving( UnityEngine.SceneManagement.Scene scene, string path )
		{
			// Restore original mesh before saving the scene
			if( scene == gameObject.scene )
				OnDestroy();
		}

		private void OnSceneSaved( UnityEngine.SceneManagement.Scene scene )
		{
			// Restore modified mesh after saving the scene
			if( scene == gameObject.scene && isActiveAndEnabled )
				OnSplineChanged( m_spline, DirtyFlags.All );
		}
#endif

		private void OnSplineChanged( BezierSpline spline, DirtyFlags dirtyFlags )
		{
#if UNITY_EDITOR
			if( !executeInEditMode && !EditorApplication.isPlaying )
				return;

			if( BuildPipeline.isBuildingPlayer )
				return;

#if UNITY_2018_3_OR_NEWER
			// Don't execute the script in prefab mode
			PrefabStage openPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
			if( openPrefabStage != null && openPrefabStage.IsPartOfPrefabContents( gameObject ) )
				return;
#endif
#endif

			if( m_autoRefresh && ( dirtyFlags & ( DirtyFlags.SplineShapeChanged | DirtyFlags.NormalsChanged ) ) != DirtyFlags.None )
				Refresh();
		}

		private void Initialize()
		{
			meshFilter = GetComponent<MeshFilter>();
			if( meshFilter.sharedMesh ) // It can sometimes be null during undo&redo which causes issues
				originalMesh = meshFilter.sharedMesh;

			if( !originalMesh )
				return;

			if( mesh )
				DestroyImmediate( mesh );

			mesh = Instantiate( originalMesh );
			meshFilter.sharedMesh = mesh;

#if UNITY_EDITOR
			if( !EditorApplication.isPlaying )
				mesh.hideFlags = HideFlags.DontSave;
#endif

			originalVertices = mesh.vertices;
			originalNormals = null;
			originalTangents = null;

			RecalculateVertexRange();
		}

		private void RecalculateVertexRange()
		{
			if( originalVertices == null )
				return;

			minVertex = float.PositiveInfinity;
			float maxVertex = float.NegativeInfinity;

			switch( m_bendAxis )
			{
				case Axis.X:
					for( int i = 0; i < originalVertices.Length; i++ )
					{
						float vertex = originalVertices[i].x;
						if( vertex < minVertex )
							minVertex = originalVertices[i].x;
						if( vertex > maxVertex )
							maxVertex = originalVertices[i].x;
					}
					break;
				case Axis.Y:
					for( int i = 0; i < originalVertices.Length; i++ )
					{
						float vertex = originalVertices[i].y;
						if( vertex < minVertex )
							minVertex = originalVertices[i].y;
						if( vertex > maxVertex )
							maxVertex = originalVertices[i].y;
					}
					break;
				case Axis.Z:
					for( int i = 0; i < originalVertices.Length; i++ )
					{
						float vertex = originalVertices[i].z;
						if( vertex < minVertex )
							minVertex = originalVertices[i].z;
						if( vertex > maxVertex )
							maxVertex = originalVertices[i].z;
					}
					break;
			}

			_1OverVertexRange = 1f / ( maxVertex - minVertex );
		}

		public void Refresh()
		{
			if( !m_spline )
				return;

			if( !meshFilter || ( meshFilter.sharedMesh && meshFilter.sharedMesh != mesh && meshFilter.sharedMesh != originalMesh ) )
				Initialize();

			if( !originalMesh )
				return;

			if( vertices == null || vertices.Length != originalVertices.Length )
				vertices = new Vector3[originalVertices.Length];

			if( m_normalsMode == VectorMode.ModifyOriginals )
			{
				if( originalNormals == null )
					originalNormals = originalMesh.normals;

				if( originalNormals == null || originalNormals.Length < originalVertices.Length ) // If somehow above statement returned null
					normals = null;
				else if( normals == null || normals.Length != originalNormals.Length )
					normals = new Vector3[originalNormals.Length];
			}
			else
				normals = null;

			if( m_tangentsMode == VectorMode.ModifyOriginals )
			{
				if( originalTangents == null )
					originalTangents = originalMesh.tangents;

				if( originalTangents == null || originalTangents.Length < originalVertices.Length ) // If somehow above statement returned null
					tangents = null;
				else if( tangents == null || tangents.Length != originalTangents.Length )
					tangents = new Vector4[originalTangents.Length];
			}
			else
				tangents = null;

			Vector2 _splineSampleRange = m_splineSampleRange;
			if( m_invertDirection )
			{
				float temp = _splineSampleRange.x;
				_splineSampleRange.x = _splineSampleRange.y;
				_splineSampleRange.y = temp;
			}

			bool isSampleRangeForwards = _splineSampleRange.x <= _splineSampleRange.y;
			float splineSampleLength = _splineSampleRange.y - _splineSampleRange.x;
			bool dontInvertModifiedVertexAttributes = ( m_thicknessMultiplier.x > 0f && m_thicknessMultiplier.y > 0f );

			BezierSpline.EvenlySpacedPointsHolder evenlySpacedPoints = m_highQuality ? m_spline.evenlySpacedPoints : null;

			Vector3 initialPoint = m_spline.GetPoint( 0f );
			for( int i = 0; i < originalVertices.Length; i++ )
			{
				Vector3 vertex = originalVertices[i];

				float vertexPosition;
				Vector3 vertexOffset;
				switch( m_bendAxis )
				{
					case Axis.X:
						vertexPosition = vertex.x;
						vertexOffset = new Vector3( vertex.z * m_thicknessMultiplier.x, 0f, vertex.y * m_thicknessMultiplier.y );
						break;
					case Axis.Y:
					default:
						vertexPosition = vertex.y;
						vertexOffset = new Vector3( vertex.x * m_thicknessMultiplier.x, 0f, vertex.z * m_thicknessMultiplier.y );
						break;
					case Axis.Z:
						vertexPosition = vertex.z;
						vertexOffset = new Vector3( vertex.y * m_thicknessMultiplier.x, 0f, vertex.x * m_thicknessMultiplier.y );
						break;
				}

				float normalizedT = _splineSampleRange.x + ( vertexPosition - minVertex ) * _1OverVertexRange * splineSampleLength; // Remap from [minVertex,maxVertex] to _splineSampleRange
				if( m_highQuality )
					normalizedT = evenlySpacedPoints.GetNormalizedTAtPercentage( normalizedT );

				BezierSpline.Segment segment = m_spline.GetSegmentAt( normalizedT );

				Vector3 point = segment.GetPoint() - initialPoint;
				Vector3 tangent = isSampleRangeForwards ? segment.GetTangent() : -segment.GetTangent();
				Quaternion rotation = Quaternion.AngleAxis( m_extraRotation, tangent ) * Quaternion.LookRotation( segment.GetNormal(), tangent );

				Vector3 direction = rotation * vertexOffset;
				vertices[i] = point + direction;

				if( normals != null ) // The only case this happens is when Normals Mode is ModifyOriginals and the original mesh has normals
					normals[i] = rotation * ( dontInvertModifiedVertexAttributes ? originalNormals[i] : -originalNormals[i] );
				if( tangents != null ) // The only case this happens is when Tangents Mode is ModifyOriginals and the original mesh has tangents
				{
					float tangentW = originalTangents[i].w;
					tangents[i] = rotation * ( dontInvertModifiedVertexAttributes ? originalTangents[i] : -originalTangents[i] );
					tangents[i].w = tangentW;
				}
			}

			mesh.vertices = vertices;
			if( m_normalsMode == VectorMode.ModifyOriginals )
				mesh.normals = normals;
			if( m_tangentsMode == VectorMode.ModifyOriginals )
				mesh.tangents = tangents;

			if( m_normalsMode == VectorMode.RecalculateFromScratch )
			{
				mesh.RecalculateNormals();

#if UNITY_EDITOR
				// Cache original normals so that we can reset normals in OnValidate when normals are reset back to DontModify
				if( originalNormals == null )
					originalNormals = originalMesh.normals;
#endif
			}

			if( m_tangentsMode == VectorMode.RecalculateFromScratch )
			{
				mesh.RecalculateTangents();

#if UNITY_EDITOR
				// Cache original tangents so that we can reset tangents in OnValidate when tangents are reset back to DontModify
				if( originalTangents == null )
					originalTangents = originalMesh.tangents;
#endif
			}

			mesh.RecalculateBounds();
		}
	}
}