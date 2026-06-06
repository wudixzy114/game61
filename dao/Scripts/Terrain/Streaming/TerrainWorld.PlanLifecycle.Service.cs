using System.Threading;
using System.Threading.Tasks;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainWorld
{
    private static class TerrainWorldPlanLifecycleService
    {
        internal static void PrepareGeneratedWorldPlan(TerrainWorld world)
        {
            if (world.GenerateOpenWorldPlanAsync)
            {
                StartOpenWorldPlanJob(world);
                return;
            }

            world._worldPlan = world.GenerateOpenWorldPlan(apply: false);
        }

        internal static void StartOpenWorldPlanJob(TerrainWorld world)
        {
            CancelWorldPlanJob(world);
            world._worldPlanGenerationVersion++;
            TerrainGenerationProfile planProfile = world._profile;
            float worldSize = Mathf.Max(planProfile.ChunkSize, world.OpenWorldPlanWorldSize);
            var cancellation = new CancellationTokenSource();
            Task<TerrainWorldPlan> task = CreateRuntimeOpenWorldPlanAsync(planProfile, worldSize, cancellation.Token);
            ObserveWorldPlanTaskCompletion(task);
            world._worldPlanJob = new PendingWorldPlanJob(world._worldPlanGenerationVersion, planProfile, worldSize, cancellation, task);
            world.MarkStreamingSnapshotDirty();
        }

        internal static void SubmitCompletedWorldPlanJob(TerrainWorld world)
        {
            if (world._worldPlanJob is not { } job || !job.Task.IsCompleted)
            {
                return;
            }

            bool hadPlan = world._worldPlan is not null;
            world._worldPlanJob = null;
            world.MarkStreamingSnapshotDirty();
            if (job.Task.IsCanceled)
            {
                job.Cancellation.Dispose();
                return;
            }

            if (job.Task.IsFaulted)
            {
                job.Cancellation.Dispose();
                GD.PushError($"Open world terrain plan generation failed: {job.Task.Exception?.GetBaseException().Message}");
                return;
            }

            if (job.Version != world._worldPlanGenerationVersion ||
                !job.Profile.Equals(world._profile) ||
                !Mathf.IsEqualApprox(job.WorldSize, Mathf.Max(world._profile.ChunkSize, world.OpenWorldPlanWorldSize)))
            {
                job.Cancellation.Dispose();
                return;
            }

            TerrainWorldPlan plan = job.Task.Result;
            job.Cancellation.Dispose();
            if (world.ValidateGeneratedOpenWorldPlan || world.PrintGeneratedOpenWorldPlanSummary)
            {
                ReportGeneratedOpenWorldPlan(world, plan);
            }

            world._worldPlan = plan;
            ApplyPlanIndexChanges(world, hadPlan);
        }

        internal static void CancelWorldPlanJob(TerrainWorld world)
        {
            if (world._worldPlanJob is not { } job)
            {
                return;
            }

            world._worldPlanJob = null;
            world.MarkStreamingSnapshotDirty();
            if (job.Task.IsCompleted)
            {
                job.Cancellation.Dispose();
                return;
            }

            job.Cancellation.Cancel();
            ObserveRetiredWorldPlanTaskCompletion(job.Task, job.Cancellation);
        }

        internal static void ApplyPlanIndexChanges(TerrainWorld world, bool hadPlanBeforeChange)
        {
            int previousKey = world.TerrainFeatureKey;
            bool hasPlanNow = world._worldPlan is not null;
            world.RebuildPlanIndices();
            world.MarkStreamingSnapshotDirty();

            if (!world._isReady)
            {
                return;
            }

            if (hadPlanBeforeChange && !hasPlanNow)
            {
                world.EmitPlanClearedSignalIfReady();
            }
            else if (hasPlanNow)
            {
                world.EmitPlanReadySignalIfReady();
            }

            if (previousKey == world.TerrainFeatureKey)
            {
                return;
            }

            world.InvalidatePlanDependentStreamingState();
            world.UpdateStreaming(force: true);
        }

        internal static void ReportGeneratedOpenWorldPlan(TerrainWorld world, TerrainWorldPlan plan)
        {
            TerrainWorldPlanningGateResult planningGate = TerrainWorldPlanner.ValidateOpenWorldPlanning(plan);
            TerrainQualityGateResult qualityGate = TerrainQualityAnalyzer.ValidateOpenWorldDefault(plan.QualityReport);
            TerrainExperienceGateResult experienceGate = TerrainExperienceAnalyzer.ValidateOpenWorldDefault(plan.ExperienceReport);
            bool passed = planningGate.Passed && qualityGate.Passed && experienceGate.Passed;

            if (world.PrintGeneratedOpenWorldPlanSummary)
            {
                GD.Print(
                    $"Open world terrain plan {(passed ? "PASS" : "FAIL")}: " +
                    $"{planningGate.Report.PointOfInterestCount} POIs, {planningGate.Report.RouteCount} routes, " +
                    $"settlements V/T/O {planningGate.Report.VillageCount}/{planningGate.Report.TownCount}/{planningGate.Report.OasisHubCount}, " +
                    $"land {qualityGate.Report.LandRatio:0.000}, scenic {qualityGate.Report.ScenicRatio:0.000}, " +
                    $"encounter {experienceGate.Report.AverageEncounterPotential:0.000}, rhythm {experienceGate.Report.RouteRhythmScore:0.000}, " +
                    $"connected {planningGate.Report.ConnectedPointRatio:0.000}, " +
                    $"settlement net {planningGate.Report.ConnectedSettlementRatio:0.000}/{planningGate.Report.SettlementRouteCount}, " +
                    $"coverage {planningGate.Report.PointOfInterestWorldCoverage:0.000}/{planningGate.Report.RouteWorldCoverage:0.000}.");
            }

            if (world.ValidateGeneratedOpenWorldPlan && !passed)
            {
                GD.PushWarning(
                    $"Generated open world terrain plan failed readiness gates. " +
                    $"Planning gate: {planningGate.Passed}, quality gate: {qualityGate.Passed}, experience gate: {experienceGate.Passed}.");
            }
        }

        private static void ObserveWorldPlanTaskCompletion(Task<TerrainWorldPlan> task)
        {
            _ = task.ContinueWith(
                static completed =>
                {
                    if (completed.IsFaulted)
                    {
                        _ = completed.Exception;
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static void ObserveRetiredWorldPlanTaskCompletion(
            Task<TerrainWorldPlan> task,
            CancellationTokenSource cancellation)
        {
            _ = task.ContinueWith(
                static (completed, state) =>
                {
                    if (completed.IsFaulted)
                    {
                        _ = completed.Exception;
                    }

                    ((CancellationTokenSource)state!).Dispose();
                },
                cancellation,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
