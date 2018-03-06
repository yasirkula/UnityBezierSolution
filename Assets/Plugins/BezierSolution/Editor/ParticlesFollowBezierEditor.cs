using UnityEditor;
using UnityEngine;

namespace BezierSolution
{
	[CustomEditor( typeof( ParticlesFollowBezier ) )]
	[CanEditMultipleObjects]
	public class ParticlesFollowBezierEditor : Editor
	{
		// This class is used to reset the a particle system attached to a ParticlesFollowBezier
		// component when it is selected. Otherwise, particles move in a chaotic way for a while

		private int particlesReset;

		void OnEnable()
		{
			particlesReset = 3;
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			if( Application.isPlaying )
				return;

			if( particlesReset > 0 )
			{
				particlesReset--;
				if( particlesReset == 0 )
				{
					ResetParticles( ( (ParticlesFollowBezier) target ).GetComponentsInParent<ParticlesFollowBezier>() );
					ResetParticles( ( (ParticlesFollowBezier) target ).GetComponentsInChildren<ParticlesFollowBezier>() );
				}
			}
		}

		private void ResetParticles( ParticlesFollowBezier[] targets )
		{
			foreach( ParticlesFollowBezier target in targets )
			{
				ParticleSystem particleSystem = target.GetComponent<ParticleSystem>();
				if( target.spline != null && particleSystem != null && target.enabled )
					particleSystem.Clear();
			}
		}
	}
}