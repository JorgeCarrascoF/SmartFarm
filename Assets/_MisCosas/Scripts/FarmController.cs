using System.Collections.Generic;
using UnityEngine;

// Holds the whole farm in one place, works out what needs doing and gives each job to the best free worker.
public class FarmController : MonoBehaviour
{
    public SoilSensor[] sensors;
    public Valve[] valves;
    public WaterTank tank;

    public float alertBelow = 15f;                         // a plot this dry needs a worker
    public float distanceWeight = 100f;                    // every this many metres halve a worker's score

    public List<SoilSensor> dryPlots = new List<SoilSensor>();
    public bool tankAlert;

    public List<Technician> workers = new List<Technician>();
    public List<Job> jobs = new List<Job>();

    void Update()
    {
        dryPlots.Clear();
        foreach (var s in sensors)
            if (s.moisture < alertBelow) dryPlots.Add(s);

        tankAlert = tank.Low;

        UpdateJobs();
        AssignJobs();
    }

    public void Register(Technician worker)
    {
        if (!workers.Contains(worker)) workers.Add(worker);
    }

    public void Complete(Job job)
    {
        jobs.Remove(job);
    }

    // A worker that disappears gives his job back.
    public void Release(Job job)
    {
        job.worker = null;
    }

    void UpdateJobs()
    {
        // Nobody took it and it is not needed any more (a valve got there first).
        jobs.RemoveAll(j => j.worker == null && (j.type == JobType.Tank ? !tankAlert : !dryPlots.Contains(j.plot)));

        if (tankAlert && !jobs.Exists(j => j.type == JobType.Tank))
            jobs.Add(new Job { type = JobType.Tank, position = tank.transform.position });

        foreach (var plot in dryPlots)
            if (!jobs.Exists(j => j.plot == plot))
                jobs.Add(new Job { type = JobType.Plot, plot = plot, position = plot.transform.position });
    }

    void AssignJobs()
    {
        jobs.Sort((a, b) => Priority(a).CompareTo(Priority(b)));

        foreach (var job in jobs)
        {
            if (job.worker != null || job.unreachable) continue;

            Technician best = null;
            float bestScore = 0f;
            foreach (var worker in workers)
            {
                if (!worker.IsFree) continue;
                float score = Score(worker, job);
                if (score > bestScore)
                {
                    best = worker;
                    bestScore = score;
                }
            }
            if (best == null) continue;                    // nobody is free, try again next frame

            if (best.Assign(job))
            {
                job.worker = best;
            }
            else
            {
                job.unreachable = true;
                Debug.LogWarning($"FarmController: no NavMesh near the {job.type} job at {job.position}", this);
            }
        }
    }

    // The tank always goes first, then the driest plot.
    float Priority(Job job)
    {
        return job.type == JobType.Tank ? -1f : job.plot.moisture;
    }

    // Strength matters for the tank, irrigation for the plots, and a long walk counts against the worker.
    float Score(Technician worker, Job job)
    {
        float stat = job.type == JobType.Tank ? worker.StrengthFactor : worker.IrrigationFactor;
        float distance = Vector3.Distance(worker.transform.position, job.position);
        return stat / (1f + distance / distanceWeight);
    }
}
