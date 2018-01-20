using System.Collections.Generic;
using UnityEngine;

namespace BezierSolution
{
	[ExecuteInEditMode]
	public class ParticlesFollowBezier : MonoBehaviour
	{
		public BezierSpline spline;

		private Transform cachedTransform;
		private ParticleSystem cachedPS;

		private ParticleSystem.Particle[] particles;
		private List<Vector4> particleData;

		void Awake()
		{
			cachedTransform = transform;
			cachedPS = GetComponent<ParticleSystem>();

			particles = new ParticleSystem.Particle[cachedPS.main.maxParticles];
			particleData = new List<Vector4>( particles.Length );
		}

#if UNITY_EDITOR
		void OnEnable()
		{
			Awake();
		}
#endif

		void LateUpdate()
		{
			if( spline == null || cachedPS == null )
				return;

			bool isLocalSpace = cachedPS.main.simulationSpace != ParticleSystemSimulationSpace.World;

			int aliveParticles = cachedPS.GetParticles( particles );
			cachedPS.GetCustomParticleData( particleData, ParticleSystemCustomData.Custom1 );

			// Credit: https://forum.unity3d.com/threads/access-to-the-particle-system-lifecycle-events.328918/#post-2295977
			for( int i = 0; i < aliveParticles; i++ )
			{
				Vector4 particleDat = particleData[i];

				Vector3 point = spline.GetPoint( 1f - ( particles[i].remainingLifetime / particles[i].startLifetime ) );
				if( isLocalSpace )
					point = cachedTransform.InverseTransformPoint( point );

				// Move particles alongside the spline
				if( particleDat.w != 0f )
					particles[i].position += point - (Vector3) particleDat;

				particleDat = point;
				particleDat.w = 1f;
				particleData[i] = particleDat;
			}

			cachedPS.SetCustomParticleData( particleData, ParticleSystemCustomData.Custom1 );
			cachedPS.SetParticles( particles, aliveParticles );
		}
	}
}