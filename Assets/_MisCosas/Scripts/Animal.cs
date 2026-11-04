using UnityEngine;
using UnityEngine.AI;

// Wanders around the pen and gets hungry over time. When a worker is on his way, it stands still and waits.
[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class Animal : MonoBehaviour
{
    [Range(0f, 100f)] public float hunger;
    public float hungerRate = 0.6f;         // hunger gained per second
    public float variation = 0.5f;          // each animal gets hungry between 1 - variation and 1 + variation times as fast
    public float hungryAbove = 60f;
    public float wanderRadius = 30f;
    public float speed = 1.2f;
    public Vector2 pause = new Vector2(2f, 8f);     // seconds standing still between walks

    public bool held;                       // a worker is coming, so stay put

    NavMeshAgent agent;
    Animator animator;
    Vector3 center;
    float rate;
    float timer;
    bool walking;
    string playing;

    public string displayName;              // shown on the status board, the object's name if empty

    public bool IsHungry => hunger > hungryAbove;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;       // the agent moves the animal, not the animation
        agent.speed = speed;

        center = transform.position;
        rate = hungerRate * Random.Range(1f - variation, 1f + variation);
        hunger = Random.Range(0f, hungryAbove * 0.7f);      // so they do not all get hungry at once
        timer = Random.Range(pause.x, pause.y);
    }

    void Start()
    {
        if (!agent.isOnNavMesh) Debug.LogWarning("Animal: not on the NavMesh, check that its agent type is Animals", this);
    }

    void Update()
    {
        hunger = Mathf.Min(100f, hunger + rate * Time.deltaTime);

        if (held)
        {
            if (walking)
            {
                agent.ResetPath();
                walking = false;
            }
            Play("idle");
        }
        else if (walking)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            {
                walking = false;
                timer = Random.Range(pause.x, pause.y);
                Play("idle");
            }
        }
        else
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) Wander();
        }
    }

    // A worker has fed it: it is no longer hungry and can walk again.
    public void Feed()
    {
        hunger = 0f;
        held = false;
    }

    void Wander()
    {
        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        Vector3 target = center + new Vector3(offset.x, 0f, offset.y);

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 5f, agent.areaMask))
        {
            agent.SetDestination(hit.position);
            walking = true;
            Play("walk_forward");
        }
        else
        {
            timer = Random.Range(pause.x, pause.y);
        }
    }

    // The pack's controller has no transitions, so the clips are started by name.
    void Play(string state)
    {
        if (playing == state) return;
        playing = state;
        animator.CrossFade(state, 0.25f);
    }
}
