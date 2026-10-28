using UnityEngine;
using UnityEngine.AI;

// Tells the Animator how fast the agent is moving, so the character walks while it moves and idles when it stops.
// The farm is huge, so the agent moves speedScale times faster than the clips were made for; the clips play
// faster by the same factor so the feet keep up with the ground.
[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class CharacterAnimation : MonoBehaviour
{
    public float speedScale = 2f;

    NavMeshAgent agent;
    Animator animator;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;      // the agent moves the character, not the animation
    }

    void Update()
    {
        float speed = agent.velocity.magnitude;
        animator.SetFloat("Speed", speed / speedScale);
        animator.speed = speed > 0.1f ? speedScale : 1f;
    }
}
