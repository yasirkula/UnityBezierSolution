﻿using System.Collections.Generic;
using UnityEngine;

namespace BezierSolution
{
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

		void Awake()
		{
			cachedTransform = transform;
			cachedPS = GetComponent<ParticleSystem>();

			cachedMainModule = cachedPS.main;
			particles = new ParticleSystem.Particle[cachedMainModule.maxParticles];

			if( followMode == FollowMode.Relaxed )
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

			if( particles.Length < cachedMainModule.maxParticles && particles.Length < MAX_PARTICLE_COUNT )
				particles = new ParticleSystem.Particle[Mathf.Min( cachedMainModule.maxParticles, MAX_PARTICLE_COUNT )];

			bool isLocalSpace = cachedMainModule.simulationSpace != ParticleSystemSimulationSpace.World;
			int aliveParticles = cachedPS.GetParticles( particles );

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
					if( isLocalSpace )
						point = cachedTransform.InverseTransformPoint( point );
					
					Vector3 startOffset = (Vector3) particleDat;
					if (particleDat.w == 0f)
					{
						// We assume the particle has not travelled yet on the first update,
						// we can now find the offset from the particle system origin
						startOffset = transform.position - particles[i].position;

						if (isLocalSpace) startOffset = cachedTransform.InverseTransformPoint(startOffset);

						particleDat = startOffset;
						particleDat.w = 1f;
						particleData[i] = particleDat;						
						cachedPS.SetCustomParticleData( particleData, ParticleSystemCustomData.Custom1 );
					}

					// Move particles alongside the spline
					particles[i].position = point + startOffset;
				}
			}
			else
			{
				Vector3 deltaPosition = cachedTransform.position - spline.GetPoint( 0f );
				for( int i = 0; i < aliveParticles; i++ )
				{
					Vector3 point = spline.GetPoint( 1f - ( particles[i].remainingLifetime / particles[i].startLifetime ) ) + deltaPosition;
					if( isLocalSpace )
						point = cachedTransform.InverseTransformPoint( point );

					particles[i].position = point;
				}
			}
			
			cachedPS.SetParticles( particles, aliveParticles );
		}
	}
}