using System.Collections.Generic;
using UnityEngine;

namespace BezierSolution
{
	[AddComponentMenu( "Bezier Solution/Particles Follow Bezier" )]
	[HelpURL( "https://github.com/yasirkula/UnityBezierSolution" )]
	[RequireComponent( typeof( ParticleSystem ) )]
	[ExecuteInEditMode]
	public class ParticlesFollowBezier : MonoBehaviour
	{
		private const int MAX_PARTICLE_COUNT = 25000;

		public enum FollowMode { Relaxed, Strict };

		public BezierSpline spline;
		public FollowMode followMode = FollowMode.Relaxed;

		private Transform cachedTransform;
		private ParticleSystem cachedPS;
		private ParticleSystem.MainModule cachedMainModule;

		private ParticleSystem.Particle[] particles;
		private List<Vector4> particleData;

		private void Awake()
		{
			cachedTransform = transform;
			cachedPS = GetComponent<ParticleSystem>();

			cachedMainModule = cachedPS.main;
			particles = new ParticleSystem.Particle[cachedMainModule.maxParticles];

			if( followMode == FollowMode.Relaxed )
				particleData = new List<Vector4>( particles.Length );
		}

#if UNITY_EDITOR
		private void OnEnable()
		{
			Awake();
		}
#endif

#if UNITY_EDITOR
		private void LateUpdate()
		{
			if( !UnityEditor.EditorApplication.isPlaying )
				FixedUpdate();
		}
#endif

		private void FixedUpdate()
		{
			if( spline == null || cachedPS == null )
				return;

			if( particles.Length < cachedMainModule.maxParticles && particles.Length < MAX_PARTICLE_COUNT )
				System.Array.Resize( ref particles, Mathf.Min( cachedMainModule.maxParticles, MAX_PARTICLE_COUNT ) );

			bool isLocalSpace = cachedMainModule.simulationSpace != ParticleSystemSimulationSpace.World;
			int aliveParticles = cachedPS.GetParticles( particles );

			Vector3 initialPoint = spline.GetPoint( 0f );
			if( followMode == FollowMode.Relaxed )
			{
				if( particleData == null )
					particleData = new List<Vector4>( particles.Length );

				cachedPS.GetCustomParticleData( particleData, ParticleSystemCustomData.Custom1 );

				// Credit: https://forum.unity3d.com/threads/access-to-the-particle-system-lifecycle-events.328918/#post-2295977
				for( int i = 0; i < aliveParticles; i++ )
				{
					Vector4 particleDat = particleData[i];
					Vector3 point = spline.GetPoint( 1f - ( particles[i].remainingLifetime / particles[i].startLifetime ) );
					if( !isLocalSpace )
						point = cachedTransform.TransformPoint( point - initialPoint );

					// Move particles alongside the spline
					if( particleDat.w != 0f )
						particles[i].position += point - (Vector3) particleDat;

					particleDat = point;
					particleDat.w = 1f;
					particleData[i] = particleDat;
				}

				cachedPS.SetCustomParticleData( particleData, ParticleSystemCustomData.Custom1 );
			}
			else
			{
				for( int i = 0; i < aliveParticles; i++ )
				{
					Vector3 point = spline.GetPoint( 1f - ( particles[i].remainingLifetime / particles[i].startLifetime ) ) - initialPoint;
					if( !isLocalSpace )
						point = cachedTransform.TransformPoint( point );

					particles[i].position = point;
				}
			}

			cachedPS.SetParticles( particles, aliveParticles );
		}
	}
}